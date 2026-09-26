from types import SimpleNamespace
from uuid import UUID, uuid4

import pytest
from fastapi.testclient import TestClient

from app import main
from app.agents.checkpoint import WorkflowStateConfigurationError
from app.agents.models import AgentName, AgentRunRequest
from app.agents.state import create_initial_state
from app.service_auth import SERVICE_KEY_HEADER

client = TestClient(main.app)
SERVICE_KEY = "test-service-key-0123456789-abcdef"
SERVICE_AUTH_HEADERS = {SERVICE_KEY_HEADER: SERVICE_KEY}


@pytest.fixture(autouse=True)
def configured_service_key(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("AI_SERVICE_KEY", SERVICE_KEY)

STUB_AGENTS = {
    "matching": "Matching Agent foundation is ready.",
}


def test_lifespan_rejects_default_postgres_mode_without_a_database_url(
    monkeypatch: pytest.MonkeyPatch,
):
    monkeypatch.delenv("WORKFLOW_STATE_STORE", raising=False)
    monkeypatch.delenv("WORKFLOW_DATABASE_URL", raising=False)

    with pytest.raises(WorkflowStateConfigurationError, match="not configured"):
        with TestClient(main.app):
            pass


@pytest.mark.parametrize(("agent", "message"), STUB_AGENTS.items())
def test_stub_agents_route_to_selected_agent(agent: str, message: str):
    response = client.post(
        "/agents/run",
        json={"agent": agent, "payload": {"ignored_by_stub": True}},
        headers=SERVICE_AUTH_HEADERS,
    )

    assert response.status_code == 200
    body = response.json()
    assert body["agent"] == agent
    assert body["status"] == "completed"
    assert body["output"] == {"stub": True, "message": message}
    assert body["trace"] == ["request_received", f"routed:{agent}", f"executed:{agent}"]


def test_description_parser_agent_run():
    response = client.post(
        "/agents/run",
        json={
            "agent": "description_parser",
            "payload": {"description": "Black laptop bag, grey zipper, small keychain."},
        },
        headers=SERVICE_AUTH_HEADERS,
    )

    assert response.status_code == 200
    body = response.json()
    assert body["agent"] == "description_parser"
    assert body["status"] == "completed"
    assert body["output"]["itemType"] == "Laptop Bag"
    assert body["output"]["primaryColor"] == "Black"
    assert body["output"]["secondaryColor"] == "Grey"
    assert body["output"]["identifyingFeatures"] == ["Small keychain"]
    assert body["trace"] == [
        "request_received",
        "routed:description_parser",
        "executed:description_parser",
    ]


def test_verification_agent_route_runs_real_operation():
    response = client.post(
        "/agents/run",
        json={
            "agent": "verification",
            "payload": {
                "operation": "generate_questions",
                "claim_id": "claim-1",
                "private_verification_details": {"distinctive_mark": "small crack near port"},
            },
        },
        headers=SERVICE_AUTH_HEADERS,
    )

    assert response.status_code == 200
    body = response.json()
    assert body["output"]["questions"] == [
        {
            "question_id": "verification-1",
            "question": "What distinctive mark or damage does the item have?",
        }
    ]
    assert body["trace"] == [
        "request_received",
        "routed:verification",
        "verification:received",
        "verification:generate_questions",
        "verification:completed",
    ]


def test_coordinator_agent_route_runs_real_safe_workflow_recommendation():
    response = client.post(
        "/agents/run",
        json={
            "agent": "coordinator",
            "payload": {
                "workflow_id": "claim-1",
                "workflow_type": "claim_verification",
                "claim_status": "ManualReviewRequired",
                "verification_recommendation": "manual_review",
                "decision_status": "no_decision",
                "notification_state": "not_required",
            },
        },
        headers=SERVICE_AUTH_HEADERS,
    )

    assert response.status_code == 200
    assert response.json()["output"] == {
        "recommended_action": "await_staff_review",
        "requires_human_action": True,
        "safe_reason_code": "verification_requires_staff_review",
    }


def test_fastapi_matching_path_uses_lifespan_composed_read_only_registry():
    with TestClient(main.app) as active_client:
        registry = main.app.state.tool_registry
        assert main.app.state.agent_graph.checkpointer is main.app.state.checkpointer
        response = active_client.post(
            "/agents/run",
            json={
                "agent": "matching",
                "payload": {
                    "operation": "match_reports",
                    "lost_report": {
                        "report_id": "lost-1",
                        "item_type": "Backpack",
                        "primary_color": "Blue",
                    },
                    "found_report": {
                        "report_id": "found-1",
                        "item_type": "Backpack",
                        "primary_color": "Blue",
                    },
                },
            },
            headers=SERVICE_AUTH_HEADERS,
        )

    assert registry is not None
    assert response.status_code == 200
    assert response.json()["output"] == {"recommendation": "match_candidate", "score": 1.0}
    assert response.json()["trace"][2:6] == [
        "tool:attempt:getLostReportDetails",
        "tool:success:getLostReportDetails",
        "tool:attempt:getFoundReportDetails",
        "tool:success:getFoundReportDetails",
    ]


def test_parse_description_endpoint():
    response = client.post(
        "/agents/parse-description",
        json={"description": "Black laptop bag, grey zipper, small keychain."},
        headers=SERVICE_AUTH_HEADERS,
    )

    assert response.status_code == 200
    data = response.json()
    assert data["itemType"] == "Laptop Bag"
    assert data["primaryColor"] == "Black"
    assert data["secondaryColor"] == "Grey"
    assert data["identifyingFeatures"] == ["Small keychain"]
    assert data["is_valid"] is True


def test_fastapi_endpoint_and_graph_share_composed_fake_llm_client():
    with TestClient(main.app) as active_client:
        fake = main.app.state.llm_client
        fake.queue_response(
            {
                "item_type": "Backpack",
                "primary_color": "Blue",
                "secondary_color": "Red",
                "identifying_features": ["Red keychain"],
                "is_valid": True,
                "confidence_score": 0.9,
            }
        )
        endpoint_response = active_client.post(
            "/agents/parse-description",
            json={"description": "Blue backpack with a red keychain"},
            headers=SERVICE_AUTH_HEADERS,
        )
        assert endpoint_response.status_code == 200
        assert endpoint_response.json()["confidence_score"] == 0.9

        fake.queue_response(
            {
                "item_type": "Backpack",
                "primary_color": "Blue",
                "secondary_color": "Red",
                "identifying_features": ["Red keychain"],
                "is_valid": True,
                "confidence_score": 0.8,
            }
        )
        graph_response = active_client.post(
            "/agents/run",
            json={
                "agent": "description_parser",
                "payload": {"description": "Blue backpack with a red keychain"},
            },
            headers=SERVICE_AUTH_HEADERS,
        )

    assert graph_response.status_code == 200
    assert graph_response.json()["output"]["confidence_score"] == 0.8
    assert graph_response.json()["trace"][-3:] == [
        "description_parser:llm_attempt",
        "description_parser:llm_success",
        "executed:description_parser",
    ]


def test_invalid_agent_returns_validation_error():
    response = client.post(
        "/agents/run",
        json={"agent": "reader", "payload": {}},
        headers=SERVICE_AUTH_HEADERS,
    )

    assert response.status_code == 422


def test_each_request_generates_a_new_agent_run_id():
    first = client.post(
        "/agents/run", json={"agent": "matching", "payload": {}}, headers=SERVICE_AUTH_HEADERS
    )
    second = client.post(
        "/agents/run", json={"agent": "matching", "payload": {}}, headers=SERVICE_AUTH_HEADERS
    )

    first_id = UUID(first.json()["agent_run_id"])
    second_id = UUID(second.json()["agent_run_id"])
    assert first_id != second_id


def test_authenticated_workflow_state_endpoint_loads_safe_completed_state():
    with TestClient(main.app) as active_client:
        created = active_client.post(
            "/agents/run",
            json={"agent": "matching", "payload": {}},
            headers=SERVICE_AUTH_HEADERS,
        )
        workflow_id = created.json()["agent_run_id"]
        state = active_client.get(
            f"/agents/workflows/{workflow_id}?agent=matching",
            headers=SERVICE_AUTH_HEADERS,
        )

    assert created.status_code == 200
    assert state.status_code == 200
    body = state.json()
    assert body["agent_run_id"] == workflow_id
    assert body["agent"] == "matching"
    assert body["status"] == "completed"
    assert body["plan"]["agent"] == "matching"
    assert body["completed_step_ids"]
    assert "payload" not in body
    assert "prompt" not in body


def test_repeated_durable_workflow_id_returns_saved_result_without_replaying():
    workflow_id = str(uuid4())
    with TestClient(main.app) as active_client:
        first = active_client.post(
            "/agents/run",
            json={"agent": "matching", "payload": {}, "workflow_id": workflow_id},
            headers=SERVICE_AUTH_HEADERS,
        )
        second = active_client.post(
            "/agents/run",
            json={"agent": "matching", "payload": {}, "workflow_id": workflow_id},
            headers=SERVICE_AUTH_HEADERS,
        )
        saved = main.app.state.workflow_state_store.load(UUID(workflow_id), AgentName.MATCHING)

    assert first.status_code == 200
    assert second.status_code == 200
    assert second.json()["agent_run_id"] == workflow_id
    assert saved["state"]["trace"].count("executed:matching") == 1


def test_in_progress_workflow_id_is_rejected_without_executing_a_second_graph_run():
    workflow_id = uuid4()
    with TestClient(main.app) as active_client:
        state_store = main.app.state.workflow_state_store
        initial_state = create_initial_state(
            AgentRunRequest(agent=AgentName.MATCHING, workflow_id=workflow_id)
        )
        assert state_store.create(workflow_id, AgentName.MATCHING, initial_state)
        state_store.update(workflow_id, AgentName.MATCHING, "executing", initial_state)

        response = active_client.post(
            "/agents/run",
            json={"agent": "matching", "payload": {}, "workflow_id": str(workflow_id)},
            headers=SERVICE_AUTH_HEADERS,
        )
        saved = state_store.load(workflow_id, AgentName.MATCHING)

    assert response.status_code == 409
    assert response.json() == {"detail": "Workflow is already in progress."}
    assert "executed:matching" not in saved["state"].get("trace", [])


def test_workflow_state_endpoint_requires_authentication_and_hides_missing_state():
    workflow_id = "00000000-0000-0000-0000-000000000001"

    unauthenticated = client.get(f"/agents/workflows/{workflow_id}?agent=matching")
    missing = client.get(
        f"/agents/workflows/{workflow_id}?agent=matching",
        headers=SERVICE_AUTH_HEADERS,
    )

    assert unauthenticated.status_code == 401
    assert missing.status_code == 404
    assert missing.json() == {"detail": "Workflow state is unavailable."}


def test_unexpected_graph_failure_returns_safe_error(monkeypatch: pytest.MonkeyPatch):
    def fail_safely(_state):
        raise RuntimeError("sensitive internal detail")

    monkeypatch.setattr(main, "agent_graph", SimpleNamespace(invoke=fail_safely))
    response = client.post(
        "/agents/run",
        json={"agent": "coordinator", "payload": {}},
        headers=SERVICE_AUTH_HEADERS,
    )

    assert response.status_code == 500
    assert response.json() == {"detail": "Agent workflow failed safely."}
    assert "sensitive internal detail" not in response.text
