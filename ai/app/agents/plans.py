"""Deterministic, validated execution plans for constrained FoundU agents."""

from app.agents.models import AgentName, AgentPlan, AgentPlanStep, PlanActionType, PlanPurpose
from app.tools.registry import ToolRegistry


class PlanValidationError(Exception):
    """Safe plan-domain failure that never retains plan contents or implementation details."""

    trace_event = "plan:rejected"

    def __init__(self) -> None:
        super().__init__("Agent plan is invalid.")


def _step(
    step_id: str,
    action_type: PlanActionType,
    purpose: PlanPurpose,
    sequence: int,
    *,
    tool_name: str | None = None,
    requires_human_approval: bool = False,
) -> AgentPlanStep:
    return AgentPlanStep(
        step_id=step_id,
        action_type=action_type,
        purpose=purpose,
        sequence=sequence,
        tool_name=tool_name,
        requires_human_approval=requires_human_approval,
    )


def build_description_parser_plan() -> AgentPlan:
    return AgentPlan(
        agent=AgentName.DESCRIPTION_PARSER,
        steps=[
            _step("inspect-input", PlanActionType.INSPECT_INPUT, PlanPurpose.INSPECT_REQUEST, 1),
            _step(
                "call-model",
                PlanActionType.CALL_MODEL,
                PlanPurpose.GENERATE_STRUCTURED_OUTPUT,
                2,
            ),
            _step(
                "validate-result", PlanActionType.VALIDATE_RESULT, PlanPurpose.VALIDATE_OUTPUT, 3
            ),
            _step(
                "produce-result",
                PlanActionType.PRODUCE_RECOMMENDATION,
                PlanPurpose.PREPARE_RECOMMENDATION,
                4,
            ),
            _step("complete", PlanActionType.COMPLETE, PlanPurpose.COMPLETE, 5),
        ],
    )


def build_matching_plan(match_reports: bool) -> AgentPlan:
    if not match_reports:
        return AgentPlan(
            agent=AgentName.MATCHING,
            steps=[
                _step(
                    "inspect-input", PlanActionType.INSPECT_INPUT, PlanPurpose.INSPECT_REQUEST, 1
                ),
                _step("complete", PlanActionType.COMPLETE, PlanPurpose.COMPLETE, 2),
            ],
        )
    return AgentPlan(
        agent=AgentName.MATCHING,
        steps=[
            _step("inspect-input", PlanActionType.INSPECT_INPUT, PlanPurpose.INSPECT_REQUEST, 1),
            _step(
                "lookup-lost-report",
                PlanActionType.CALL_TOOL,
                PlanPurpose.RETRIEVE_REPORT,
                2,
                tool_name="getLostReportDetails",
            ),
            _step(
                "lookup-found-report",
                PlanActionType.CALL_TOOL,
                PlanPurpose.RETRIEVE_REPORT,
                3,
                tool_name="getFoundReportDetails",
            ),
            _step(
                "validate-results", PlanActionType.VALIDATE_RESULT, PlanPurpose.VALIDATE_OUTPUT, 4
            ),
            _step(
                "produce-recommendation",
                PlanActionType.PRODUCE_RECOMMENDATION,
                PlanPurpose.PREPARE_RECOMMENDATION,
                5,
            ),
            _step("complete", PlanActionType.COMPLETE, PlanPurpose.COMPLETE, 6),
        ],
    )


def build_verification_plan() -> AgentPlan:
    return AgentPlan(
        agent=AgentName.VERIFICATION,
        steps=[
            _step("inspect-input", PlanActionType.INSPECT_INPUT, PlanPurpose.INSPECT_REQUEST, 1),
            _step(
                "validate-result", PlanActionType.VALIDATE_RESULT, PlanPurpose.VALIDATE_OUTPUT, 2
            ),
            _step(
                "request-human-review",
                PlanActionType.REQUEST_HUMAN_REVIEW,
                PlanPurpose.REQUEST_HUMAN_REVIEW,
                3,
                requires_human_approval=True,
            ),
            _step("complete", PlanActionType.COMPLETE, PlanPurpose.COMPLETE, 4),
        ],
    )


def build_coordinator_plan() -> AgentPlan:
    return AgentPlan(
        agent=AgentName.COORDINATOR,
        steps=[
            _step("inspect-input", PlanActionType.INSPECT_INPUT, PlanPurpose.INSPECT_REQUEST, 1),
            _step("complete", PlanActionType.COMPLETE, PlanPurpose.COMPLETE, 2),
        ],
    )


def validate_agent_plan(
    plan: object,
    trusted_agent: AgentName,
    tool_registry: ToolRegistry | None = None,
) -> AgentPlan:
    """Validate plan intent against trusted agent identity and registered tool metadata.

    The registry remains authoritative at execution time; this check grants no capability.
    """
    try:
        plan_data = plan.model_dump() if isinstance(plan, AgentPlan) else plan
        validated_plan = AgentPlan.model_validate(plan_data)
        if validated_plan.agent is not trusted_agent:
            raise ValueError
        for step in validated_plan.steps:
            if step.tool_name is None:
                continue
            if tool_registry is None:
                raise ValueError
            definition = tool_registry.get(step.tool_name)
            if trusted_agent not in definition.allowed_agents:
                raise ValueError
    except Exception:
        is_invalid = True
    else:
        is_invalid = False
    if is_invalid:
        raise PlanValidationError()
    return validated_plan


def plan_includes_tool(plan: AgentPlan, tool_name: str) -> bool:
    """Check planned intent only; callers must still invoke the registry for authorization."""
    return any(
        step.action_type is PlanActionType.CALL_TOOL and step.tool_name == tool_name
        for step in plan.steps
    )
