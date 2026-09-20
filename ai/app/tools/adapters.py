"""Deterministic in-memory adapters for Phase 4; they do not access business state."""

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
    ToolExecutionContext,
)


def lookup_report(input_model: ReportLookupInput, _: ToolExecutionContext) -> ReportLookupOutput:
    return ReportLookupOutput(report_id=input_model.report_id, found=False)


def search_active_lost_reports(
    _: SearchActiveLostReportsInput, __: ToolExecutionContext
) -> SearchActiveLostReportsOutput:
    return SearchActiveLostReportsOutput(report_ids=[])


def save_match_candidate(
    _: SaveMatchCandidateInput, __: ToolExecutionContext
) -> SaveMatchCandidateOutput:
    return SaveMatchCandidateOutput(recorded=True)


def create_verification_challenge(
    input_model: CreateVerificationChallengeInput, _: ToolExecutionContext
) -> CreateVerificationChallengeOutput:
    return CreateVerificationChallengeOutput(challenge_id=f"challenge-{input_model.claim_id}")


def record_verification_result(
    _: RecordVerificationResultInput, __: ToolExecutionContext
) -> RecordVerificationResultOutput:
    return RecordVerificationResultOutput(recorded=True)


def pause_for_approval(
    _: PauseForApprovalInput, __: ToolExecutionContext
) -> PauseForApprovalOutput:
    return PauseForApprovalOutput(paused=True)


def read_workflow_state(
    _: ReadWorkflowStateInput, __: ToolExecutionContext
) -> ReadWorkflowStateOutput:
    return ReadWorkflowStateOutput(state="awaiting_human")


def record_workflow_step(
    _: RecordWorkflowStepInput, __: ToolExecutionContext
) -> RecordWorkflowStepOutput:
    return RecordWorkflowStepOutput(recorded=True)
