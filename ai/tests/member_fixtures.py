"""Shared fixtures for the per-member test folders (SE3110 Assignment 2).

Each member owns a folder named after them, holding their unit tests and one file that tests
the agent of their business component end to end through the real `/agents/run` endpoint:

  member1_jaliya/test_member1_jaliya_support_agent.py           Support agent
  member2_ranasinghe/test_member2_ranasinghe_parser_agent.py     Description parser
  member3_uthpala/test_member3_uthpala_matching_agent.py         Matching agent
  member4_braveena/test_member4_braveena_verification_agent.py   Verification and Coordinator

Run one member's folder on its own, for example:

  cd ai && .venv/bin/python -m pytest tests/member1_jaliya -v

The default model is the deterministic `fake` provider, so the agents finish on their
rule-based paths and every run gives the same result. Set LLM_PROVIDER=groq (with LLM_MODEL
and LLM_API_KEY) to run the same cases against the live model.
"""

from __future__ import annotations

import os

import pytest
from fastapi.testclient import TestClient

os.environ.setdefault("LLM_PROVIDER", "fake")
os.environ.setdefault("LLM_MODEL", "fake-structured-v1")

from app import main  # noqa: E402
from app.service_auth import SERVICE_KEY_HEADER  # noqa: E402

SERVICE_KEY = "member-tests-service-key-0123456789"


@pytest.fixture(scope="module")
def service():
    mp = pytest.MonkeyPatch()
    mp.setenv("AI_SERVICE_KEY", SERVICE_KEY)
    mp.setenv("WORKFLOW_STATE_STORE", "memory")
    with TestClient(main.app) as client:
        yield client
    mp.undo()


@pytest.fixture(scope="module")
def run_agent(service):
    """POST one request to /agents/run and return the whole response body."""

    def run(agent: str, payload: dict, expect: int = 200, key: str | None = SERVICE_KEY) -> dict:
        headers = {SERVICE_KEY_HEADER: key} if key else {}
        response = service.post(
            "/agents/run", json={"agent": agent, "payload": payload}, headers=headers
        )
        assert response.status_code == expect, response.text[:300]
        return response.json()

    return run
