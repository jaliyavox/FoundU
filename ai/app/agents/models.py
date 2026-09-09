"""HTTP request and response contracts for agent execution."""

from enum import StrEnum
from typing import Any, Literal
from uuid import UUID

from pydantic import BaseModel, Field


class AgentName(StrEnum):
    DESCRIPTION_PARSER = "description_parser"
    MATCHING = "matching"
    VERIFICATION = "verification"
    COORDINATOR = "coordinator"


class AgentRunRequest(BaseModel):
    agent: AgentName
    payload: dict[str, Any] = Field(default_factory=dict)
    correlation_id: str | None = None


class AgentRunResponse(BaseModel):
    agent_run_id: UUID
    agent: AgentName
    status: Literal["completed"]
    output: dict[str, Any]
    trace: list[str]
