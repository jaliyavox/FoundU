"""HTTP request and response contracts for agent execution."""

from enum import StrEnum
from typing import Annotated, Any, Literal
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, StringConstraints, model_validator


class AgentName(StrEnum):
    DESCRIPTION_PARSER = "description_parser"
    MATCHING = "matching"
    VERIFICATION = "verification"
    COORDINATOR = "coordinator"
    INTAKE = "intake"


class PlanActionType(StrEnum):
    """Finite, non-authoritative actions that may appear in an execution plan."""

    INSPECT_INPUT = "inspect_input"
    CALL_MODEL = "call_model"
    CALL_TOOL = "call_tool"
    VALIDATE_RESULT = "validate_result"
    PRODUCE_RECOMMENDATION = "produce_recommendation"
    REQUEST_HUMAN_REVIEW = "request_human_review"
    COMPLETE = "complete"


class PlanPurpose(StrEnum):
    """Safe labels replace free-form plan reasoning or prompt content."""

    INSPECT_REQUEST = "inspect_request"
    GENERATE_STRUCTURED_OUTPUT = "generate_structured_output"
    RETRIEVE_REPORT = "retrieve_report"
    VALIDATE_OUTPUT = "validate_output"
    PREPARE_RECOMMENDATION = "prepare_recommendation"
    REQUEST_HUMAN_REVIEW = "request_human_review"
    COMPLETE = "complete"


PlanStepIdentifier = Annotated[
    str,
    StringConstraints(
        strip_whitespace=True,
        min_length=1,
        max_length=48,
        pattern=r"^[a-z][a-z0-9_-]*$",
    ),
]
PlanToolName = Annotated[
    str,
    StringConstraints(
        strip_whitespace=True,
        min_length=1,
        max_length=64,
        pattern=r"^[A-Za-z0-9][A-Za-z0-9_-]*$",
    ),
]


class AgentPlanStep(BaseModel):
    """One executable-intent label; it contains no private inputs or reasoning."""

    model_config = ConfigDict(extra="forbid", strict=True)

    step_id: PlanStepIdentifier
    action_type: PlanActionType
    purpose: PlanPurpose
    requires_human_approval: bool = False
    sequence: int = Field(ge=1, le=8)
    tool_name: PlanToolName | None = None

    @model_validator(mode="after")
    def validate_action_shape(self) -> "AgentPlanStep":
        expected_purposes = {
            PlanActionType.INSPECT_INPUT: PlanPurpose.INSPECT_REQUEST,
            PlanActionType.CALL_MODEL: PlanPurpose.GENERATE_STRUCTURED_OUTPUT,
            PlanActionType.CALL_TOOL: PlanPurpose.RETRIEVE_REPORT,
            PlanActionType.VALIDATE_RESULT: PlanPurpose.VALIDATE_OUTPUT,
            PlanActionType.PRODUCE_RECOMMENDATION: PlanPurpose.PREPARE_RECOMMENDATION,
            PlanActionType.REQUEST_HUMAN_REVIEW: PlanPurpose.REQUEST_HUMAN_REVIEW,
            PlanActionType.COMPLETE: PlanPurpose.COMPLETE,
        }
        if self.purpose is not expected_purposes[self.action_type]:
            raise ValueError("Plan purpose must match the action type.")
        if self.action_type is PlanActionType.CALL_TOOL and self.tool_name is None:
            raise ValueError("Tool steps require a tool name.")
        if self.action_type is not PlanActionType.CALL_TOOL and self.tool_name is not None:
            raise ValueError("Only tool steps may name a tool.")
        if self.requires_human_approval is not (
            self.action_type is PlanActionType.REQUEST_HUMAN_REVIEW
        ):
            raise ValueError("Human-approval flags must match the action type.")
        return self


class AgentPlan(BaseModel):
    """A bounded, serializable execution plan—not chain-of-thought or a scratchpad."""

    model_config = ConfigDict(extra="forbid", strict=True)

    agent: AgentName
    steps: list[AgentPlanStep] = Field(min_length=2, max_length=8)

    @model_validator(mode="after")
    def validate_step_order(self) -> "AgentPlan":
        step_ids = [step.step_id for step in self.steps]
        if len(step_ids) != len(set(step_ids)):
            raise ValueError("Plan step IDs must be unique.")
        sequences = [step.sequence for step in self.steps]
        if sequences != list(range(1, len(self.steps) + 1)):
            raise ValueError("Plan sequences must be contiguous and ordered.")
        if self.steps[-1].action_type is not PlanActionType.COMPLETE:
            raise ValueError("Plans must end with completion.")
        if sum(step.action_type is PlanActionType.COMPLETE for step in self.steps) != 1:
            raise ValueError("Plans must contain exactly one completion step.")
        if sum(step.requires_human_approval for step in self.steps) > 1:
            raise ValueError("Plans may request human review only once.")
        tool_names = [step.tool_name for step in self.steps if step.tool_name is not None]
        if len(tool_names) != len(set(tool_names)):
            raise ValueError("Plans may not contain duplicate tool steps.")
        return self


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


NonEmptyText = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]


class PrivateVerificationDetails(BaseModel):
    """Private staff evidence. This model is input-only and is never serialized in responses."""

    model_config = ConfigDict(extra="forbid")

    details: dict[NonEmptyText, NonEmptyText] = Field(default_factory=dict)

    @model_validator(mode="before")
    @classmethod
    def accept_evidence_mapping(cls, value: Any) -> Any:
        # The public contract presents private_verification_details directly as a mapping.
        # Keep the named field internally so this input-only type remains explicit.
        if isinstance(value, dict) and "details" not in value:
            return {"details": value}
        return value


