"""Read-only, deterministic public-evidence matcher."""

import re
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
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
    matched_factors: list[str] = Field(default_factory=list, max_length=4)
    missing_factors: list[str] = Field(default_factory=list, max_length=4)
    conflicting_factors: list[str] = Field(default_factory=list, max_length=4)


# Reporting boilerplate, the item type, colours, and campus-place wording do not identify an
# individual item. Removing them also prevents double-counting the other score components.
_DESCRIPTION_STOP_WORDS = frozenset(
    "a an the and or is it its was were be been my this that of to in on at near "
    "by beside for from with lost found item color colour area place student study campus "
    "i we you they has have had there here just some one an another "
    "code number regular regularly ordinary usual standard basic "
    "library canteen cafeteria building room floor desk table tables regularly used".split()
)

_COLOR_ALIASES = {
    # Spelling variants only. Shades such as navy and blue are not identical evidence.
    "grey": "gray",
}
_KNOWN_COLORS = frozenset(
    "black white red blue green yellow orange purple pink gray brown silver gold beige teal".split()
)
_TOKEN_ALIASES = {
    "scuff": "scratch", "scuffed": "scratch", "scratched": "scratch",
    "marking": "mark", "markings": "mark",
    "zip": "zipper", "zipped": "zipper", "base": "bottom",
    "minor": "small", "tiny": "small", "sized": "",
    "aluminium": "aluminum", "rucksack": "backpack", "cellphone": "phone",
}
_ATTRIBUTE_VALUES = {
    "size": {"small", "medium", "large"},
    "material": {
        "stainless_steel", "steel", "aluminum", "plastic", "glass",
        "leather", "canvas", "wood", "metal",
    },
    "pattern": {"plain", "striped", "spotted", "checkered"},
    "brand": {"nike", "adidas", "apple", "samsung", "dell", "lenovo", "hp"},
}

_STRONG_CANDIDATE_MIN = 0.65
_STRONG_DESCRIPTION_MIN = 0.10
_UNLIKELY_MAX = 0.35


@dataclass(frozen=True)
class _ScoreBreakdown:
    score: float
    description_score: float
    matched: list[str]
    missing: list[str]
    conflicting: list[str]


def _normalize(value: str | None) -> str:
    """Casefold, replace punctuation with spaces, and collapse whitespace."""
    return " ".join(re.findall(r"[^\W_]+", (value or "").casefold()))


def _normalize_color(value: str | None) -> str:
    normalized = _normalize(value).replace("colour", "color")
    return _COLOR_ALIASES.get(normalized, normalized)


def _description_tokens(value: str | None, item_type: str | None, color: str | None) -> set[str]:
    text = (value or "").casefold().replace("stainless-steel", "stainless_steel")
    text = text.replace("stainless steel", "stainless_steel")
    tokens = re.findall(r"[a-z0-9_]+", text)
    excluded = _DESCRIPTION_STOP_WORDS | set(_normalize(item_type).split()) | _KNOWN_COLORS
    normalized = {_TOKEN_ALIASES.get(token, token) for token in tokens if token not in excluded}
    normalized.discard("")
    # The structured primary colour owns colour evidence. Explicit aliases must not sneak it
    # back into description credit.
    normalized.discard(_normalize_color(color))
    return normalized


def _description_similarity(
    left: str | None,
    right: str | None,
    *,
    item_type: str | None = None,
    left_color: str | None = None,
    right_color: str | None = None,
) -> float:
    """Dice overlap of public identifying details after removing already-scored context."""
    a = _description_tokens(left, item_type, left_color)
    b = _description_tokens(right, item_type, right_color)
    return 2 * len(a & b) / (len(a) + len(b)) if a and b else 0.0


def _attribute_values(tokens: set[str], attribute: str) -> set[str]:
    return tokens & _ATTRIBUTE_VALUES[attribute]


def _description_conflicts(left: set[str], right: set[str]) -> list[str]:
    conflicts: list[str] = []
    for attribute in ("size", "pattern", "brand"):
        a, b = _attribute_values(left, attribute), _attribute_values(right, attribute)
        if a and b and a.isdisjoint(b):
            conflicts.append(f"Public descriptions disagree about {attribute}.")
    a_material = _attribute_values(left, "material")
    b_material = _attribute_values(right, "material")
    if a_material and b_material and a_material.isdisjoint(b_material):
        # "metal" and "steel" are broad and compatible with their specific forms, but are
        # not equivalent and earn no shared-detail credit.
        compatible_broad_metal = ("metal" in a_material or "metal" in b_material
                                  or {"steel", "stainless_steel"} <= (a_material | b_material))
        metallic = {"stainless_steel", "steel", "aluminum", "metal"}
        if not compatible_broad_metal or not (a_material | b_material) <= metallic:
            conflicts.append("Public descriptions disagree about material.")
    return conflicts


def _as_utc(value: str) -> datetime:
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    return parsed.replace(tzinfo=UTC) if parsed.tzinfo is None else parsed.astimezone(UTC)


