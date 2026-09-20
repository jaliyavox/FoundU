"""HTTP request and response contracts for agent execution."""

from enum import StrEnum
from typing import Any, Literal
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class AgentName(StrEnum):
    DESCRIPTION_PARSER = "description_parser"
    MATCHING = "matching"
    VERIFICATION = "verification"
    COORDINATOR = "coordinator"


class DescriptionParseRequest(BaseModel):
    description: str = Field(..., min_length=1, description="Raw item description text")


class DescriptionParseResult(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    item_type: str = Field(..., alias="itemType", description="Parsed category or item type")
    primary_color: str = Field(..., alias="primaryColor", description="Primary color of the item")
    secondary_color: str | None = Field(
        None, alias="secondaryColor", description="Secondary color of the item if any"
    )
    identifying_features: list[str] = Field(
        default_factory=list,
        alias="identifyingFeatures",
        description="List of distinctive features or keychains/marks",
    )
    is_valid: bool = Field(
        True, description="Indicates whether description was valid and parseable"
    )
    confidence_score: float = Field(
        1.0, ge=0.0, le=1.0, description="Confidence score of parsing result"
    )
    unclear_reason: str | None = Field(
        None, description="Reason provided if description is invalid or unclear"
    )


class AgentPermissions(BaseModel):
    agent: AgentName
    has_approval_permission: bool = False
    allow_listed_tools: list[str] = Field(default_factory=list)


# Permission Registry: Enforce strict least-privilege permissions
AGENT_PERMISSIONS: dict[AgentName, AgentPermissions] = {
    AgentName.DESCRIPTION_PARSER: AgentPermissions(
        agent=AgentName.DESCRIPTION_PARSER,
        has_approval_permission=False,  # NO permission to approve claims
        allow_listed_tools=[],  # Pure analysis, no external tool invocation
    ),
    AgentName.MATCHING: AgentPermissions(
        agent=AgentName.MATCHING,
        has_approval_permission=False,
        allow_listed_tools=[
            "searchActiveLostReports",
            "getLostReportDetails",
            "getFoundReportDetails",
            "saveMatchCandidate",
        ],
    ),
    AgentName.VERIFICATION: AgentPermissions(
        agent=AgentName.VERIFICATION,
        has_approval_permission=False,
        allow_listed_tools=[
            "getLostReportDetails",
            "getFoundReportDetails",
            "createVerificationChallenge",
            "recordVerificationResult",
        ],
    ),
    AgentName.COORDINATOR: AgentPermissions(
        agent=AgentName.COORDINATOR,
        has_approval_permission=False,  # Human approval required, agent cannot approve claims
        allow_listed_tools=[
            "searchActiveLostReports",
            "getLostReportDetails",
            "getFoundReportDetails",
            "pauseForApproval",
            "readWorkflowState",
            "recordWorkflowStep",
        ],
    ),
}


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
