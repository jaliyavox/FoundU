"""Regression: every public question must be supported by an original observation."""

from pathlib import Path

import pytest

from app.agents.state import AgentState
from app.agents.verification import verification_node
from app.agents.verification_grounding import candidate_questions, is_grounded
from app.llm.fake import FakeLlmClient


def test_api_grounding_rules_match_python_rules():
    repository = Path(__file__).resolve().parents[2]
    python_rules = repository / "ai/app/agents/verification_grounding.json"
    api_rules = repository / "api/src/FoundU.Infrastructure/Claims/verification_grounding.json"
    assert api_rules.read_bytes() == python_rules.read_bytes()


BOTTLE = (
    "Nike stainless steel water bottle with a black screw cap. "
    "It has a small scratch near the bottom and a white sticker on one side."
)
SAFE = [
    "What brand is the bottle?",
    "What color is the bottle cap?",
    "Is there any noticeable scratch or damage on the bottle?",
    "Where is the noticeable scratch located?",
    "Is there any sticker or label on the bottle?",
    "What color is the sticker?",
    "Where is the sticker located?",
]
UNSAFE = [
    "What identifying mark is underneath the bottle cap?",
    "What is written on the white sticker?",
    "What text is on the sticker?",
    "What shape is the sticker?",
    "What is inside the bottle?",
    "Where is the hidden compartment?",
    "What engraving does the bottle have?",
    "What is the serial number?",
    "What color is the cap and where is the sticker?",
    "Where is the scratch behind the sticker?",
]


def draft(detail, question):
    fake = FakeLlmClient()
    fake.queue_response({"questions": [
        {"question_id": "verification-1", "question_text": question}
    ]})
    return verification_node(AgentState(payload={
        "operation": "generate_questions", "claim_id": "claim-1",
        "private_verification_details": {"staff_observation": detail},
    }, trace=[]), llm_client=fake)


@pytest.mark.parametrize("question", SAFE)
def test_supported_atomic_ai_questions_pass_validation(question):
    result = draft(BOTTLE, question)
    assert "verification:llm_success" in result["trace"]
    assert result["output"]["questions"][0]["question"] == question
    assert BOTTLE not in str(result)
    assert all(value not in str(result["output"]) for value in ["Nike", "black", "white"])


@pytest.mark.parametrize("question", UNSAFE)
def test_unsupported_ai_questions_are_replaced_by_grounded_fallback(question):
    result = draft(BOTTLE, question)
    assert "verification:fallback" in result["trace"]
    fallback = result["output"]["questions"][0]["question"]
    assert fallback == "What color is the bottle cap?"
    assert is_grounded(fallback, BOTTLE)
    assert question != fallback


@pytest.mark.parametrize("detail,safe,unsafe", [
    ("A Samsung phone with a blue sticker on the back.",
     "What color is the sticker?", "What is written on the sticker?"),
    ("A backpack has a scratch near the zipper and a red strap.",
     "Where is the noticeable scratch located?", "What mark is underneath the strap?"),
    ("An Adidas shoe with a white sticker on the sole.",
     "Where is the sticker located?", "What shape is the sticker?"),
])
def test_other_items_are_grounded(detail, safe, unsafe):
    assert "verification:llm_success" in draft(detail, safe)["trace"]
    assert "verification:fallback" in draft(detail, unsafe)["trace"]


@pytest.mark.parametrize("detail", [
    "A white cap and a sticker.",
    "A white cap. The sticker is on the back.",
    "A cap with no sticker.",
    "A cap with possibly a white sticker.",
    "A white sticker-less cap.",
    "A cap lacking a white sticker.",
])
def test_attributes_cannot_move_between_clauses_or_negated_observations(detail):
    assert not is_grounded("What color is the sticker?", detail)


def test_location_is_not_inferred_from_independent_facts():
    assert not is_grounded("Where is the sticker located?", "A sticker and a scratch on the back.")
    assert not is_grounded(
        "Where is the noticeable scratch located?", "A scratch. A cap near the bottom."
    )
    assert set(SAFE).issubset(candidate_questions(BOTTLE))


def test_brand_and_bottle_cap_relationships_are_not_inferred():
    assert not is_grounded("What brand is the item?", "Nike is written on a sticker.")
    assert not is_grounded("What color is the bottle cap?", "A bottle. A black cap on a pen.")
    assert not is_grounded("What brand is the item?", "A bag with an apple inside.")
