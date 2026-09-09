"""Typed state shared by every node in the FoundU agent graph."""

from typing import Any, TypedDict
from uuid import UUID, uuid4

from app.agents.models import AgentName, AgentRunRequest


class AgentState(TypedDict, total=False):
    agent_run_id: UUID
    requested_agent: AgentName
    payload: dict[str, Any]
    correlation_id: str | None
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
