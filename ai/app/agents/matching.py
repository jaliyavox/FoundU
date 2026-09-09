"""Matching Agent node foundation."""

from app.agents.state import AgentState


def matching_node(state: AgentState) -> AgentState:
    """Return a deterministic stub until matching is implemented."""
    return {
        "output": {
            "stub": True,
            "message": "Matching Agent foundation is ready.",
        },
        "trace": [*state["trace"], "executed:matching"],
    }
