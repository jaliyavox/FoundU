"""Typed state shared by every node in the FoundU agent graph."""

from typing import Any, TypedDict
from uuid import UUID, uuid4

from app.agents.models import AgentName, AgentPlan, AgentRunRequest
from app.tools.models import ToolExecutionContext


class AgentState(TypedDict, total=False):
    agent_run_id: UUID
    requested_agent: AgentName
    payload: dict[str, Any]
    correlation_id: str | None
    plan: AgentPlan
    output: dict[str, Any]
    trace: list[str]
    error: str | None


def create_initial_state(request: AgentRunRequest) -> AgentState:
    return AgentState(
        agent_run_id=uuid4(),
        requested_agent=request.agent,
        payload=request.payload,
        correlation_id=request.correlation_id,
        output={},
        trace=["request_received"],
        error=None,
    )


def create_tool_execution_context(state: AgentState) -> ToolExecutionContext:
    """Build an immutable tool context from graph-owned state.

    Tool input is intentionally not consulted here.  This is the only graph-side path for a
    future agent node to obtain a context for ``ToolRegistry.execute``.
    """
    return ToolExecutionContext(
        agent=state["requested_agent"],
        agent_run_id=state["agent_run_id"],
        correlation_id=state.get("correlation_id"),
    )
