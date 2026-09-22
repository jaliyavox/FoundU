"""Intake Agent: the conversation an owner has when they come looking for something.

The agent asks what was lost, in a fixed order, until it knows enough to search: what the item
is, its colour, and roughly where or when. ASP.NET owns the search - this service never sees the
database - so the node works in two calls: first it collects, and says when it is ready; then it
is called again with the candidates ASP.NET found, and picks one or admits there is none.

Every step has a deterministic path. The language model, when present, does two bounded jobs:
pull slot values out of free text, and word the next question. When it is absent or fails, a
keyword pass over the vocabulary ASP.NET sends does the extraction and a fixed sentence asks the
question. The conversation never stalls on the model.

Nothing here decides ownership. A "match" is a suggestion for the owner to confirm; the desk's
verification questions still decide, exactly as they do for a match a person made by hand.
"""

from __future__ import annotations

import re
from typing import Any, Literal

from pydantic import BaseModel, ConfigDict, Field, ValidationError

from app.agents.state import AgentState
from app.llm.client import LlmClient
from app.llm.errors import LlmError
from app.llm.models import StructuredGenerationRequest

# ------------------------------------------------------------------ contracts


class IntakeTurn(BaseModel):
    model_config = ConfigDict(extra="forbid")

    role: Literal["user", "assistant"]
    text: str = Field(min_length=1, max_length=1000)


class IntakeSlots(BaseModel):
    """What the agent knows so far. Every field is optional until said."""

    model_config = ConfigDict(extra="forbid")

    item_type: str | None = Field(default=None, max_length=80)
    colour: str | None = Field(default=None, max_length=40)
    location: str | None = Field(default=None, max_length=80)
    when: str | None = Field(default=None, max_length=80)
    distinctive: str | None = Field(default=None, max_length=200)

    def is_enough_to_search(self) -> bool:
        # An item and one more anchor. Colour or place is enough to narrow a campus board.
        return self.item_type is not None and (self.colour is not None or self.location is not None)

    def missing_in_order(self) -> list[str]:
        order = ["item_type", "colour", "location", "when"]
        return [name for name in order if getattr(self, name) is None]


class IntakeCandidate(BaseModel):
    """A found item as ASP.NET is allowed to show it: the student-safe summary, no evidence."""

    model_config = ConfigDict(extra="forbid")

    id: str = Field(min_length=1, max_length=64)
    item_type: str = Field(max_length=80)
    colour: str | None = Field(default=None, max_length=40)
    location: str = Field(max_length=80)
    description: str = Field(max_length=500)
    kind: Literal["post", "desk"]


class IntakeVocabulary(BaseModel):
    """The words the extractor may match against - the campus's own taxonomy."""

    model_config = ConfigDict(extra="forbid")

    item_types: list[str] = Field(default_factory=list, max_length=200)
    locations: list[str] = Field(default_factory=list, max_length=200)


class IntakeRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    history: list[IntakeTurn] = Field(min_length=1, max_length=40)
    slots: IntakeSlots = Field(default_factory=IntakeSlots)
    vocabulary: IntakeVocabulary = Field(default_factory=IntakeVocabulary)
    candidates: list[IntakeCandidate] | None = None


class IntakeResult(BaseModel):
    """What ASP.NET acts on. `phase` says what to do next; the reply is what the person reads."""

    model_config = ConfigDict(extra="forbid")

    reply: str
    slots: IntakeSlots
    phase: Literal["collecting", "ready_to_search", "matched", "no_match"]
    match_candidate_id: str | None = None
    match_confidence: float | None = Field(default=None, ge=0.0, le=1.0)


# ----------------------------------------------------------- llm-shaped outputs


class SlotExtraction(BaseModel):
    """Strict, bounded model output: only the slots, never prose."""

    model_config = ConfigDict(extra="forbid", strict=True)

    item_type: str | None = Field(default=None, max_length=80)
    colour: str | None = Field(default=None, max_length=40)
    location: str | None = Field(default=None, max_length=80)
    when: str | None = Field(default=None, max_length=80)
    distinctive: str | None = Field(default=None, max_length=200)


class CandidatePick(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)

    candidate_id: str | None = Field(default=None, max_length=64)
    confidence: float = Field(ge=0.0, le=1.0)


# --------------------------------------------------------------- vocabulary

_COLOURS = [
    "black",
    "white",
    "grey",
    "gray",
    "silver",
    "red",
    "blue",
    "navy",
    "green",
    "yellow",
    "orange",
    "pink",
    "purple",
    "brown",
    "beige",
    "gold",
    "maroon",
    "teal",
    "cream",
]

