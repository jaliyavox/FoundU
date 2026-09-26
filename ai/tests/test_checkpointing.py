"""Tests for safe in-memory LangGraph checkpoint continuity."""

import os
from concurrent.futures import ThreadPoolExecutor
from functools import partial
from uuid import uuid4

import pytest

from app.agents.checkpoint import (
    CheckpointStateError,
    InMemoryWorkflowStateStore,
    PostgresWorkflowStateStore,
    WorkflowStateConfigurationError,
    checkpoint_config,
    create_checkpointer,
    create_workflow_state_store,
    load_checkpointed_state,
)
from app.agents.description_parser import description_parser_node
from app.agents.graph import build_agent_graph
from app.agents.matching import matching_node
from app.agents.models import AGENT_PERMISSIONS, AgentName, AgentRunRequest, PlanActionType
from app.agents.plans import build_description_parser_plan
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


def test_postgres_mode_requires_a_database_url_without_exposing_configuration(
    monkeypatch: pytest.MonkeyPatch,
):
    monkeypatch.delenv("WORKFLOW_STATE_STORE", raising=False)
    monkeypatch.delenv("WORKFLOW_DATABASE_URL", raising=False)

    with pytest.raises(WorkflowStateConfigurationError, match="not configured") as error:
        create_workflow_state_store(store_type="postgres")

    assert "postgresql://" not in str(error.value)


def test_memory_store_requires_explicit_selection():
    assert isinstance(create_workflow_state_store(store_type="memory"), InMemoryWorkflowStateStore)


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


def test_stable_workflow_id_can_be_reloaded_by_a_recreated_graph_without_replaying_steps():
    workflow_id = uuid4()
    saver = create_checkpointer()
    first_graph = build_agent_graph(
        partial(description_parser_node, llm_client=None), checkpointer=saver
    )
    state = create_initial_state(
        AgentRunRequest(
            agent=AgentName.DESCRIPTION_PARSER,
            workflow_id=workflow_id,
            payload={"description": "blue backpack"},
        )
    )
    first_graph.invoke(state, checkpoint_config(workflow_id))

    # Simulates a graph reconstruction. Durable process-restart recovery is exercised through the
    # repository reconstruction test below; this in-memory saver is intentionally test-only.
    recreated_graph = build_agent_graph(
        partial(description_parser_node, llm_client=None), checkpointer=saver
    )
    restored = load_checkpointed_state(
        recreated_graph, workflow_id, AgentName.DESCRIPTION_PARSER
    )

    assert restored["agent_run_id"] == workflow_id
    assert restored["plan"].agent is AgentName.DESCRIPTION_PARSER
    assert restored["output"]


def test_durable_workflow_record_survives_repository_reconstruction_without_private_input():
    workflow_id = uuid4()
    shared_records: dict[str, dict[str, object]] = {}
    first_store = InMemoryWorkflowStateStore(shared_records)
    secret = "PRIVATE-OWNERSHIP-EVIDENCE-DO-NOT-PERSIST"
    state = {
        "agent_run_id": workflow_id,
        "requested_agent": AgentName.DESCRIPTION_PARSER,
        "payload": {"description": secret, "prompt": secret},
        "plan": build_description_parser_plan(),
        "trace": ["request_received", "executed:description_parser"],
        "tool_results": [{"tool": "safe_tool", "status": "completed"}],
        "validation_results": {"status": "passed"},
        "approval_required": True,
        "approval_status": "pending",
        "output": {"item_type": "Backpack"},
        "final_outcome": {"item_type": "Backpack"},
        "error": "safe_failure_code",
    }

    assert first_store.create(workflow_id, AgentName.DESCRIPTION_PARSER, state)
    first_store.update(workflow_id, AgentName.DESCRIPTION_PARSER, "waiting_for_approval", state)

    recreated_store = InMemoryWorkflowStateStore(shared_records)
    restored = recreated_store.load(workflow_id, AgentName.DESCRIPTION_PARSER)

    assert restored["status"] == "waiting_for_approval"
    assert restored["state"]["plan"].agent is AgentName.DESCRIPTION_PARSER
    assert restored["state"]["tool_results"] == [{"tool": "safe_tool", "status": "completed"}]
    assert restored["state"]["validation_results"] == {"status": "passed"}
    assert restored["state"]["approval_required"] is True
    assert restored["state"]["approval_status"] == "pending"
    assert restored["state"]["error"] == "safe_failure_code"
    assert restored["state"]["final_outcome"] == {"item_type": "Backpack"}
    assert "payload" not in restored["state"]
    assert secret not in str(shared_records)