class VerificationQuestion(BaseModel):
    question_id: NonEmptyText
    question: NonEmptyText


class VerificationQuestionDraft(BaseModel):
    """Internal-only LLM wording draft; it never carries ownership evidence."""

    model_config = ConfigDict(extra="forbid", strict=True)

    question_id: Annotated[
        str, StringConstraints(strip_whitespace=True, min_length=1, max_length=64)
    ]
    question_text: Annotated[
        str, StringConstraints(strip_whitespace=True, min_length=1, max_length=240)
    ]


class VerificationQuestionDraftResult(BaseModel):
    """Strict, bounded internal LLM response for verification question wording."""

    model_config = ConfigDict(extra="forbid", strict=True)

    questions: list[VerificationQuestionDraft] = Field(min_length=1, max_length=3)

    @model_validator(mode="after")
    def unique_question_ids(self) -> "VerificationQuestionDraftResult":
        ids = [question.question_id for question in self.questions]
        if len(ids) != len(set(ids)):
            raise ValueError("Question draft identifiers must be unique.")
        return self


class VerificationAnswer(BaseModel):
    question_id: NonEmptyText
    answer: str = ""


class GenerateVerificationQuestionsRequest(BaseModel):
    operation: Literal["generate_questions"]
    claim_id: NonEmptyText
    private_verification_details: PrivateVerificationDetails


class EvaluateVerificationAnswersRequest(BaseModel):
    operation: Literal["evaluate_answers"]
    claim_id: NonEmptyText
    questions: list[VerificationQuestion] = Field(min_length=1)
    private_verification_details: PrivateVerificationDetails
    answers: list[VerificationAnswer]

    @model_validator(mode="after")
    def validate_question_and_answer_ids(self) -> "EvaluateVerificationAnswersRequest":
        question_ids = [question.question_id for question in self.questions]
        answer_ids = [answer.question_id for answer in self.answers]
        if len(question_ids) != len(set(question_ids)):
            raise ValueError("Duplicate question IDs are not allowed.")
        if len(answer_ids) != len(set(answer_ids)):
            raise ValueError("Duplicate answer IDs are not allowed.")
        if any(answer_id not in set(question_ids) for answer_id in answer_ids):
            raise ValueError("Answers must reference a submitted question.")
        return self


VerificationRequest = Annotated[
    GenerateVerificationQuestionsRequest | EvaluateVerificationAnswersRequest,
    Field(discriminator="operation"),
]


class VerificationAnswerEvaluation(BaseModel):
    question_id: str
    result: Literal["match", "partial_match", "no_match", "insufficient"]
    score: float = Field(ge=0.0, le=1.0)


class CoordinatorRequest(BaseModel):
    """Safe, bounded workflow metadata for non-authoritative coordination only."""

    model_config = ConfigDict(extra="forbid", strict=True)

    workflow_id: Annotated[
        str,
        StringConstraints(
            strip_whitespace=True,
            min_length=1,
            max_length=64,
            pattern=r"^[A-Za-z0-9][A-Za-z0-9-]*$",
        ),
    ]
    workflow_type: Literal["claim_verification"]
    claim_status: Literal[
        "Pending",
        "WaitingForAnswer",
        "UnderReview",
        "RevisionRequested",
        "Approved",
        "Rejected",
        "Cancelled",
        "ManualReviewRequired",
    ]
    verification_recommendation: Literal[
        "not_available", "likely_match", "unlikely_match", "manual_review"
    ]
    decision_status: Literal["no_decision", "approved", "rejected", "revision_requested"]
    notification_state: Literal["not_required", "pending", "sent"]

    @model_validator(mode="after")
    def validate_workflow_consistency(self) -> "CoordinatorRequest":
        final_decisions = {
            "Approved": "approved",
            "Rejected": "rejected",
            "RevisionRequested": "revision_requested",
        }
        expected_decision = final_decisions.get(self.claim_status)
        if expected_decision is not None and self.decision_status != expected_decision:
            raise ValueError("Workflow state is inconsistent.")
        undecided_statuses = {
            "Pending",
            "WaitingForAnswer",
            "UnderReview",
            "ManualReviewRequired",
            "Cancelled",
        }
        if self.claim_status in undecided_statuses and self.decision_status != "no_decision":
            raise ValueError("Workflow state is inconsistent.")
        if self.claim_status == "Pending" and self.verification_recommendation != "not_available":
            raise ValueError("Workflow state is inconsistent.")
        if (
            self.claim_status == "UnderReview"
            and self.verification_recommendation != "likely_match"
        ):
            raise ValueError("Workflow state is inconsistent.")
        if (
            self.claim_status == "ManualReviewRequired"
            and self.verification_recommendation not in {"manual_review", "unlikely_match"}
        ):
            raise ValueError("Workflow state is inconsistent.")
        return self


class CoordinatorResult(BaseModel):
    """Bounded recommendation; this model cannot express a business decision or mutation."""

    model_config = ConfigDict(extra="forbid", strict=True)

    recommended_action: Literal[
        "await_staff_review",
        "await_claimant_answers",
        "notify_claimant",
        "no_action",
        "workflow_complete",
    ]
    requires_human_action: bool
    safe_reason_code: Literal[
        "claim_pending",
        "awaiting_claimant_answers",
        "verification_requires_staff_review",
        "final_decision_notification_pending",
        "workflow_complete",
        "inconsistent_workflow_state",
    ]


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
    AgentName.INTAKE: AgentPermissions(
        agent=AgentName.INTAKE,
        has_approval_permission=False,  # Suggests; the owner confirms and the desk verifies
        allow_listed_tools=[],  # ASP.NET searches; the agent never touches the database
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
