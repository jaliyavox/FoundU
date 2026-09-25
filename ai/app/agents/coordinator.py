"""Deterministic, non-authoritative workflow coordination agent."""

from pydantic import ValidationError

from app.agents.models import AGENT_PERMISSIONS, AgentName, CoordinatorRequest, CoordinatorResult
from app.agents.plans import PlanValidationError, build_coordinator_plan, validate_agent_plan
from app.agents.state import AgentState


def check_agent_permissions() -> None:
    """Coordinator can recommend a human action but cannot decide a claim."""
    permissions = AGENT_PERMISSIONS.get(AgentName.COORDINATOR)
    if not permissions or permissions.has_approval_permission:
        raise PermissionError("Coordinator Agent does NOT have permission to decide claims.")


def coordinate_workflow(request: CoordinatorRequest) -> CoordinatorResult:
    """Map validated, public workflow state to a fixed next-step recommendation."""
    if request.claim_status in {"UnderReview", "ManualReviewRequired"}:
        return CoordinatorResult(
            recommended_action="await_staff_review",
            requires_human_action=True,
            safe_reason_code="verification_requires_staff_review",
        )
    if request.claim_status in {"WaitingForAnswer", "RevisionRequested"}:
        return CoordinatorResult(
            recommended_action="await_claimant_answers",
            requires_human_action=False,
            safe_reason_code="awaiting_claimant_answers",
        )
    if request.claim_status in {"Approved", "Rejected", "Cancelled"}:
        if request.notification_state == "pending":
            return CoordinatorResult(
                recommended_action="notify_claimant",
                requires_human_action=False,
                safe_reason_code="final_decision_notification_pending",
            )
        return CoordinatorResult(
            recommended_action="workflow_complete",
            requires_human_action=False,
            safe_reason_code="workflow_complete",
        )
    return CoordinatorResult(
        recommended_action="no_action",
        requires_human_action=False,
        safe_reason_code="claim_pending",
    )


def _invalid_state_result() -> CoordinatorResult:
    """Do not reveal validation input; ask the authoritative workflow to review it."""
    return CoordinatorResult(
        recommended_action="await_staff_review",
        requires_human_action=True,
        safe_reason_code="inconsistent_workflow_state",
    )


def coordinator_node(state: AgentState) -> AgentState:
    """Coordinate safe workflow metadata without executing business decisions or tools."""
    trace = [*state.get("trace", []), "coordinator:received"]
    try:
        request = CoordinatorRequest.model_validate(state.get("payload", {}))
    except ValidationError:
        plan = build_coordinator_plan(requires_human_action=True)
        return {
            "output": _invalid_state_result().model_dump(),
            "trace": [*trace, "coordinator:invalid_state", "coordinator:completed"],
            "plan": plan,
        }
    result = coordinate_workflow(request)
    plan = build_coordinator_plan(result.requires_human_action)
    try:
        validate_agent_plan(plan, state.get("requested_agent", AgentName.COORDINATOR))
    except PlanValidationError:
        return {
            "output": _invalid_state_result().model_dump(),
            "trace": [*trace, "plan:rejected"],
            "plan": plan,
        }
    check_agent_permissions()
    return {
        "output": result.model_dump(),
        "trace": [
            *trace,
            "coordinator:validated",
            "coordinator:recommendation",
            "coordinator:completed",
        ],
        "plan": plan,
    }
