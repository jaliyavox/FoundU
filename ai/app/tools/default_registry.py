"""Composition for FoundU's local deterministic Phase 4 tool set."""

from app.tools import adapters
from app.tools.models import (
    CreateVerificationChallengeInput,
    CreateVerificationChallengeOutput,
    PauseForApprovalInput,
    PauseForApprovalOutput,
    ReadWorkflowStateInput,
    ReadWorkflowStateOutput,
    RecordVerificationResultInput,
    RecordVerificationResultOutput,
    RecordWorkflowStepInput,
    RecordWorkflowStepOutput,
    ReportLookupInput,
    ReportLookupOutput,
    SaveMatchCandidateInput,
    SaveMatchCandidateOutput,
    SearchActiveLostReportsInput,
    SearchActiveLostReportsOutput,
)
from app.tools.registry import ToolDefinition, ToolRegistry


def create_default_tool_registry() -> ToolRegistry:
    """Create all currently declared local adapters and verify permission coverage."""
    registry = ToolRegistry()
    definitions = (
        ToolDefinition(
            "searchActiveLostReports",
            "Search supplied active-lost-report context without persistence.",
            SearchActiveLostReportsInput,
            SearchActiveLostReportsOutput,
            adapters.search_active_lost_reports,
        ),
        ToolDefinition(
            "getLostReportDetails",
            "Retrieve a supplied lost-report identifier without persistence.",
            ReportLookupInput,
            ReportLookupOutput,
            adapters.lookup_report,
        ),
        ToolDefinition(
            "getFoundReportDetails",
            "Retrieve a supplied found-report identifier without persistence.",
            ReportLookupInput,
            ReportLookupOutput,
            adapters.lookup_report,
        ),
        ToolDefinition(
            "saveMatchCandidate",
            "Record a local deterministic match-candidate adapter result.",
            SaveMatchCandidateInput,
            SaveMatchCandidateOutput,
            adapters.save_match_candidate,
        ),
        ToolDefinition(
            "createVerificationChallenge",
            "Create a local safe verification-challenge reference.",
            CreateVerificationChallengeInput,
            CreateVerificationChallengeOutput,
            adapters.create_verification_challenge,
        ),
        ToolDefinition(
            "recordVerificationResult",
            "Record a non-authoritative verification recommendation locally.",
            RecordVerificationResultInput,
            RecordVerificationResultOutput,
            adapters.record_verification_result,
        ),
        ToolDefinition(
            "pauseForApproval",
            "Mark a local coordinator workflow as awaiting human approval.",
            PauseForApprovalInput,
            PauseForApprovalOutput,
            adapters.pause_for_approval,
        ),
        ToolDefinition(
            "readWorkflowState",
            "Read local coordinator workflow state.",
            ReadWorkflowStateInput,
            ReadWorkflowStateOutput,
            adapters.read_workflow_state,
        ),
        ToolDefinition(
            "recordWorkflowStep",
            "Record a local coordinator workflow-step adapter result.",
            RecordWorkflowStepInput,
            RecordWorkflowStepOutput,
            adapters.record_workflow_step,
        ),
    )
    for definition in definitions:
        registry.register(definition)
    registry.validate_declared_tools()
    return registry
