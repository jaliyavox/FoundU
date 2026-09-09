"""Description Parsing Agent node foundation."""

from app.agents.state import AgentState


def description_parser_node(state: AgentState) -> AgentState:
    """Return a deterministic stub until description parsing is implemented."""
    return {
        "output": {
            "stub": True,
            "message": "Description Parsing Agent foundation is ready.",
        },
        "trace": [*state["trace"], "executed:description_parser"],
    }
