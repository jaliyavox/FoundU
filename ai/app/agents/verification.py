"""Deterministic, non-decisioning ownership verification agent.

Private verification details are deliberately confined to this module's local variables.
They must never be returned, placed in traces, or included in errors.
"""

import re
from dataclasses import dataclass
from typing import Any

from pydantic import TypeAdapter, ValidationError

from app.agents.models import (
    AGENT_PERMISSIONS,
    AgentName,
    EvaluateVerificationAnswersRequest,
    GenerateVerificationQuestionsRequest,
    VerificationAnswerEvaluation,
    VerificationQuestion,
    VerificationRequest,
)
from app.agents.plans import PlanValidationError, build_verification_plan, validate_agent_plan
from app.agents.state import AgentState

MAX_GENERATED_QUESTIONS = 3
PARTIAL_MATCH_MIN_SCORE = 0.4

# These intentionally ask only about an evidence category, never its staff-held value.
QUESTION_TEMPLATES: tuple[tuple[tuple[str, ...], str], ...] = (
    (
        ("distinctive", "mark", "damage", "scratch", "crack"),
        "What distinctive mark or damage does the item have?",
    ),
    (
        ("accessory", "attached", "keychain", "strap"),
        "What accessory was attached to the item?",
    ),
    (
        ("inside", "interior", "lining"),
        "What identifying detail is visible on the inside?",
    ),
    (
        ("case", "cover", "sleeve"),
        "What specific case or cover was the item using?",
    ),
    (
        ("engraving", "initial", "label", "sticker"),
        "What identifying marking or label does the item have?",
    ),
)
GENERIC_QUESTION = "What identifying detail can you provide about the item?"


@dataclass(frozen=True)
class _InternalChallenge:
    """Private binding between a safe question and its staff-held expected value."""

    question: VerificationQuestion
    expected_value: str


def check_agent_permissions() -> None:
    """Ensure this agent remains unable to approve or reject a claim."""
    permissions = AGENT_PERMISSIONS.get(AgentName.VERIFICATION)
    if not permissions or permissions.has_approval_permission:
        raise PermissionError("Verification Agent does NOT have permission to decide claims.")


def _template_for_key(key: str) -> str | None:
    normalized_key = re.sub(r"[^a-z0-9]+", " ", key.lower())
    for keywords, template in QUESTION_TEMPLATES:
        if any(keyword in normalized_key.split() for keyword in keywords):
            return template
    return None


def _build_challenges(details: dict[str, str]) -> list[_InternalChallenge]:
    """Build canonical challenges with private evidence bindings kept in memory only."""
    challenges: list[_InternalChallenge] = []
    used_templates: set[str] = set()
    for key in sorted(details):
        template = _template_for_key(key) or GENERIC_QUESTION
        if template in used_templates:
            continue
        used_templates.add(template)
        challenges.append(
            _InternalChallenge(
                question=VerificationQuestion(
                    question_id=f"verification-{len(challenges) + 1}",
                    question=template,
                ),
                expected_value=details[key],
            )
        )
        if len(challenges) == MAX_GENERATED_QUESTIONS:
            break
    return challenges


def generate_questions(request: GenerateVerificationQuestionsRequest) -> dict[str, Any]:
    """Create at most three safe, category-based questions from non-empty private evidence."""
    details = request.private_verification_details.details
    if not details:
        return {
            "operation": request.operation,
            "claim_id": request.claim_id,
            "questions": [],
            "recommendation": "manual_review",
            "reason": "No usable verification evidence is available.",
        }

    challenges = _build_challenges(details)

    return {
        "operation": request.operation,
        "claim_id": request.claim_id,
        "questions": [challenge.question.model_dump() for challenge in challenges],
        "recommendation": "manual_review",
    }


def _normalize(value: str) -> tuple[str, set[str]]:
    normalized = re.sub(r"[^a-z0-9]+", " ", value.lower()).strip()
    return normalized, set(normalized.split())


