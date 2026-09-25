"""Safe in-process LangGraph checkpoints for FoundU's completed single-pass agent runs."""

from typing import Any
from uuid import UUID

from langgraph.checkpoint.memory import InMemorySaver

from app.agents.models import AgentName

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
    """Create one application-owned, non-durable in-memory checkpointer."""
    return SafeInMemorySaver()


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
