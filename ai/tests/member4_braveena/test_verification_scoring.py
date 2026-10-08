"""Question-specific scoring, independent of the generation provider."""

import pytest

from app.agents.models import EvaluateVerificationAnswersRequest
from app.agents.verification import evaluate_answers, verification_node
from app.llm.fake import FakeLlmClient

EVIDENCE = (
    "Nike stainless-steel water bottle with a black screw cap. "
    "It has a small scratch near the bottom and a white sticker on one side."
)
CAP = "What color is the bottle cap?"


def request(pairs, evidence=EVIDENCE):
    return EvaluateVerificationAnswersRequest(
        operation="evaluate_answers", claim_id="scoring-regression",
        private_verification_details={"staff_observation": evidence},
        questions=[{"question_id": f"verification-{i+1}", "question": q}
                   for i, (q, _) in enumerate(pairs)],
        answers=[{"question_id": f"verification-{i+1}", "answer": a}
                 for i, (_, a) in enumerate(pairs) if a.strip()],
    )


@pytest.mark.parametrize("answer", [
    "Black", "The cap is black.", "It has a black cap.", "Black colour",
    "The bottle cap was black.", "Dark black",
])
def test_direct_and_equivalent_cap_answers_are_strong(answer):
    output = evaluate_answers(request([(CAP, answer)]))
    assert output["evaluations"] == [
        {"question_id": "verification-1", "result": "match", "score": 1.0}
    ]
    assert output["recommendation"] == "likely_match"
    assert EVIDENCE not in str(output)
    assert "black" not in str(output).lower()


@pytest.mark.parametrize("answer,result", [
    ("White", "no_match"), ("Blue", "no_match"), ("Nike", "insufficient"),
    ("I don’t know", "insufficient"), ("I don't know", "insufficient"),
    ("", "insufficient"), ("   ", "insufficient"),
    ("Black or white", "no_match"), ("Not black", "no_match"),
    ("Maybe black", "insufficient"), ("Nike black", "partial_match"),
    ("The sticker is black", "partial_match"),
])
def test_wrong_off_topic_and_uncertain_are_distinguished(answer, result):
    evaluation = evaluate_answers(request([(CAP, answer)]))["evaluations"][0]
    assert evaluation["result"] == result
    assert evaluation["score"] < .75


@pytest.mark.parametrize("question,answer", [
    ("What brand is the bottle?", "Nike"),
    ("Where is the noticeable scratch located?", "Near the bottom"),
    ("What color is the sticker?", "White"),
    ("Where is the sticker located?", "On one side"),
])
def test_each_question_resolves_its_own_fact(question, answer):
    evaluation = evaluate_answers(request([(question, answer)]))["evaluations"][0]
    assert evaluation["result"] == "match"
    assert evaluation["score"] > .75


@pytest.mark.parametrize("question,answer", [
    (CAP, "Nike"), ("What brand is the bottle?", "Black"),
    ("Where is the noticeable scratch located?", "White"),
])
def test_other_paragraph_facts_do_not_get_credit(question, answer):
    assert evaluate_answers(request([(question, answer)]))["evaluations"][0]["score"] == 0


@pytest.mark.parametrize("second,result,score", [
    ("White", "match", 1.0), ("Blue", "no_match", 0.0),
    ("I don't know", "insufficient", 0.0),
])
def test_multiple_questions_same_observation(second, result, score):
    output = evaluate_answers(request([(CAP, "Black"), ("What color is the sticker?", second)]))
    assert output["evaluations"][0]["score"] == 1.0
    assert output["evaluations"][1]["result"] == result
    assert output["evaluations"][1]["score"] == score
    assert output["recommendation"] == ("likely_match" if second == "White" else "manual_review")


@pytest.mark.parametrize("evidence,question,answer", [
    ("An Adidas shoe with a red sticker on the sole.", "What color is the sticker?", "Red"),
    ("A Samsung phone with a gray sticker on the back.", "What color is the sticker?", "Grey"),
    ("A backpack has a scratch near the zipper.", "Where is the noticeable scratch located?",
     "Near the zipper"),
])
def test_other_items_and_follow_up_observations(evidence, question, answer):
    assert evaluate_answers(request([(question, answer)], evidence))["evaluations"][0]["score"] == 1


def test_fake_provider_cannot_override_deterministic_evaluation():
    fake = FakeLlmClient()
    fake.queue_response({"evaluations": "malformed"})
    output = verification_node({"payload": request([(CAP, "Black")]).model_dump(), "trace": []},
                               llm_client=fake)
    assert output["output"]["evaluations"][0]["score"] == 1
    assert "verification:llm_attempt" not in output["trace"]


def test_descriptive_question_cannot_score_other_facts_in_same_clause():
    output = evaluate_answers(request([("What sticker or label does the item have?", "Nike")],
                                      "Nike bottle with a white sticker on one side."))
    assert output["evaluations"][0]["score"] == 0


def test_wrong_location_is_a_contradiction():
    output = evaluate_answers(request([
        ("Where is the noticeable scratch located?", "Near the top")
    ]))
    assert output["evaluations"][0]["result"] == "no_match"
