"""Sanitized LangGraph checkpoints and durable PostgreSQL workflow state."""

import json
import os
from typing import Any
from uuid import UUID

from langgraph.checkpoint.memory import InMemorySaver
from pydantic import BaseModel

from app.agents.models import AgentName, AgentPlan, PlanActionType, PlanPurpose

_EXCLUDED_CHECKPOINT_KEYS = frozenset(
    {
        "payload",
        "correlation_id",
        "description",
        "private_verification_details",
        "prompt",
        "system_instruction",
        "messages",
        "response",
        "model_response",
        "reasoning",
        "scratchpad",
    }
)
WORKFLOW_STATE_STORE_ENVIRONMENT_VARIABLE = "WORKFLOW_STATE_STORE"
WORKFLOW_DATABASE_URL_ENVIRONMENT_VARIABLE = "WORKFLOW_DATABASE_URL"
WORKFLOW_STATE_STORE_POSTGRES = "postgres"
WORKFLOW_STATE_STORE_MEMORY = "memory"


def _sanitize_checkpoint_value(value: Any) -> Any:
    """Remove untrusted input and sensitive-content fields before checkpoint serialization."""
    if isinstance(value, dict):
        return {
            key: _sanitize_checkpoint_value(child)
            for key, child in value.items()
            if key not in _EXCLUDED_CHECKPOINT_KEYS
        }
    if isinstance(value, list):
        return [_sanitize_checkpoint_value(child) for child in value]
    if isinstance(value, tuple):
        return tuple(_sanitize_checkpoint_value(child) for child in value)
    return value


class SafeInMemorySaver(InMemorySaver):
    """LangGraph's supported in-memory saver with a minimum safe-state write boundary."""

    def put(self, config, checkpoint, metadata, new_versions):
        safe_checkpoint = checkpoint.copy()
        channel_values = safe_checkpoint.get("channel_values", {})
        safe_checkpoint["channel_values"] = _sanitize_checkpoint_value(channel_values)
        return super().put(
            config,
            safe_checkpoint,
            _sanitize_checkpoint_value(metadata),
            new_versions,
        )

    def put_writes(self, config, writes, task_id, task_path=""):
        safe_writes = [
            (channel, _sanitize_checkpoint_value(value))
            for channel, value in writes
            if channel not in _EXCLUDED_CHECKPOINT_KEYS
        ]
        return super().put_writes(config, safe_writes, task_id, task_path)


class CheckpointStateError(Exception):
    """Safe failure for unknown, invalid, or cross-agent checkpoint continuity requests."""

    def __init__(self) -> None:
        super().__init__("Checkpoint continuity is unavailable.")


def create_checkpointer() -> SafeInMemorySaver:
    """Create the graph-local saver; durable operational state lives in WorkflowStateStore."""
    return SafeInMemorySaver()


class WorkflowStateStoreError(Exception):
    """Safe failure for unavailable or conflicting durable workflow state."""

    def __init__(self) -> None:
        super().__init__("Workflow state is unavailable.")


class WorkflowStateConfigurationError(RuntimeError):
    """Startup failure that never includes connection details or credentials."""

    def __init__(self, message: str) -> None:
        super().__init__(message)


class InMemoryWorkflowStateStore:
    """Non-durable local/test implementation of the workflow-state repository."""

    def __init__(self, records: dict[str, dict[str, Any]] | None = None) -> None:
        self.records = records if records is not None else {}

    def create(self, workflow_id: UUID, agent: AgentName, state: dict[str, Any]) -> bool:
        key = str(workflow_id)
        if key in self.records:
            return False
        self.records[key] = {
            "agent": agent.value,
            "status": "created",
            "state": _safe_json(state),
        }
        return True

    def update(
        self, workflow_id: UUID, agent: AgentName, status: str, state: dict[str, Any]
    ) -> None:
        key = str(workflow_id)
        existing = self.records.get(key)
        if existing is None or existing["agent"] != agent.value:
            raise WorkflowStateStoreError()
        existing.update({"status": status, "state": _safe_json(state)})

    def transition(
        self,
        workflow_id: UUID,
        agent: AgentName,
        expected_status: str,
        status: str,
        state: dict[str, Any],
    ) -> bool:
        key = str(workflow_id)
        existing = self.records.get(key)
        if existing is None or existing["agent"] != agent.value:
            raise WorkflowStateStoreError()
        if existing["status"] != expected_status:
            return False
        existing.update({"status": status, "state": _safe_json(state)})
        return True

    def load(self, workflow_id: UUID, agent: AgentName) -> dict[str, Any]:
        record = self.records.get(str(workflow_id))
        if record is None or record["agent"] != agent.value:
            raise WorkflowStateStoreError()
        return {"status": record["status"], "state": _rehydrate_state(record["state"])}


