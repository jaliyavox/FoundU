"""Deterministic scoring for atomic attributes; expected values remain internal."""

import re

from app.agents.models import VerificationAnswerEvaluation
from app.agents.verification_grounding import GroundedAnswerFact

COLORS = set("black white red blue green yellow orange purple pink gray brown silver gold".split())
WRAPPER = set(
    "the a an it its has have had is are was were i my item color colour brand located "
    "location of found this that type there any".split()
)


def normalized(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", " ", value.lower().replace("grey", "gray")).strip()


def uncertain(answer: str) -> bool:
    return not answer.strip() or bool(re.search(
        r"\b(?:don['’]?t know|do not know|no idea|unknown|unsure|not sure|maybe|possibly|"
        r"might|could|guess|cannot remember|can['’]?t remember)\b", answer.lower()
    ))


def evaluate_atomic(question_id: str, answer: str, fact: GroundedAnswerFact | None):
    def result(kind, score):
        return VerificationAnswerEvaluation(question_id=question_id, result=kind, score=score)

    if fact is None or uncertain(answer):
        return result("insufficient", 0.0)
    if re.search(r"ignore|system prompt|approve|instruction", answer, re.IGNORECASE):
        return result("insufficient", 0.0)
    actual = set(normalized(answer).split())
    expected = set(normalized(fact.value).split()) - {"the", "a", "an"}
    wrapper = WRAPPER | {fact.subject}
    if fact.subject == "cap":
        wrapper |= {"bottle"}
    if actual.intersection({"not", "no", "isn", "wasn", "doesn", "didn"}):
        return result("no_match", 0.0)
    if fact.kind == "presence":
        return result("match", 1.0) if "yes" in actual and actual <= wrapper | {"yes"} else (
            result("insufficient", 0.0)
        )
    if fact.kind == "color":
        colors = actual.intersection(COLORS)
        if colors and colors != expected:
            return result("no_match", 0.0)
        # 'dark black' is an explicitly supported colour equivalent, not token overlap.
        permitted = wrapper | expected | ({"dark", "pitch"} if expected == {"black"} else set())
    elif fact.kind == "location":
        # Retain relational words and quantities: 'on one side' is one complete fact.
        permitted = wrapper | expected
        positions = {"top", "bottom", "front", "back", "left", "right", "inside", "outside"}
        if actual & positions and not (actual & positions) <= expected:
            return result("no_match", 0.0)
    else:
        permitted = wrapper | expected
    if expected.issubset(actual) and actual.issubset(permitted):
        return result("match", 1.0)
    if fact.kind == "brand" and actual and not actual.intersection(COLORS):
        if not actual.intersection(expected):
            return result("no_match", 0.0)
    if actual.intersection(expected):
        return result("partial_match", 0.4)
    # Off-topic answers supply no requested information; they do not prove a contradiction.
    return result("insufficient", 0.0)
