from types import SimpleNamespace
from uuid import UUID

import pytest
from fastapi.testclient import TestClient

from app import main

client = TestClient(main.app)

AGENTS = {
    "description_parser": "Description Parsing Agent foundation is ready.",
    "matching": "Matching Agent foundation is ready.",
    "verification": "Verification Agent foundation is ready.",
    "coordinator": "Coordinator Agent foundation is ready.",
}


@pytest.mark.parametrize(("agent", "message"), AGENTS.items())
def test_request_routes_to_selected_agent(agent: str, message: str):
    response = client.post(
        "/agents/run",
        json={"agent": agent, "payload": {"ignored_by_stub": True}},
    )

    assert response.status_code == 200
    body = response.json()
    assert body["agent"] == agent
    assert body["status"] == "completed"
    assert body["output"] == {"stub": True, "message": message}
    assert body["trace"] == ["request_received", f"routed:{agent}", f"executed:{agent}"]
    assert all(
        other_agent not in " ".join(body["trace"])
        for other_agent in AGENTS
        if other_agent != agent
    )


def test_invalid_agent_returns_validation_error():
    response = client.post(
        "/agents/run",
        json={"agent": "reader", "payload": {}},
    )

    assert response.status_code == 422


def test_each_request_generates_a_new_agent_run_id():
    first = client.post("/agents/run", json={"agent": "matching", "payload": {}})
    second = client.post("/agents/run", json={"agent": "matching", "payload": {}})

    first_id = UUID(first.json()["agent_run_id"])
    second_id = UUID(second.json()["agent_run_id"])
    assert first_id != second_id


def test_unexpected_graph_failure_returns_safe_error(monkeypatch: pytest.MonkeyPatch):
    def fail_safely(_state):
        raise RuntimeError("sensitive internal detail")

    monkeypatch.setattr(main, "agent_graph", SimpleNamespace(invoke=fail_safely))
    response = client.post("/agents/run", json={"agent": "coordinator", "payload": {}})

    assert response.status_code == 500
    assert response.json() == {"detail": "Agent workflow failed safely."}
    assert "sensitive internal detail" not in response.text
