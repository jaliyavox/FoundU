"""Compiled LangGraph workflow for routing FoundU agent requests."""

from collections.abc import Callable
from typing import Literal

from langgraph.graph import END, START, StateGraph

from app.agents.checkpoint import SafeInMemorySaver
from app.agents.coordinator import coordinator_node
from app.agents.description_parser import description_parser_node
from app.agents.matching import matching_node
from app.agents.models import AgentName
from app.agents.state import AgentState
from app.agents.verification import verification_node

AgentNodeName = Literal["description_parser", "matching", "verification", "coordinator"]


def route_request_node(state: AgentState) -> AgentState:
    requested_agent = state["requested_agent"].value
    return {"trace": [*state["trace"], f"routed:{requested_agent}"]}


def select_agent(state: AgentState) -> AgentNodeName:
    return state["requested_agent"].value


def build_agent_graph(
    description_parser_handler: Callable[[AgentState], AgentState] = description_parser_node,
    matching_handler: Callable[[AgentState], AgentState] = matching_node,
    checkpointer: SafeInMemorySaver | None = None,
):
    """Build the routing graph with composed node dependencies."""
    graph = StateGraph(AgentState)
    graph.add_node("route_request", route_request_node)
    graph.add_node(AgentName.DESCRIPTION_PARSER.value, description_parser_handler)
    graph.add_node(AgentName.MATCHING.value, matching_handler)
    graph.add_node(AgentName.VERIFICATION.value, verification_node)
    graph.add_node(AgentName.COORDINATOR.value, coordinator_node)

    graph.add_edge(START, "route_request")
    graph.add_conditional_edges(
        "route_request",
        select_agent,
        {
            AgentName.DESCRIPTION_PARSER.value: AgentName.DESCRIPTION_PARSER.value,
            AgentName.MATCHING.value: AgentName.MATCHING.value,
            AgentName.VERIFICATION.value: AgentName.VERIFICATION.value,
            AgentName.COORDINATOR.value: AgentName.COORDINATOR.value,
        },
    )
    for agent in AgentName:
        graph.add_edge(agent.value, END)

    return graph.compile(checkpointer=checkpointer)


# Compile once at import/startup, not once per HTTP request.
agent_graph = build_agent_graph()
