"""Coordinator Agent node foundation."""

from app.agents.state import AgentState


def coordinator_node(state: AgentState) -> AgentState:
    """Return a deterministic stub until coordination logic is implemented."""
    return {
        "output": {
            "stub": True,
            "message": "Coordinator Agent foundation is ready.",
        },
        "trace": [*state["trace"], "executed:coordinator"],
    }
