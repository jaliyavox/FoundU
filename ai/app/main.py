"""FoundU AI service with Description-Parsing Agent and LangGraph agent foundation."""

import logging
from contextlib import asynccontextmanager
from functools import partial
from uuid import UUID

from fastapi import Depends, FastAPI, HTTPException
from pydantic import BaseModel

from app.agents.checkpoint import (
    WorkflowStateStoreError,
    checkpoint_config,
    create_checkpointer,
    create_workflow_state_store,
)
from app.agents.description_parser import description_parser_node, parse_item_description
from app.agents.graph import agent_graph, build_agent_graph
from app.agents.intake import intake_node
from app.agents.matching import matching_node
from app.agents.models import (
    AgentName,
    AgentPlan,
    AgentRunRequest,
    AgentRunResponse,
    DescriptionParseRequest,
    DescriptionParseResult,
    WorkflowStateResponse,
)
from app.agents.state import create_initial_state
from app.agents.verification import verification_node
from app.llm.client import create_llm_client
from app.llm.config import LlmSettings
from app.service_auth import require_service_auth
from app.tools.default_registry import create_default_tool_registry

logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Compose one shared LLM client and close it when the FastAPI app stops."""
    llm_client = create_llm_client(LlmSettings.from_environment())
    app.state.llm_client = llm_client
    app.state.checkpointer = create_checkpointer()
    app.state.workflow_state_store = create_workflow_state_store()
    # Shared executable boundary. Agent/model code must use this registry rather than call a
    # tool adapter directly when tools are introduced into a graph node.
    app.state.tool_registry = create_default_tool_registry()
    app.state.agent_graph = build_agent_graph(
        partial(description_parser_node, llm_client=llm_client),
        partial(matching_node, tool_registry=app.state.tool_registry),
        app.state.checkpointer,
        verification_handler=partial(verification_node, llm_client=llm_client),
        intake_handler=partial(intake_node, llm_client=llm_client),
    )
    try:
        yield
    finally:
        close = getattr(llm_client, "close", None)
        if callable(close):
            close()
        delattr(app.state, "llm_client")
        delattr(app.state, "checkpointer")
        delattr(app.state, "workflow_state_store")
        delattr(app.state, "tool_registry")
        delattr(app.state, "agent_graph")


app = FastAPI(title="FoundU AI Service", version="0.1.0", lifespan=lifespan)


class HealthResponse(BaseModel):
    status: str
    service: str


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    return HealthResponse(status="ok", service="FoundU AI Service")


@app.post("/agents/parse-description", response_model=DescriptionParseResult)
def parse_description_endpoint(
    request: DescriptionParseRequest,
    _: None = Depends(require_service_auth),
) -> DescriptionParseResult:
    """Extract structured item attributes from natural language description."""
    try:
        return parse_item_description(
            request.description,
            llm_client=getattr(app.state, "llm_client", None),
        )
    except PermissionError as perm_err:
        raise HTTPException(status_code=403, detail=str(perm_err)) from perm_err
    except Exception as exc:
        raise HTTPException(status_code=500, detail="Description parsing failed safely.") from exc


@app.post("/agents/run", response_model=AgentRunResponse)
def run_agent(
    request: AgentRunRequest,
    _: None = Depends(require_service_auth),
) -> AgentRunResponse:
    initial_state = create_initial_state(request)
    workflow_id = initial_state["agent_run_id"]
    state_store = getattr(app.state, "workflow_state_store", None)
    if state_store is not None:
        try:
            if not state_store.create(workflow_id, request.agent, initial_state):
                existing = state_store.load(workflow_id, request.agent)
                if existing["status"] == "completed":
                    result = existing["state"]
                    return AgentRunResponse(
                        agent_run_id=workflow_id,
                        agent=request.agent,
                        status="completed",
                        output=result.get("output", {}),
                        trace=result.get("trace", []),
                    )
                raise HTTPException(status_code=409, detail="Workflow is already in progress.")
            logger.info(
                "workflow_created workflow_id=%s agent=%s", workflow_id, request.agent.value
            )
            state_store.update(workflow_id, request.agent, "planning", initial_state)
            logger.info("workflow_planning workflow_id=%s", workflow_id)
            state_store.update(workflow_id, request.agent, "executing", initial_state)
            logger.info("workflow_executing workflow_id=%s", workflow_id)
        except WorkflowStateStoreError:
            raise HTTPException(
                status_code=503, detail="Workflow persistence is unavailable."
            ) from None

    try:
        runtime_graph = getattr(app.state, "agent_graph", agent_graph)
        result = runtime_graph.invoke(
            initial_state, checkpoint_config(initial_state["agent_run_id"])
        )
    except PermissionError as perm_err:
        _mark_workflow_failed(state_store, workflow_id, request.agent)
        raise HTTPException(status_code=403, detail=str(perm_err)) from perm_err
    except Exception:
        _mark_workflow_failed(state_store, workflow_id, request.agent)
        raise HTTPException(status_code=500, detail="Agent workflow failed safely.") from None

    if state_store is not None:
        try:
            persisted_result = {
                **result,
                "completed_step_ids": [step.step_id for step in result["plan"].steps],
                "approval_required": bool(result["output"].get("requires_human_action", False)),
                "approval_status": (
                    "pending"
                    if result["output"].get("requires_human_action", False)
                    else "not_required"
                ),
                "final_outcome": result["output"],
            }
            workflow_status = (
                "waiting_for_approval"
                if persisted_result["approval_required"]
                else "completed"
            )
            state_store.update(workflow_id, request.agent, workflow_status, persisted_result)
            logger.info("workflow_%s workflow_id=%s", workflow_status, workflow_id)
        except WorkflowStateStoreError:
            raise HTTPException(
                status_code=503, detail="Workflow persistence is unavailable."
            ) from None

    return AgentRunResponse(
        agent_run_id=result["agent_run_id"],
        agent=result["requested_agent"],
        status="completed",
        output=result["output"],
        trace=result["trace"],
    )


@app.get("/agents/workflows/{workflow_id}", response_model=WorkflowStateResponse)
def workflow_state(
    workflow_id: UUID,
    agent: AgentName,
    _: None = Depends(require_service_auth),
) -> WorkflowStateResponse:
    """Load a sanitized durable workflow summary without replaying a completed graph node."""
    try:
        state_store = getattr(app.state, "workflow_state_store", None)
        if state_store is None:
            raise WorkflowStateStoreError()
        record = state_store.load(workflow_id, agent)
        state = record["state"]
        raw_plan = state.get("plan")
        plan = AgentPlan.model_validate(raw_plan) if raw_plan is not None else None
        output = state.get("output", {})
        approval_required = bool(state.get("approval_required", False))
        validation_failed = "plan:rejected" in state.get("trace", [])
        workflow_status = (
            "waiting_for_approval"
            if approval_required
            else record["status"]
        )
        if workflow_status not in {
            "created",
            "planning",
            "executing",
            "waiting_for_approval",
            "completed",
            "failed",
        }:
            raise WorkflowStateStoreError()
        return WorkflowStateResponse(
            agent_run_id=workflow_id,
            agent=agent,
            status=workflow_status,
            plan=plan,
            completed_step_ids=state.get("completed_step_ids", []),
            output=output,
            validation_status="failed" if validation_failed else "passed",
            approval_required=approval_required,
            approval_status=state.get("approval_status", "not_required"),
            error=state.get("error"),
        )
    except WorkflowStateStoreError:
        raise HTTPException(status_code=404, detail="Workflow state is unavailable.") from None
    except Exception:
        raise HTTPException(status_code=500, detail="Workflow state is unavailable.") from None


def _mark_workflow_failed(state_store, workflow_id: UUID, agent: AgentName) -> None:
    """Record a bounded failure marker without serializing exception details."""
    if state_store is None:
        return
    try:
        state_store.update(workflow_id, agent, "failed", {"error": "workflow_failed"})
        logger.warning("workflow_failed workflow_id=%s", workflow_id)
    except WorkflowStateStoreError:
        pass
