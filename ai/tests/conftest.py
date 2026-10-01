"""Test-only environment defaults for intentionally non-durable isolated runs."""

import pytest


@pytest.fixture(autouse=True)
def use_explicit_in_memory_workflow_store(monkeypatch: pytest.MonkeyPatch) -> None:
    """Ordinary tests use memory; PostgreSQL integration tests override this explicitly."""
    monkeypatch.setenv("WORKFLOW_STATE_STORE", "memory")
