"""Typed, local-only contracts for the constrained FoundU tool boundary."""

from typing import Annotated, Literal
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, StringConstraints, model_validator

from app.agents.models import AgentName

ToolIdentifier = Annotated[
    str,
    StringConstraints(
        strip_whitespace=True,
        min_length=1,
        max_length=64,
        pattern=r"^[A-Za-z0-9][A-Za-z0-9_-]*$",
    ),
]
ShortText = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=160)]
ReportText = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=80)]


class StrictToolModel(BaseModel):
    """All registry payloads reject unknown fields and unsafe coercion."""

    model_config = ConfigDict(extra="forbid", strict=True)


class ToolExecutionContext(StrictToolModel):
    """Trusted graph/application context; never derive this identity from tool input."""

    model_config = ConfigDict(extra="forbid", strict=True, frozen=True)

    agent: AgentName
    agent_run_id: UUID
    correlation_id: str | None = Field(default=None, max_length=128)


class ReportLookupInput(StrictToolModel):
    report_id: ToolIdentifier
    report: "ReportSummary | None" = None

    @model_validator(mode="after")
    def report_identifier_matches_lookup(self) -> "ReportLookupInput":
        if self.report is not None and self.report.report_id != self.report_id:
            raise ValueError("Report context does not match the requested identifier.")
        return self


class SuppliedReportContext(StrictToolModel):
    """Read-only request context supplied by the trusted application boundary.

    ``description`` is accepted as ordinary data but is intentionally excluded from lookup tools,
    scoring, tool selection, permissions, and workflow authority.
    """

    report_id: ToolIdentifier
    item_type: ReportText
    primary_color: ReportText
    description: Annotated[
        str | None,
        StringConstraints(strip_whitespace=True, max_length=500),
    ] = None


class ReportSummary(StrictToolModel):
    """Minimum public fields that a matching agent needs for deterministic comparison."""

    report_id: ToolIdentifier
    item_type: ReportText
    primary_color: ReportText


class ReportLookupOutput(StrictToolModel):
    report_id: ToolIdentifier
    found: bool
    report: ReportSummary | None = None

    @model_validator(mode="after")
    def found_status_matches_report(self) -> "ReportLookupOutput":
        if self.found and self.report is None:
            raise ValueError("Found reports require a report summary.")
        if not self.found and self.report is not None:
            raise ValueError("Missing reports cannot include a report summary.")
        if self.report is not None and self.report.report_id != self.report_id:
            raise ValueError("Report summary identifier does not match the lookup identifier.")
        return self


class SearchActiveLostReportsInput(StrictToolModel):
    query: ShortText


class SearchActiveLostReportsOutput(StrictToolModel):
    report_ids: list[ToolIdentifier] = Field(default_factory=list, max_length=20)


class SaveMatchCandidateInput(StrictToolModel):
    lost_report_id: ToolIdentifier
    found_report_id: ToolIdentifier
    score: float = Field(strict=True, ge=0.0, le=1.0)


class SaveMatchCandidateOutput(StrictToolModel):
    recorded: bool


class CreateVerificationChallengeInput(StrictToolModel):
    claim_id: ToolIdentifier
    question_ids: list[ToolIdentifier] = Field(min_length=1, max_length=3)

    @model_validator(mode="after")
    def unique_question_ids(self) -> "CreateVerificationChallengeInput":
        if len(self.question_ids) != len(set(self.question_ids)):
            raise ValueError("Question IDs must be unique.")
        return self


class CreateVerificationChallengeOutput(StrictToolModel):
    challenge_id: ToolIdentifier


class RecordVerificationResultInput(StrictToolModel):
    claim_id: ToolIdentifier
    recommendation: Literal["likely_match", "manual_review", "unlikely_match"]


class RecordVerificationResultOutput(StrictToolModel):
    recorded: bool


class PauseForApprovalInput(StrictToolModel):
    workflow_id: ToolIdentifier
    reason_code: Annotated[
        str,
        StringConstraints(strip_whitespace=True, min_length=1, max_length=64, pattern=r"^[a-z_]+$"),
    ]


class PauseForApprovalOutput(StrictToolModel):
    paused: bool


class ReadWorkflowStateInput(StrictToolModel):
    workflow_id: ToolIdentifier


class ReadWorkflowStateOutput(StrictToolModel):
    state: Literal["awaiting_human"]


class RecordWorkflowStepInput(StrictToolModel):
    workflow_id: ToolIdentifier
    step_name: Annotated[
        str,
        StringConstraints(strip_whitespace=True, min_length=1, max_length=64, pattern=r"^[a-z_]+$"),
    ]


class RecordWorkflowStepOutput(StrictToolModel):
    recorded: bool