def _time_score(lost: ReportSummary, found: ReportSummary) -> tuple[float, str | None, str | None]:
    if lost.event_start_at is None or lost.event_end_at is None or found.event_start_at is None:
        return 0.0, None, "Reported time evidence is incomplete (0/25)."
    try:
        start, end, found_at = map(
            _as_utc, (lost.event_start_at, lost.event_end_at, found.event_start_at)
        )
    except ValueError:
        return 0.0, None, "Reported time evidence is invalid (0/25)."
    if end < start:
        return 0.0, None, "The approximate lost-time window is invalid (0/25)."
    # Lost times are explicitly approximate. A find up to two hours before the entered start is
    # tolerated; earlier finds are a clear sequence conflict. Later finds remain plausible, with
    # decreasing evidence rather than an arbitrary rejection.
    if found_at < start - timedelta(hours=2):
        return 0.0, None, "The item was reportedly found before the tolerated loss window (0/25)."
    if found_at <= end + timedelta(days=1):
        return 0.25, "Reported times are in a strongly plausible sequence (25/25).", None
    if found_at <= end + timedelta(days=30):
        return 0.15, "Reported times are in a plausible sequence (15/25).", None
    return 0.08, "The found time is later than the loss window but still possible (8/25).", None


def _score_reports(lost: ReportLookupOutput, found: ReportLookupOutput) -> _ScoreBreakdown:
    """Score only public evidence; eligibility contributes no points."""
    if not lost.found or not found.found or lost.report is None or found.report is None:
        return _ScoreBreakdown(0.0, 0.0, [], ["Report evidence is unavailable."], [])
    left, right = lost.report, found.report
    item_type = _normalize(left.item_type)
    if not item_type or item_type != _normalize(right.item_type):
        return _ScoreBreakdown(0.0, 0.0, [], [], ["Item types are incompatible."])

    matched: list[str] = []
    missing: list[str] = []
    conflicting: list[str] = []
    score = 0.0

    left_color = _normalize_color(left.primary_color)
    right_color = _normalize_color(right.primary_color)
    if not left_color or not right_color:
        missing.append("Primary colour evidence is incomplete (0/20).")
    elif left_color == right_color:
        score += 0.20
        matched.append("Reported primary colour matched (20/20).")
    else:
        conflicting.append("Reported primary colours conflict (0/20).")

    left_tokens = _description_tokens(left.description, left.item_type, left.primary_color)
    right_tokens = _description_tokens(right.description, right.item_type, right.primary_color)
    similarity = _description_similarity(
        left.description, right.description, item_type=left.item_type,
        left_color=left.primary_color, right_color=right.primary_color,
    )
    description_score = 0.35 * similarity
    score += description_score
    if not left_tokens or not right_tokens:
        missing.append("Comparable public identifying details are missing (0/35).")
    elif description_score > 0:
        matched.append(f"Public identifying details matched ({description_score * 100:.1f}/35).")
    else:
        missing.append("Public descriptions share no identifying detail (0/35).")
    conflicting.extend(_description_conflicts(left_tokens, right_tokens))

    location = _normalize(left.location)
    right_location = _normalize(right.location)
    if not location or not right_location:
        missing.append("Structured location evidence is incomplete (0/20).")
    elif location == right_location:
        score += 0.20
        matched.append("Structured campus location matched (20/20).")
    else:
        missing.append("Structured locations differ; no proximity metadata is available (0/20).")

    time_score, time_match, time_problem = _time_score(left, right)
    score += time_score
    if time_match:
        matched.append(time_match)
    if time_problem:
        target = conflicting if "before" in time_problem or "invalid" in time_problem else missing
        target.append(time_problem)

    return _ScoreBreakdown(
        round(min(1.0, max(0.0, score)), 6), round(description_score, 6),
        matched, missing, conflicting,
    )


def _report_summary(report: SuppliedReportContext) -> ReportSummary:
    """Pass only the minimum matching fields across the read-only tool boundary."""
    return ReportSummary(
        report_id=report.report_id,
        item_type=report.item_type,
        primary_color=report.primary_color,
        description=report.description,
        location=report.location,
        event_start_at=report.event_start_at,
        event_end_at=report.event_end_at,
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

    breakdown = _score_reports(lost_result.output, found_result.output)
    recommendation: Literal["match_candidate", "manual_review", "no_match"]
    if ("Item types are incompatible." in breakdown.conflicting
            or (breakdown.score < _UNLIKELY_MAX and len(breakdown.conflicting) >= 2)):
        recommendation = "no_match"
    elif (breakdown.score >= _STRONG_CANDIDATE_MIN
            and breakdown.description_score >= _STRONG_DESCRIPTION_MIN
            and not breakdown.conflicting):
        recommendation = "match_candidate"
    else:
        # One disagreement or missing public evidence cannot establish an unlikely item.
        recommendation = "manual_review"
    output = MatchingResult(
        recommendation=recommendation,
        score=breakdown.score,
        matched_factors=breakdown.matched,
        missing_factors=breakdown.missing,
        conflicting_factors=breakdown.conflicting,
    )
    return {
        "output": output.model_dump(),
        "trace": [*trace, "matching:scored", "executed:matching"],
        "plan": plan,
    }
