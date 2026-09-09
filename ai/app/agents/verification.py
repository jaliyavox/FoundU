"""Verification Agent node foundation."""

from app.agents.state import AgentState


def verification_node(state: AgentState) -> AgentState:
    """Return a deterministic stub until ownership verification is implemented."""
    return {
        "output": {
            "stub": True,
            "message": "Verification Agent foundation is ready.",
        },
        "trace": [*state["trace"], "executed:verification"],
    }