_STOP_WORDS = {"main", "center", "centre", "building", "block", "hall", "front", "desk", "area"}

_WHEN_HINTS = [
    "today",
    "yesterday",
    "this morning",
    "this afternoon",
    "last night",
    "monday",
    "tuesday",
    "wednesday",
    "thursday",
    "friday",
    "saturday",
    "sunday",
    "last week",
]


def _keyword_extract(text: str, vocabulary: IntakeVocabulary) -> SlotExtraction:
    """The path that never fails: match the person's words against the campus's own names."""
    lowered = text.lower()
    found: dict[str, str | None] = {
        "item_type": None,
        "colour": None,
        "location": None,
        "when": None,
        "distinctive": None,
    }

    # Longest names first, so "student id card" beats "card".
    for name in sorted(vocabulary.item_types, key=len, reverse=True):
        if name.lower() in lowered:
            found["item_type"] = name
            break
    if found["item_type"] is None:
        # "my bottle" should find "Water Bottle": the last word of a type name, singular or plural.
        for name in sorted(vocabulary.item_types, key=len, reverse=True):
            head = re.findall(r"[a-z]+", name.lower())
            if not head:
                continue
            stem = head[-1].rstrip("s")
            if len(stem) >= 3 and re.search(rf"\b{re.escape(stem)}s?\b", lowered):
                found["item_type"] = name
                break

    for colour in _COLOURS:
        if re.search(rf"\b{colour}\b", lowered):
            found["colour"] = colour
            break

    for name in sorted(vocabulary.locations, key=len, reverse=True):
        if name.lower() in lowered:
            found["location"] = name
            break
    if found["location"] is None:
        # "near the gym" should find "Sports Center Gym": any distinctive word of a place name.
        for name in sorted(vocabulary.locations, key=len, reverse=True):
            tokens = [
                t
                for t in re.findall(r"[a-z]+", name.lower())
                if len(t) >= 3 and t not in _STOP_WORDS
            ]
            if any(re.search(rf"\b{re.escape(t)}\b", lowered) for t in tokens):
                found["location"] = name
                break

    for hint in _WHEN_HINTS:
        if hint in lowered:
            found["when"] = hint
            break

    return SlotExtraction(**found)


def _merge(slots: IntakeSlots, extracted: SlotExtraction) -> IntakeSlots:
    """Newer words fill gaps; they do not overwrite what was already said."""
    data = slots.model_dump()
    for key, value in extracted.model_dump().items():
        if data.get(key) is None and value:
            data[key] = value
    return IntakeSlots(**data)


# ----------------------------------------------------------------- wording

_QUESTIONS = {
    "item_type": "What did you lose? A bag, a bottle, a card - whatever you'd call it.",
    "colour": "What colour is it, mainly?",
    "location": "Where do you think you left it? A building or an area is enough.",
    "when": "Roughly when? Today, yesterday, a day of the week.",
}


def _question_for(missing: str, slots: IntakeSlots) -> str:
    if missing == "colour" and slots.item_type:
        return f"Got it - a {slots.item_type.lower()}. What colour is it, mainly?"
    return _QUESTIONS[missing]


def _describe(slots: IntakeSlots) -> str:
    parts = [p for p in [slots.colour, slots.item_type.lower() if slots.item_type else None] if p]
    return " ".join(parts) if parts else "item"


# ------------------------------------------------------------------- scoring


def _score(candidate: IntakeCandidate, slots: IntakeSlots) -> float:
    """Deterministic, explainable, and enough on its own for a campus board."""
    score = 0.0
    if slots.item_type and candidate.item_type.lower() == slots.item_type.lower():
        score += 0.55
    if slots.colour and candidate.colour and candidate.colour.lower() == slots.colour.lower():
        score += 0.25
    if slots.location and candidate.location.lower() == slots.location.lower():
        score += 0.15
    if slots.distinctive and slots.distinctive.lower() in candidate.description.lower():
        score += 0.05
    return min(score, 1.0)


# ---------------------------------------------------------------------- node


