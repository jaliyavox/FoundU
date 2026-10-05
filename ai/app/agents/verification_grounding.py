"""Fail-closed public question grammar, shared with ASP.NET via an embedded catalogue.

Each pattern inspects one clause of one original observation. No evidence values are
interpolated into questions and no model is trusted to judge its own grounding.
Unrecognised observations use an open request for a detail, with no asserted attribute.
"""

import json
import re
from dataclasses import dataclass
from pathlib import Path

GENERIC_QUESTION = "What identifying detail can you provide about the item?"
_RULES = json.loads(Path(__file__).with_name("verification_grounding.json").read_text())


def normalize_question(question: str) -> str:
    return re.sub(r"[^a-z0-9]+", " ", question.lower().replace("colour", "color")).strip()


def candidate_questions(detail: str) -> list[str]:
    clauses = re.split(r"[.!?;\n]|\band\b|\bbut\b", detail.lower())
    # Negated/uncertain statements do not establish positive physical observations.
    clauses = [c for c in clauses if not re.search(
        r"\b(no|not|without|lacks|lacking|missing|none|maybe|possibly|might|could|unknown)\b"
        r"|\b\w+-less\b|\b(?:isn|wasn|doesn|didn)'t\b", c
    )]
    return list(dict.fromkeys(
        question
        for rule in _RULES
        if any(re.search(rule["pattern"], clause) for clause in clauses)
        for question in rule["questions"]
        if "bottle" not in question or re.search(r"\bbottle\b", detail.lower())
    ))


def is_grounded(question: str, detail: str) -> bool:
    return normalize_question(question) in {
        normalize_question(q) for q in [*candidate_questions(detail), GENERIC_QUESTION]
    } and bool(detail.strip())


@dataclass(frozen=True)
class GroundedAnswerFact:
    kind: str
    value: str
    subject: str = "item"


def answer_fact(question: str, detail: str) -> GroundedAnswerFact | None:
    """Resolve only the question's supported attribute, never other paragraph facts.

    Ambiguous observations fail closed. Expected values stay internal. Broad requests
    use one original clause; existing single-observation descriptive scoring is retained.
    """
    normalized = normalize_question(question)
    subject = next((s for s in ("cap", "sticker", "scratch", "damage", "bottle")
                    if s in normalized.split()), "item")
    facts = set()
    clauses = re.split(r"[.!?;\n]|\band\b|\bbut\b", detail.lower())
    for rule in _RULES:
        if normalized not in {normalize_question(q) for q in rule["questions"]}:
            continue
        if not is_grounded(question, detail):
            return None
        for clause in clauses:
            if not any(normalize_question(q) == normalized for q in candidate_questions(clause)):
                continue
            for match in re.finditer(rule["answer_pattern"], clause):
                kind = rule["answer_kind"]
                presence = {normalize_question(q) for q in rule.get("presence_questions", [])}
                if normalized in presence:
                    kind, value = "presence", "yes"
                elif rule["answer_group"] == -1:
                    value = next((v for v in match.groups() if v), "")
                elif rule["answer_group"] > 0:
                    value = match.group(rule["answer_group"])
                else:
                    value = clause.strip()
                if value:
                    facts.add(GroundedAnswerFact(kind, value.strip(), subject))
    if not facts and normalized == normalize_question(GENERIC_QUESTION):
        usable = [c.strip() for c in clauses if c.strip()]
        # A broad question with multiple independent observations has no unique target.
        if len(usable) == 1:
            return GroundedAnswerFact("description", usable[0])
    return next(iter(facts)) if len(facts) == 1 else None
