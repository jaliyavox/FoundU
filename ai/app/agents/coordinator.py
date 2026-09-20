"""Coordinator Agent node foundation."""

from app.agents.models import AgentName
from app.agents.plans import PlanValidationError, build_coordinator_plan, validate_agent_plan
from app.agents.state import AgentState


def coordinator_node(state: AgentState) -> AgentState:
    """Return a deterministic stub until coordination logic is implemented."""
    plan = build_coordinator_plan()
    try:
        validate_agent_plan(plan, state.get("requested_agent", AgentName.COORDINATOR))
    except PlanValidationError:
        return {
            "output": {"stub": True, "message": "Coordinator plan is unavailable."},
            "trace": [*state["trace"], "plan:rejected"],
            "plan": plan,
        }
    return {
        "output": {
            "stub": True,
            "message": "Coordinator Agent foundation is ready.",
        },
        "trace": [*state["trace"], "executed:coordinator"],
        "plan": plan,
    }
