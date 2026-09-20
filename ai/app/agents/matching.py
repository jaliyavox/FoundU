"""Read-only, deterministic matching agent backed by the central tool registry."""

from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, ValidationError

from app.agents.state import AgentState, create_tool_execution_context
from app.tools.errors import ToolError
from app.tools.models import ReportLookupOutput, ReportSummary, SuppliedReportContext
from app.tools.registry import ToolRegistry


class MatchingRequest(BaseModel):
    """Explicit context-only request for the Phase 5 matching path."""

    model_config = ConfigDict(extra="forbid", strict=True)

    operation: Literal["match_reports"]
    lost_report: SuppliedReportContext
    found_report: SuppliedReportContext


class MatchingResult(BaseModel):
    """A recommendation only; it never creates or changes a claim."""

    model_config = ConfigDict(extra="forbid", strict=True)

    recommendation: Literal["match_candidate", "no_match", "manual_review"]
    score: float = Field(ge=0.0, le=1.0)


def _score_reports(lost: ReportLookupOutput, found: ReportLookupOutput) -> float:
    """Compare only validated public fields; report descriptions are intentionally unused."""
    if not lost.found or not found.found or lost.report is None or found.report is None:
        return 0.0
    same_type = lost.report.item_type.casefold() == found.report.item_type.casefold()
    same_color = lost.report.primary_color.casefold() == found.report.primary_color.casefold()
    if same_type and same_color:
        return 1.0
    if same_type or same_color:
        return 0.5
    return 0.0


def _report_summary(report: SuppliedReportContext) -> ReportSummary:
    """Pass only the minimum matching fields across the read-only tool boundary."""
    return ReportSummary(
        report_id=report.report_id,
        item_type=report.item_type,
        primary_color=report.primary_color,
    )


def _safe_lookup_failure(trace: list[str], error: ToolError) -> AgentState:
    """Do not read raw payload or adapters after a registry failure."""
    return {
        "output": MatchingResult(recommendation="manual_review", score=0.0).model_dump(),
        "trace": [*trace, *error.trace, "matching:lookup_unavailable"],
    }


def matching_node(state: AgentState, *, tool_registry: ToolRegistry | None = None) -> AgentState:
    """Execute two authorized, read-only report lookups before making a recommendation.

    Legacy payloads retain the previous foundation response. The explicit ``match_reports`` path
    cannot bypass a failed registry call by inspecting raw report context or invoking an adapter.
    """
    payload = state.get("payload", {})
    if payload.get("operation") != "match_reports":
        return {
            "output": {
                "stub": True,
                "message": "Matching Agent foundation is ready.",
            },
            "trace": [*state["trace"], "executed:matching"],
        }

    try:
        request = MatchingRequest.model_validate(payload)
    except ValidationError:
        return {
            "output": MatchingResult(recommendation="manual_review", score=0.0).model_dump(),
            "trace": [*state["trace"], "matching:invalid_request"],
        }

    if tool_registry is None:
        return {
            "output": MatchingResult(recommendation="manual_review", score=0.0).model_dump(),
            "trace": [*state["trace"], "matching:lookup_unavailable"],
        }

    context = create_tool_execution_context(state)
    trace = [*state["trace"]]
    try:
        lost_result = tool_registry.execute(
            "getLostReportDetails",
            context,
            {
                "report_id": request.lost_report.report_id,
                "report": _report_summary(request.lost_report),
            },
        )
        trace.extend(lost_result.trace)
        found_result = tool_registry.execute(
            "getFoundReportDetails",
            context,
            {
                "report_id": request.found_report.report_id,
                "report": _report_summary(request.found_report),
            },
        )
        trace.extend(found_result.trace)
    except ToolError as error:
        return _safe_lookup_failure(trace, error)

    score = _score_reports(lost_result.output, found_result.output)
    recommendation: Literal["match_candidate", "no_match"]
    recommendation = "match_candidate" if score > 0.0 else "no_match"
    output = MatchingResult(recommendation=recommendation, score=score)
    return {
        "output": output.model_dump(),
        "trace": [*trace, "matching:scored", "executed:matching"],
    }
