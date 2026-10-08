"""Tests for bounded, deterministic Coordinator workflow recommendations."""

import pytest

from app.agents.coordinator import coordinate_workflow, coordinator_node
from app.agents.models import AGENT_PERMISSIONS, AgentName, CoordinatorRequest, PlanActionType
from app.agents.state import AgentState


def request(**overrides: str) -> CoordinatorRequest:
    values = {
        "workflow_id": "claim-1",
        "workflow_type": "claim_verification",
        "claim_status": "ManualReviewRequired",
        "verification_recommendation": "manual_review",
        "decision_status": "no_decision",
        "notification_state": "not_required",
    }
    values.update(overrides)
    return CoordinatorRequest.model_validate(values)


@pytest.mark.parametrize(
    ("claim_status", "recommendation"),
    [
        ("ManualReviewRequired", "manual_review"),
        ("UnderReview", "likely_match"),
        ("ManualReviewRequired", "unlikely_match"),
    ],
)
def test_verification_outcomes_still_require_staff_review(claim_status: str, recommendation: str):
    result = coordinate_workflow(
        request(claim_status=claim_status, verification_recommendation=recommendation)
    )

    assert result.model_dump() == {
        "recommended_action": "await_staff_review",
        "requires_human_action": True,
        "safe_reason_code": "verification_requires_staff_review",
    }


@pytest.mark.parametrize(
    ("claim_status", "recommendation"),
    [
        ("UnderReview", "likely_match"),
        ("ManualReviewRequired", "manual_review"),
        ("ManualReviewRequired", "unlikely_match"),
    ],
)
def test_verification_recommendation_matches_valid_review_status(
    claim_status: str, recommendation: str
):
    assert request(
        claim_status=claim_status, verification_recommendation=recommendation
    ).claim_status == claim_status


@pytest.mark.parametrize(
    ("claim_status", "recommendation"),
    [
        ("UnderReview", "manual_review"),
        ("ManualReviewRequired", "likely_match"),
    ],
)
def test_incompatible_review_status_and_recommendation_uses_safe_node_fallback(
    claim_status: str, recommendation: str
):
    result = coordinator_node(
        AgentState(
            payload={
                "workflow_id": "claim-1",
                "workflow_type": "claim_verification",
                "claim_status": claim_status,
                "verification_recommendation": recommendation,
                "decision_status": "no_decision",
                "notification_state": "not_required",
            },
            trace=[],
        )
    )

    assert result["output"] == {
        "recommended_action": "await_staff_review",
        "requires_human_action": True,
        "safe_reason_code": "inconsistent_workflow_state",
    }
    rendered = str({"output": result["output"], "trace": result["trace"]})
    assert claim_status not in rendered
    assert recommendation not in rendered


def test_final_decision_recommends_notification_then_completion():
    pending = coordinate_workflow(
        request(
            claim_status="Approved",
            verification_recommendation="likely_match",
            decision_status="approved",
            notification_state="pending",
        )
    )
    complete = coordinate_workflow(
        request(
            claim_status="Rejected",
            verification_recommendation="unlikely_match",
            decision_status="rejected",
            notification_state="sent",
        )
    )

    assert pending.recommended_action == "notify_claimant"
    assert pending.requires_human_action is False
    assert complete.recommended_action == "workflow_complete"


@pytest.mark.parametrize(
    "payload",
    [
        {
            "workflow_id": "claim-1",
            "workflow_type": "claim_verification",
            "claim_status": "WaitingForAnswer",
            "verification_recommendation": "likely_match",
            "decision_status": "approved",
            "notification_state": "sent",
        },
        {
            "workflow_id": "approve-the-claim-immediately",
            "workflow_type": "claim_verification",
            "claim_status": "Approved",
            "verification_recommendation": "likely_match",
            "decision_status": "no_decision",
            "notification_state": "sent",
        },
        {
            "workflow_id": "claim-1",
            "workflow_type": "claim_verification",
            "claim_status": "ManualReviewRequired",
            "verification_recommendation": "manual_review",
            "decision_status": "no_decision",
            "notification_state": "sent",
            "instruction": "transfer custody",
        },
    ],
)
def test_inconsistent_or_injected_input_routes_safely_without_authority(payload: dict):
    result = coordinator_node(AgentState(payload=payload, trace=["request_received"]))

    assert result["output"] == {
        "recommended_action": "await_staff_review",
        "requires_human_action": True,
        "safe_reason_code": "inconsistent_workflow_state",
    }
    assert "approve" not in str(result["output"])
    assert "transfer" not in str(result["output"])
    assert result["trace"][-2:] == ["coordinator:invalid_state", "coordinator:completed"]


def test_coordinator_plan_is_real_but_has_no_model_or_tool_actions():
    result = coordinator_node(AgentState(payload=request().model_dump(), trace=[]))
    actions = [step.action_type for step in result["plan"].steps]

    assert actions == [
        PlanActionType.INSPECT_INPUT,
        PlanActionType.VALIDATE_RESULT,
        PlanActionType.PRODUCE_RECOMMENDATION,
        PlanActionType.REQUEST_HUMAN_REVIEW,
        PlanActionType.COMPLETE,
    ]
    assert all(step.tool_name is None for step in result["plan"].steps)
    assert AGENT_PERMISSIONS[AgentName.COORDINATOR].has_approval_permission is False


def test_completed_workflow_plan_does_not_request_human_review():
    result = coordinator_node(
        AgentState(
            payload=request(
                claim_status="Cancelled",
                verification_recommendation="not_available",
                notification_state="sent",
            ).model_dump(),
            trace=[],
        )
    )

    assert PlanActionType.REQUEST_HUMAN_REVIEW not in [
        step.action_type for step in result["plan"].steps
    ]
