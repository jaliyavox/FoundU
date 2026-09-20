import pytest
from fastapi.testclient import TestClient

from app import main
from app.service_auth import SERVICE_KEY_HEADER

SERVICE_KEY = "service-auth-test-key-0123456789abcdef"
WRONG_KEY = "distinctive-wrong-secret-that-must-not-leak"
client = TestClient(main.app)


@pytest.fixture(autouse=True)
def configured_service_key(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("AI_SERVICE_KEY", SERVICE_KEY)


def _matching_request() -> dict[str, object]:
    return {"agent": "matching", "payload": {}}


def test_health_is_public() -> None:
    response = client.get("/health")

    assert response.status_code == 200


@pytest.mark.parametrize("path", ["/agents/run", "/agents/parse-description"])
def test_protected_routes_reject_missing_service_key(path: str) -> None:
    payload = _matching_request() if path == "/agents/run" else {"description": "Blue backpack"}

    response = client.post(path, json=payload)

    assert response.status_code == 401
    assert response.json() == {"detail": "Unauthorized service request."}


def test_protected_route_rejects_blank_service_key() -> None:
    response = client.post(
        "/agents/run", json=_matching_request(), headers={SERVICE_KEY_HEADER: "   "}
    )

    assert response.status_code == 401
    assert response.json() == {"detail": "Unauthorized service request."}


def test_wrong_service_key_is_not_reflected() -> None:
    response = client.post(
        "/agents/run", json=_matching_request(), headers={SERVICE_KEY_HEADER: WRONG_KEY}
    )

    assert response.status_code == 401
    assert response.json() == {"detail": "Unauthorized service request."}
    assert WRONG_KEY not in response.text


def test_correct_service_key_allows_existing_agent_behavior_without_key_leakage() -> None:
    response = client.post(
        "/agents/run", json=_matching_request(), headers={SERVICE_KEY_HEADER: SERVICE_KEY}
    )

    assert response.status_code == 200
    assert response.json()["agent"] == "matching"
    assert SERVICE_KEY not in response.text
    assert SERVICE_KEY not in " ".join(response.json()["trace"])


@pytest.mark.parametrize("injected_field", ["service_key", "X-FoundU-Service-Key"])
def test_payload_fields_cannot_authenticate_service_request(injected_field: str) -> None:
    request = _matching_request()
    request["payload"] = {injected_field: SERVICE_KEY}

    response = client.post("/agents/run", json=request)

    assert response.status_code == 401
    assert SERVICE_KEY not in response.text


@pytest.mark.parametrize(
    "configured_key",
    [
        "",
        "too-short",
        "replace-with-strong-random-secret",
        "distinctive-control-secret-0123456789\r",
        "distinctive-control-secret-0123456789\n",
    ],
)
def test_invalid_server_configuration_fails_closed(
    monkeypatch: pytest.MonkeyPatch, configured_key: str
) -> None:
    monkeypatch.setenv("AI_SERVICE_KEY", configured_key)

    response = client.post(
        "/agents/run", json=_matching_request(), headers={SERVICE_KEY_HEADER: SERVICE_KEY}
    )

    assert response.status_code == 503
    assert response.json() == {"detail": "Service authentication is unavailable."}
    if configured_key:
        assert configured_key not in response.text