@pytest.mark.skipif(
    not os.getenv("TEST_WORKFLOW_DATABASE_URL"),
    reason="Set TEST_WORKFLOW_DATABASE_URL to run PostgreSQL workflow recovery coverage.",
)
def test_postgres_workflow_recovery_survives_repository_reconstruction_and_blocks_duplicates():
    database_url = os.environ["TEST_WORKFLOW_DATABASE_URL"]
    workflow_id = uuid4()
    concurrent_workflow_id = uuid4()
    state = {
        "agent_run_id": workflow_id,
        "requested_agent": AgentName.DESCRIPTION_PARSER,
        "objective": "description_parser:execute_request",
        "plan": build_description_parser_plan(),
        "completed_step_ids": ["inspect-input"],
        "trace": ["request_received", "executed:description_parser"],
        "tool_results": [{"tool": "safe_tool", "status": "completed"}],
        "validation_results": {"status": "passed"},
        "approval_required": True,
        "approval_status": "pending",
        "output": {"item_type": "Backpack"},
    }
    try:
        first_store = PostgresWorkflowStateStore(database_url)
        assert first_store.create(workflow_id, AgentName.DESCRIPTION_PARSER, state)
        first_store.update(
            workflow_id, AgentName.DESCRIPTION_PARSER, "waiting_for_approval", state
        )

        del first_store
        restored = PostgresWorkflowStateStore(database_url).load(
            workflow_id, AgentName.DESCRIPTION_PARSER
        )
        assert restored["status"] == "waiting_for_approval"
        assert restored["state"]["objective"] == "description_parser:execute_request"
        assert restored["state"]["plan"].agent is AgentName.DESCRIPTION_PARSER
        assert restored["state"]["completed_step_ids"] == ["inspect-input"]
        assert restored["state"]["tool_results"] == [{"tool": "safe_tool", "status": "completed"}]
        assert restored["state"]["validation_results"] == {"status": "passed"}
        assert restored["state"]["approval_status"] == "pending"

        concurrent_state = {**state, "agent_run_id": concurrent_workflow_id}
        with ThreadPoolExecutor(max_workers=2) as executor:
            duplicate_creates = list(
                executor.map(
                    lambda _: PostgresWorkflowStateStore(database_url).create(
                        concurrent_workflow_id, AgentName.DESCRIPTION_PARSER, concurrent_state
                    ),
                    range(2),
                )
            )
        assert duplicate_creates.count(True) == 1
        assert duplicate_creates.count(False) == 1

        final_state = {**state, "approval_required": False, "approval_status": "not_required"}
        final_store = PostgresWorkflowStateStore(database_url)
        final_store.update(workflow_id, AgentName.DESCRIPTION_PARSER, "completed", final_state)

        final_record = PostgresWorkflowStateStore(database_url).load(
            workflow_id, AgentName.DESCRIPTION_PARSER
        )
        assert final_record["status"] == "completed"
        assert final_record["state"]["approval_status"] == "not_required"
    finally:
        import psycopg

        with psycopg.connect(database_url) as connection, connection.cursor() as cursor:
            cursor.execute(
                "DELETE FROM ai_workflow_states WHERE workflow_id IN (%s, %s)",
                (workflow_id, concurrent_workflow_id),
            )