class PostgresWorkflowStateStore:
    """PostgreSQL system of record for sanitized FastAPI workflow lifecycle state."""

    _CREATE_TABLE = """
        CREATE TABLE IF NOT EXISTS ai_workflow_states (
            workflow_id UUID PRIMARY KEY,
            agent VARCHAR(64) NOT NULL,
            status VARCHAR(32) NOT NULL,
            state_json JSONB NOT NULL,
            version INTEGER NOT NULL DEFAULT 1,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        )
    """
    _CREATE_INDEX = """
        CREATE INDEX IF NOT EXISTS ix_ai_workflow_states_agent_updated
            ON ai_workflow_states (agent, updated_at DESC)
    """
    _EXPECTED_COLUMNS = {
        "workflow_id": "uuid",
        "agent": "character varying",
        "status": "character varying",
        "state_json": "jsonb",
        "version": "integer",
        "created_at": "timestamp with time zone",
        "updated_at": "timestamp with time zone",
    }

    def __init__(self, database_url: str) -> None:
        try:
            import psycopg
            from psycopg.types.json import Jsonb
        except ImportError as exc:  # pragma: no cover - dependency installation configuration.
            raise RuntimeError("PostgreSQL workflow persistence support is not installed.") from exc
        self._psycopg = psycopg
        self._jsonb = Jsonb
        self._database_url = database_url
        try:
            self._ensure_schema()
        except WorkflowStateConfigurationError:
            raise
        except Exception:
            raise WorkflowStateConfigurationError(
                "PostgreSQL workflow persistence is unavailable."
            ) from None

    def _connect(self):
        return self._psycopg.connect(self._database_url)

    def _ensure_schema(self) -> None:
        with self._connect() as connection, connection.cursor() as cursor:
            cursor.execute(self._CREATE_TABLE)
            cursor.execute(self._CREATE_INDEX)
            cursor.execute(
                """SELECT column_name, data_type FROM information_schema.columns
                   WHERE table_schema = current_schema() AND table_name = 'ai_workflow_states'"""
            )
            columns = dict(cursor.fetchall())
        if any(
            columns.get(name) != data_type for name, data_type in self._EXPECTED_COLUMNS.items()
        ):
            raise WorkflowStateConfigurationError("Workflow persistence schema is incompatible.")

    def create(self, workflow_id: UUID, agent: AgentName, state: dict[str, Any]) -> bool:
        try:
            with self._connect() as connection, connection.cursor() as cursor:
                cursor.execute(
                    """INSERT INTO ai_workflow_states (workflow_id, agent, status, state_json)
                       VALUES (%s, %s, 'created', %s) ON CONFLICT (workflow_id) DO NOTHING""",
                    (workflow_id, agent.value, self._jsonb(_safe_json(state))),
                )
                return cursor.rowcount == 1
        except Exception:
            raise WorkflowStateStoreError() from None

    def update(
        self, workflow_id: UUID, agent: AgentName, status: str, state: dict[str, Any]
    ) -> None:
        try:
            with self._connect() as connection, connection.cursor() as cursor:
                cursor.execute(
                    """UPDATE ai_workflow_states
                       SET status = %s, state_json = %s, version = version + 1, updated_at = NOW()
                       WHERE workflow_id = %s AND agent = %s""",
                    (status, self._jsonb(_safe_json(state)), workflow_id, agent.value),
                )
                if cursor.rowcount != 1:
                    raise WorkflowStateStoreError()
        except WorkflowStateStoreError:
            raise
        except Exception:
            raise WorkflowStateStoreError() from None

    def transition(
        self,
        workflow_id: UUID,
        agent: AgentName,
        expected_status: str,
        status: str,
        state: dict[str, Any],
    ) -> bool:
        """Atomically move one workflow state only when its prior lifecycle state matches."""
        try:
            with self._connect() as connection, connection.cursor() as cursor:
                cursor.execute(
                    """UPDATE ai_workflow_states
                       SET status = %s, state_json = %s, version = version + 1, updated_at = NOW()
                       WHERE workflow_id = %s AND agent = %s AND status = %s""",
                    (
                        status,
                        self._jsonb(_safe_json(state)),
                        workflow_id,
                        agent.value,
                        expected_status,
                    ),
                )
                return cursor.rowcount == 1
        except Exception:
            raise WorkflowStateStoreError() from None

    def load(self, workflow_id: UUID, agent: AgentName) -> dict[str, Any]:
        try:
            with self._connect() as connection, connection.cursor() as cursor:
                cursor.execute(
                    """SELECT status, state_json FROM ai_workflow_states
                       WHERE workflow_id = %s AND agent = %s""",
                    (workflow_id, agent.value),
                )
                row = cursor.fetchone()
        except Exception:
            raise WorkflowStateStoreError() from None
        if row is None:
            raise WorkflowStateStoreError()
        return {"status": row[0], "state": _rehydrate_state(row[1])}