def _evaluate_answer(
    question_id: str, answer: str, expected: str | None
) -> VerificationAnswerEvaluation:
    if not answer.strip() or not expected:
        return VerificationAnswerEvaluation(
            question_id=question_id, result="insufficient", score=0.0
        )
    normalized_answer, answer_tokens = _normalize(answer)
    normalized_expected, expected_tokens = _normalize(expected)
    if not normalized_answer or not normalized_expected:
        return VerificationAnswerEvaluation(
            question_id=question_id, result="insufficient", score=0.0
        )
    if normalized_answer == normalized_expected:
        return VerificationAnswerEvaluation(question_id=question_id, result="match", score=1.0)

    score = len(answer_tokens.intersection(expected_tokens)) / len(expected_tokens)
    if score >= PARTIAL_MATCH_MIN_SCORE:
        return VerificationAnswerEvaluation(
            question_id=question_id, result="partial_match", score=score
        )
    return VerificationAnswerEvaluation(question_id=question_id, result="no_match", score=score)


def evaluate_answers(request: EvaluateVerificationAnswersRequest) -> dict[str, Any]:
    """Compare answers deterministically; this deliberately produces recommendations only."""
    answers_by_id = {answer.question_id: answer.answer for answer in request.answers}
    challenges = _build_challenges(request.private_verification_details.details)
    submitted_questions = [
        (question.question_id, question.question) for question in request.questions
    ]
    canonical_questions = [
        (challenge.question.question_id, challenge.question.question) for challenge in challenges
    ]
    if submitted_questions != canonical_questions:
        return _safe_challenge_error_output()
    evaluations = [
        _evaluate_answer(
            challenge.question.question_id,
            answers_by_id.get(challenge.question.question_id, ""),
            challenge.expected_value,
        )
        for challenge in challenges
    ]
    results = [evaluation.result for evaluation in evaluations]
    if results and all(result == "match" for result in results):
        recommendation = "likely_match"
    elif results and all(result == "no_match" for result in results):
        recommendation = "unlikely_match"
    else:
        recommendation = "manual_review"
    return {
        "operation": request.operation,
        "claim_id": request.claim_id,
        "evaluations": [evaluation.model_dump() for evaluation in evaluations],
        "recommendation": recommendation,
    }


def _safe_error_output() -> dict[str, str]:
    return {"recommendation": "manual_review", "error": "Invalid verification request."}


def _safe_challenge_error_output() -> dict[str, str]:
    return {"recommendation": "manual_review", "error": "Verification challenge is invalid."}


def verification_node(state: AgentState) -> AgentState:
    """Validate and run a verification operation without leaking request contents."""
    plan = build_verification_plan()
    try:
        validate_agent_plan(plan, state.get("requested_agent", AgentName.VERIFICATION))
    except PlanValidationError:
        return {
            "output": _safe_error_output(),
            "trace": [*state.get("trace", []), "plan:rejected"],
            "plan": plan,
        }
    check_agent_permissions()
    trace = [*state.get("trace", []), "verification:received"]
    try:
        request = TypeAdapter(VerificationRequest).validate_python(state.get("payload", {}))
        if isinstance(request, GenerateVerificationQuestionsRequest):
            output = generate_questions(request)
            operation_trace = "verification:generate_questions"
        else:
            output = evaluate_answers(request)
            operation_trace = "verification:evaluate_answers"
    except ValidationError:
        return {
            "output": _safe_error_output(),
            "trace": [*trace, "verification:invalid_request"],
            "plan": plan,
        }
    except Exception:
        return {
            "output": {
                "recommendation": "manual_review",
                "error": "Verification processing failed safely.",
            },
            "trace": [*trace, "verification:failed"],
            "plan": plan,
        }
    return {
        "output": output,
        "trace": [*trace, operation_trace, "verification:completed"],
        "plan": plan,
    }
