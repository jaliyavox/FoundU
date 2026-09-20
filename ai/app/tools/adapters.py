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


class ContextReportLookupProvider:
    """Read only the minimal report context carried by a validated tool request.

    This local deterministic provider has no database connection or mutable state. A future
    application-owned provider can replace it without changing the registry tool contracts.
    """

    def lookup(self, input_model: ReportLookupInput) -> ReportLookupOutput:
        if input_model.report is None:
            return ReportLookupOutput(report_id=input_model.report_id, found=False)
        return ReportLookupOutput(
            report_id=input_model.report_id,
            found=True,
            report={
                "report_id": input_model.report.report_id,
                "item_type": input_model.report.item_type,
                "primary_color": input_model.report.primary_color,
            },
        )


def lookup_report(input_model: ReportLookupInput, _: ToolExecutionContext) -> ReportLookupOutput:
    """Registry handler for context-backed, read-only report summaries."""
    return ContextReportLookupProvider().lookup(input_model)


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
