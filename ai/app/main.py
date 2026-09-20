"""FoundU AI service with Description-Parsing Agent and LangGraph agent foundation."""

from contextlib import asynccontextmanager
from functools import partial

from fastapi import Depends, FastAPI, HTTPException
from pydantic import BaseModel

from app.agents.checkpoint import checkpoint_config, create_checkpointer
from app.agents.description_parser import description_parser_node, parse_item_description
from app.agents.graph import agent_graph, build_agent_graph
from app.agents.matching import matching_node
from app.agents.models import (
    AgentRunRequest,
    AgentRunResponse,
    DescriptionParseRequest,
    DescriptionParseResult,
)
from app.agents.state import create_initial_state
from app.llm.client import create_llm_client
from app.llm.config import LlmSettings
from app.service_auth import require_service_auth
from app.tools.default_registry import create_default_tool_registry


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Compose one shared LLM client and close it when the FastAPI app stops."""
    llm_client = create_llm_client(LlmSettings.from_environment())
    app.state.llm_client = llm_client
    app.state.checkpointer = create_checkpointer()
    # Shared executable boundary. Agent/model code must use this registry rather than call a
    # tool adapter directly when tools are introduced into a graph node.
    app.state.tool_registry = create_default_tool_registry()
    app.state.agent_graph = build_agent_graph(
        partial(description_parser_node, llm_client=llm_client),
        partial(matching_node, tool_registry=app.state.tool_registry),
        app.state.checkpointer,
    )
    try:
        yield
    finally:
        close = getattr(llm_client, "close", None)
        if callable(close):
            close()
        delattr(app.state, "llm_client")
        delattr(app.state, "checkpointer")
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

    try:
        runtime_graph = getattr(app.state, "agent_graph", agent_graph)
        result = runtime_graph.invoke(
            initial_state, checkpoint_config(initial_state["agent_run_id"])
        )
    except PermissionError as perm_err:
        raise HTTPException(status_code=403, detail=str(perm_err)) from perm_err
    except Exception:
        raise HTTPException(status_code=500, detail="Agent workflow failed safely.") from None

    return AgentRunResponse(
        agent_run_id=result["agent_run_id"],
        agent=result["requested_agent"],
        status="completed",
        output=result["output"],
        trace=result["trace"],
    )
