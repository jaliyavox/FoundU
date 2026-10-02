"""Read-only, deterministic matching agent backed by the central tool registry."""

import re
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, ValidationError

from app.agents.models import AgentName, AgentPlan
from app.agents.plans import (
    PlanValidationError,
    build_matching_plan,
    plan_includes_tool,
    validate_agent_plan,
)
from app.agents.state import AgentState, create_tool_execution_context
from app.tools.errors import ToolError
from app.tools.models import (
    ReportLookupOutput,
    ReportSummary,
    SuppliedReportContext,
    ToolExecutionContext,
)
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


# Reporting boilerplate contributes no identifying information.
_DESCRIPTION_STOP_WORDS = frozenset(
    "a an the and or is it its was were be been my this that of to in on at near "
    "by for with lost found item color colour area place student study".split()
)


def _normalize(value: str | None) -> str:
    """Casefold, replace punctuation with spaces, and collapse whitespace."""
    return " ".join(re.findall(r"[^\W_]+", (value or "").casefold()))


def _description_similarity(left: str | None, right: str | None) -> float:
    """Jaccard overlap of unique meaningful public words; missing text earns zero."""
    a = set(_normalize(left).split()) - _DESCRIPTION_STOP_WORDS
    b = set(_normalize(right).split()) - _DESCRIPTION_STOP_WORDS
    return len(a & b) / len(a | b) if a and b else 0.0


def _score_reports(lost: ReportLookupOutput, found: ReportLookupOutput) -> float:
    """Fixed evidence weights, never renormalized when optional evidence is missing."""
    if not lost.found or not found.found or lost.report is None or found.report is None:
        return 0.0
    left, right = lost.report, found.report
    item_type = _normalize(left.item_type)
    if not item_type or item_type != _normalize(right.item_type):
        return 0.0
    color = _normalize(left.primary_color)
    same_color = bool(color) and color == _normalize(right.primary_color)
    location = _normalize(left.location)
    same_location = bool(location) and location == _normalize(right.location)
    score = (0.40 + 0.20 * same_color
             + 0.25 * _description_similarity(left.description, right.description)
             + 0.15 * same_location)
    return round(min(1.0, max(0.0, score)), 6)


def _report_summary(report: SuppliedReportContext) -> ReportSummary:
    """Pass only the minimum matching fields across the read-only tool boundary."""
    return ReportSummary(
        report_id=report.report_id,
        item_type=report.item_type,
        primary_color=report.primary_color,
        description=report.description,
        location=report.location,
    )


def _safe_lookup_failure(trace: list[str], error: ToolError, plan: AgentPlan) -> AgentState:
    """Do not read raw payload or adapters after a registry failure."""
    return {
        "output": MatchingResult(recommendation="manual_review", score=0.0).model_dump(),
        "trace": [*trace, *error.trace, "matching:lookup_unavailable"],
        "plan": plan,
    }


def _execute_planned_tool(
    plan: AgentPlan,
    tool_registry: ToolRegistry,
    tool_name: str,
    context: ToolExecutionContext,
    raw_input: object,
):
    """Prevent Matching from invoking even an allowed tool absent from its validated plan."""
    if not plan_includes_tool(plan, tool_name):
        raise PlanValidationError()
    return tool_registry.execute(tool_name, context, raw_input)


def matching_node(state: AgentState, *, tool_registry: ToolRegistry | None = None) -> AgentState:
    """Execute two authorized, read-only report lookups before making a recommendation.

    Legacy payloads retain the previous foundation response. The explicit ``match_reports`` path
    cannot bypass a failed registry call by inspecting raw report context or invoking an adapter.
    """
    payload = state.get("payload", {})
    match_reports = payload.get("operation") == "match_reports"
    plan = build_matching_plan(match_reports)
    trusted_agent = state.get("requested_agent", AgentName.MATCHING)
    try:
        validate_agent_plan(
            plan,
            trusted_agent,
            tool_registry if match_reports else None,
        )
    except PlanValidationError:
        return {
            "output": MatchingResult(recommendation="manual_review", score=0.0).model_dump(),
            "trace": [*state["trace"], "plan:rejected", "matching:lookup_unavailable"],
            "plan": plan,
        }

    if not match_reports:
        return {
            "output": {
                "stub": True,
                "message": "Matching Agent foundation is ready.",
            },
            "trace": [*state["trace"], "executed:matching"],
            "plan": plan,
        }

    try:
        request = MatchingRequest.model_validate(payload)
    except ValidationError:
        return {
            "output": MatchingResult(recommendation="manual_review", score=0.0).model_dump(),
            "trace": [*state["trace"], "matching:invalid_request"],
            "plan": plan,
        }

    if tool_registry is None:
        return {
            "output": MatchingResult(recommendation="manual_review", score=0.0).model_dump(),
            "trace": [*state["trace"], "matching:lookup_unavailable"],
            "plan": plan,
        }

    context = create_tool_execution_context(state)
    trace = [*state["trace"]]
    try:
        lost_result = _execute_planned_tool(
            plan,
            tool_registry,
            "getLostReportDetails",
            context,
            {
                "report_id": request.lost_report.report_id,
                "report": _report_summary(request.lost_report),
            },
        )
        trace.extend(lost_result.trace)
        found_result = _execute_planned_tool(
            plan,
            tool_registry,
            "getFoundReportDetails",
            context,
            {
                "report_id": request.found_report.report_id,
                "report": _report_summary(request.found_report),
            },
        )
        trace.extend(found_result.trace)
    except ToolError as error:
        return _safe_lookup_failure(trace, error, plan)
    except PlanValidationError:
        return {
            "output": MatchingResult(recommendation="manual_review", score=0.0).model_dump(),
            "trace": [*trace, "plan:rejected", "matching:lookup_unavailable"],
            "plan": plan,
        }

    score = _score_reports(lost_result.output, found_result.output)
    recommendation: Literal["match_candidate", "manual_review", "no_match"]
    recommendation = ("match_candidate" if score >= 0.75 else
                      "manual_review" if score >= 0.50 else "no_match")
    output = MatchingResult(recommendation=recommendation, score=score)
    return {
        "output": output.model_dump(),
        "trace": [*trace, "matching:scored", "executed:matching"],
        "plan": plan,
    }
