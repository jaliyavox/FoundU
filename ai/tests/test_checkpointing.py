"""Tests for safe in-memory LangGraph checkpoint continuity."""

from functools import partial
from uuid import uuid4

import pytest

from app.agents.checkpoint import (
    CheckpointStateError,
    checkpoint_config,
    create_checkpointer,
    load_checkpointed_state,
)
from app.agents.description_parser import description_parser_node
from app.agents.graph import build_agent_graph
from app.agents.matching import matching_node
from app.agents.models import AGENT_PERMISSIONS, AgentName, AgentRunRequest, PlanActionType
from app.agents.state import create_initial_state
from app.tools.default_registry import create_default_tool_registry


def _graph_with_checkpointer():
    saver = create_checkpointer()
    graph = build_agent_graph(
        partial(description_parser_node, llm_client=None),
        partial(matching_node, tool_registry=create_default_tool_registry()),
        saver,
    )
    return graph, saver


def _invoke(graph, agent: AgentName, payload: dict[str, object]):
    state = create_initial_state(AgentRunRequest(agent=agent, payload=payload))
    graph.invoke(state, checkpoint_config(state["agent_run_id"]))
    return state


def test_graph_is_compiled_with_application_owned_in_memory_checkpointer():
    graph, saver = _graph_with_checkpointer()

    assert graph.checkpointer is saver


def test_first_run_checkpoints_safe_state_and_same_thread_loads_its_checkpointed_plan():
    graph, _ = _graph_with_checkpointer()
    secret = "PRIVATE-REPORT-DESCRIPTION-DO-NOT-CHECKPOINT"
    state = _invoke(
        graph,
        AgentName.DESCRIPTION_PARSER,
        {"description": secret, "prompt": secret, "reasoning": secret},
    )

    loaded_state = load_checkpointed_state(
        graph, state["agent_run_id"], AgentName.DESCRIPTION_PARSER
    )

    assert loaded_state["agent_run_id"] == state["agent_run_id"]
    assert loaded_state["requested_agent"] is AgentName.DESCRIPTION_PARSER
    assert loaded_state["plan"].agent is AgentName.DESCRIPTION_PARSER
    assert "payload" not in loaded_state
    assert secret not in str(loaded_state)


def test_checkpoint_threads_are_isolated_and_payload_thread_spoofing_is_ignored():
    graph, _ = _graph_with_checkpointer()
    first = _invoke(
        graph,
        AgentName.COORDINATOR,
        {"thread_id": "spoofed-thread", "description": "PRIVATE-A"},
    )
    second = _invoke(graph, AgentName.DESCRIPTION_PARSER, {"description": "PRIVATE-B"})

    first_state = load_checkpointed_state(graph, first["agent_run_id"], AgentName.COORDINATOR)
    second_state = load_checkpointed_state(
        graph, second["agent_run_id"], AgentName.DESCRIPTION_PARSER
    )

    assert first_state["agent_run_id"] != second_state["agent_run_id"]
    assert first_state["requested_agent"] is AgentName.COORDINATOR
    assert second_state["requested_agent"] is AgentName.DESCRIPTION_PARSER
    assert "PRIVATE-A" not in str(first_state)
    assert "PRIVATE-B" not in str(second_state)


def test_unknown_or_cross_agent_checkpoint_continuity_fails_safely():
    graph, _ = _graph_with_checkpointer()
    state = _invoke(graph, AgentName.COORDINATOR, {})

    with pytest.raises(CheckpointStateError) as unknown:
        load_checkpointed_state(graph, uuid4(), AgentName.COORDINATOR)
    with pytest.raises(CheckpointStateError) as mismatch:
        load_checkpointed_state(graph, state["agent_run_id"], AgentName.VERIFICATION)

    assert str(unknown.value) == "Checkpoint continuity is unavailable."
    assert str(mismatch.value) == "Checkpoint continuity is unavailable."
    assert unknown.value.__cause__ is None
    assert unknown.value.__context__ is None


def test_matching_checkpoint_keeps_plan_and_registry_execution_boundary():
    graph, _ = _graph_with_checkpointer()
    state = _invoke(
        graph,
        AgentName.MATCHING,
        {
            "operation": "match_reports",
            "lost_report": {
                "report_id": "lost-1",
                "item_type": "Backpack",
                "primary_color": "Blue",
            },
            "found_report": {
                "report_id": "found-1",
                "item_type": "Backpack",
                "primary_color": "Blue",
            },
        },
    )
    loaded_state = load_checkpointed_state(graph, state["agent_run_id"], AgentName.MATCHING)

    assert loaded_state["output"] == {"recommendation": "match_candidate", "score": 1.0}
    assert [step.tool_name for step in loaded_state["plan"].steps if step.tool_name] == [
        "getLostReportDetails",
        "getFoundReportDetails",
    ]
    assert "tool:success:getLostReportDetails" in loaded_state["trace"]


def test_loaded_verification_checkpoint_keeps_non_authoritative_plan_and_permissions():
    graph, _ = _graph_with_checkpointer()
    state = _invoke(
        graph,
        AgentName.VERIFICATION,
        {
            "operation": "generate_questions",
            "claim_id": "claim-1",
            "private_verification_details": {"distinctive_mark": "PRIVATE-EVIDENCE"},
        },
    )
    loaded_state = load_checkpointed_state(graph, state["agent_run_id"], AgentName.VERIFICATION)

    assert AGENT_PERMISSIONS[AgentName.VERIFICATION].has_approval_permission is False
    assert all(
        step.action_type is not PlanActionType.CALL_TOOL for step in loaded_state["plan"].steps
    )
    assert "PRIVATE-EVIDENCE" not in str(loaded_state)


def test_coordinator_checkpoint_excludes_untrusted_sensitive_workflow_payload():
    graph, _ = _graph_with_checkpointer()
    secret = "PRIVATE-VERIFICATION-ANSWER-DO-NOT-CHECKPOINT"
    state = _invoke(
        graph,
        AgentName.COORDINATOR,
        {
            "workflow_id": "claim-1",
            "workflow_type": "claim_verification",
            "claim_status": "ManualReviewRequired",
            "verification_recommendation": "manual_review",
            "decision_status": "no_decision",
            "notification_state": "not_required",
            "private_verification_details": secret,
            "answers": [secret],
            "prompt": secret,
        },
    )
    loaded_state = load_checkpointed_state(graph, state["agent_run_id"], AgentName.COORDINATOR)

    assert loaded_state["output"]["safe_reason_code"] == "inconsistent_workflow_state"
    assert "payload" not in loaded_state
    assert secret not in str(loaded_state)


def test_loading_checkpointed_state_does_not_replay_a_completed_graph_node():
    calls = 0

    def counted_description_node(state):
        nonlocal calls
        calls += 1
        return {
            "output": {"safe": True},
            "trace": [*state["trace"], "executed:description_parser"],
        }

    saver = create_checkpointer()
    graph = build_agent_graph(counted_description_node, checkpointer=saver)
    state = _invoke(graph, AgentName.DESCRIPTION_PARSER, {"description": "ordinary data"})

    assert calls == 1
    loaded_state = load_checkpointed_state(
        graph, state["agent_run_id"], AgentName.DESCRIPTION_PARSER
    )

    assert loaded_state["output"] == {"safe": True}
    assert calls == 1