def _safe_json(value: Any) -> Any:
    """Sanitize and serialize only JSON-compatible, safe operational data."""
    safe_value = _sanitize_checkpoint_value(value)
    return json.loads(json.dumps(safe_value, default=_json_default))


def _json_default(value: Any) -> Any:
    if isinstance(value, BaseModel):
        return value.model_dump(mode="json")
    if isinstance(value, UUID):
        return str(value)
    return getattr(value, "value", str(value))


def _rehydrate_state(value: dict[str, Any]) -> dict[str, Any]:
    """Restore trusted enum/model types after JSONB storage without restoring private input."""
    state = _safe_json(value)
    requested_agent = state.get("requested_agent")
    if isinstance(requested_agent, str):
        state["requested_agent"] = AgentName(requested_agent)
    raw_plan = state.get("plan")
    if not isinstance(raw_plan, dict):
        return state
    plan = raw_plan.copy()
    if isinstance(plan.get("agent"), str):
        plan["agent"] = AgentName(plan["agent"])
    restored_steps = []
    for raw_step in plan.get("steps", []):
        step = raw_step.copy()
        if isinstance(step.get("action_type"), str):
            step["action_type"] = PlanActionType(step["action_type"])
        if isinstance(step.get("purpose"), str):
            step["purpose"] = PlanPurpose(step["purpose"])
        restored_steps.append(step)
    plan["steps"] = restored_steps
    state["plan"] = AgentPlan.model_validate(plan)
    return state


def create_workflow_state_store(
    database_url: str | None = None, store_type: str | None = None
) -> Any:
    """Create an explicitly configured workflow store.

    PostgreSQL is the default and fails startup if its URL is absent. ``memory`` is intentionally
    opt-in for isolated tests and explicitly non-durable local development.
    """
    selected_store_type = (
        store_type
        or os.getenv(
            WORKFLOW_STATE_STORE_ENVIRONMENT_VARIABLE, WORKFLOW_STATE_STORE_POSTGRES
        )
    ).strip().lower()
    database_url = database_url or os.getenv(WORKFLOW_DATABASE_URL_ENVIRONMENT_VARIABLE)
    if selected_store_type == WORKFLOW_STATE_STORE_MEMORY:
        return InMemoryWorkflowStateStore()
    if selected_store_type != WORKFLOW_STATE_STORE_POSTGRES:
        raise WorkflowStateConfigurationError("Workflow state store configuration is invalid.")
    if not database_url or not database_url.strip():
        raise WorkflowStateConfigurationError("PostgreSQL workflow persistence is not configured.")
    return PostgresWorkflowStateStore(database_url)


def checkpoint_config(agent_run_id: UUID) -> dict[str, dict[str, str]]:
    """Build LangGraph configuration from a server-generated run identifier only."""
    return {"configurable": {"thread_id": str(agent_run_id)}}


def load_checkpointed_state(graph, agent_run_id: UUID, expected_agent: AgentName) -> dict[str, Any]:
    """Read a completed thread snapshot without resuming or replaying graph execution."""
    try:
        snapshot = graph.get_state(checkpoint_config(agent_run_id))
        values = dict(snapshot.values)
        if not values or values.get("requested_agent") is not expected_agent:
            raise ValueError
    except Exception:
        is_unavailable = True
    else:
        is_unavailable = False
    if is_unavailable:
        raise CheckpointStateError()
    return values