def intake_node(state: AgentState, llm_client: LlmClient | None = None) -> AgentState:
    trace = [*state["trace"], "executed:intake"]

    try:
        request = IntakeRequest.model_validate(state["payload"])
    except ValidationError as error:
        return {"output": {"error": "invalid_request"}, "error": str(error), "trace": trace}

    latest = request.history[-1]
    slots = request.slots

    # ---- collecting: read the latest user words into slots
    if latest.role == "user":
        extracted = _keyword_extract(latest.text, request.vocabulary)
        if llm_client is not None:
            try:
                model_view = llm_client.generate_structured(
                    StructuredGenerationRequest(
                        operation="intake_extract",
                        system_instruction=(
                            "Extract only what the person states about a lost item: item type, "
                            "main colour, place, rough time, one distinctive feature. Use the item "
                            "type names and location names given when they fit. Leave anything "
                            "not stated as null. Never invent."
                        ),
                        input={
                            "message": latest.text,
                            "known": slots.model_dump(),
                            "item_types": request.vocabulary.item_types[:120],
                            "locations": request.vocabulary.locations[:120],
                        },
                        correlation_id=state.get("correlation_id"),
                    ),
                    SlotExtraction,
                )
                # The model may only add words the person actually used, or vocabulary names.
                extracted = _merge_extractions(
                    extracted, model_view, latest.text, request.vocabulary
                )
                trace.append("intake:llm_extracted")
            except (LlmError, ValidationError):
                trace.append("intake:llm_unavailable")
        slots = _merge(slots, extracted)

    # ---- searching: ASP.NET has been asked, and answered with candidates
    if request.candidates is not None:
        ranked = sorted(request.candidates, key=lambda c: _score(c, slots), reverse=True)
        best = ranked[0] if ranked else None
        confidence = _score(best, slots) if best else 0.0

        if best is not None and confidence >= 0.55:
            where = (
                "at a desk"
                if best.kind == "desk"
                else "posted by a student who found it, not yet at a desk"
            )
            colour_word = f"{best.colour} " if best.colour else ""
            reply = (
                f"This might be it: a {colour_word}{best.item_type.lower()} "
                f'{where}, found at {best.location}. "{best.description}" '
                + (
                    "If it's yours, I can open a claim - the desk will ask you one question "
                    "only the owner could answer."
                    if best.kind == "desk"
                    else "If it's yours, say so and the finder will be asked to hand it in."
                )
            )
            return {
                "output": IntakeResult(
                    reply=reply,
                    slots=slots,
                    phase="matched",
                    match_candidate_id=best.id,
                    match_confidence=round(confidence, 2),
                ).model_dump(),
                "trace": [*trace, "intake:matched"],
            }

        reply = (
            f"Nothing like your {_describe(slots)} has been handed in or posted yet. "
            "I've drafted a lost report from what you told me - check it and post it, "
            "and you'll be told the moment something matching turns up."
        )
        return {
            "output": IntakeResult(reply=reply, slots=slots, phase="no_match").model_dump(),
            "trace": [*trace, "intake:no_match"],
        }

    # ---- enough to search?
    if slots.is_enough_to_search():
        reply = (
            f"Thanks. Let me check what's been found for a {_describe(slots)}"
            + (f" near {slots.location}" if slots.location else "")
            + "."
        )
        return {
            "output": IntakeResult(reply=reply, slots=slots, phase="ready_to_search").model_dump(),
            "trace": [*trace, "intake:ready"],
        }

    # ---- ask the next question, in a fixed order
    missing = slots.missing_in_order()
    next_slot = missing[0] if missing else "item_type"
    return {
        "output": IntakeResult(
            reply=_question_for(next_slot, slots), slots=slots, phase="collecting"
        ).model_dump(),
        "trace": [*trace, f"intake:ask:{next_slot}"],
    }


def _merge_extractions(
    keyword: SlotExtraction, model: SlotExtraction, text: str, vocabulary: IntakeVocabulary
) -> SlotExtraction:
    """Accept a model value only if the person said it, or it names something in the vocabulary.

    This is the guard that keeps a model from inventing a colour or a place. Grounding is
    checked against the message text and the campus taxonomy, nothing else.
    """
    lowered = text.lower()
    allowed_types = {t.lower() for t in vocabulary.item_types}
    allowed_places = {p.lower() for p in vocabulary.locations}
    data: dict[str, Any] = keyword.model_dump()

    def grounded(value: str | None, allowed: set[str]) -> str | None:
        if value is None:
            return None
        v = value.strip()
        if not v:
            return None
        if v.lower() in allowed or v.lower() in lowered:
            return v
        return None

    # Item types and places may be the model mapping the person's words onto a campus name -
    # that is its whole value here. A colour, a time or a feature must have been said: a list
    # of colours is not evidence the person named one.
    for key, allowed in (
        ("item_type", allowed_types),
        ("location", allowed_places),
        ("colour", set()),
        ("when", set()),
        ("distinctive", set()),
    ):
        if data.get(key) is None:
            data[key] = grounded(getattr(model, key), allowed)
    return SlotExtraction(**data)
