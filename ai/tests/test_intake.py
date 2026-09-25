"""The intake conversation: deterministic without a model, grounded with one."""

from app.agents.intake import intake_node
from app.agents.models import AgentName, AgentRunRequest
from app.agents.state import create_initial_state
from app.llm.fake import FakeLlmClient

VOCAB = {
    "item_types": ["Water Bottle", "Backpack", "Student ID Card", "Keys"],
    "locations": ["Main Library", "Sports Center Gym", "Cafeteria"],
}


def run(payload, llm=None):
    state = create_initial_state(AgentRunRequest(agent=AgentName.INTAKE, payload=payload))
    return intake_node(state, llm_client=llm)["output"]


def test_first_message_fills_what_it_can_and_asks_for_the_rest():
    out = run(
        {
            "history": [{"role": "user", "text": "I lost my water bottle somewhere"}],
            "vocabulary": VOCAB,
        }
    )
    assert out["phase"] == "collecting"
    assert out["slots"]["item_type"] == "Water Bottle"
    assert "colour" in out["reply"].lower()


def test_item_and_colour_is_enough_to_search():
    out = run(
        {
            "history": [{"role": "user", "text": "it's a grey water bottle"}],
            "slots": {
                "item_type": None,
                "colour": None,
                "location": None,
                "when": None,
                "distinctive": None,
            },
            "vocabulary": VOCAB,
        }
    )
    assert out["phase"] == "ready_to_search"
    assert out["slots"] == {
        "item_type": "Water Bottle",
        "colour": "grey",
        "location": None,
        "when": None,
        "distinctive": None,
    }


def test_earlier_answers_are_not_overwritten_by_later_words():
    out = run(
        {
            "history": [{"role": "user", "text": "near the gym"}],
            "slots": {
                "item_type": "Backpack",
                "colour": "blue",
                "location": None,
                "when": None,
                "distinctive": None,
            },
            "vocabulary": VOCAB,
        }
    )
    assert out["slots"]["item_type"] == "Backpack"
    assert out["slots"]["location"] == "Sports Center Gym"


def test_candidates_pick_the_best_and_say_where_it_is():
    out = run(
        {
            "history": [{"role": "assistant", "text": "Let me check."}],
            "slots": {
                "item_type": "Water Bottle",
                "colour": "grey",
                "location": "Sports Center Gym",
                "when": None,
                "distinctive": None,
            },
            "vocabulary": VOCAB,
            "candidates": [
                {
                    "id": "c-keys",
                    "item_type": "Keys",
                    "colour": None,
                    "location": "Cafeteria",
                    "description": "A bunch of keys.",
                    "kind": "desk",
                },
                {
                    "id": "c-bottle",
                    "item_type": "Water Bottle",
                    "colour": "grey",
                    "location": "Sports Center Gym",
                    "description": "Grey bottle, stickers.",
                    "kind": "post",
                },
            ],
        }
    )
    assert out["phase"] == "matched"
    assert out["match_candidate_id"] == "c-bottle"
    assert out["match_confidence"] >= 0.9
    assert "not yet at a desk" in out["reply"]


def test_weak_candidates_are_an_honest_no_match():
    out = run(
        {
            "history": [{"role": "assistant", "text": "Let me check."}],
            "slots": {
                "item_type": "Water Bottle",
                "colour": "grey",
                "location": None,
                "when": None,
                "distinctive": None,
            },
            "vocabulary": VOCAB,
            "candidates": [
                {
                    "id": "c-keys",
                    "item_type": "Keys",
                    "colour": None,
                    "location": "Cafeteria",
                    "description": "Keys.",
                    "kind": "desk",
                }
            ],
        }
    )
    assert out["phase"] == "no_match"
    assert "drafted a lost report" in out["reply"]


def test_model_values_are_accepted_only_when_grounded():
    fake = FakeLlmClient()
    # The model "hallucinates" a colour the person never said and a place off-campus.
    fake.queue_response(
        {
            "item_type": "Backpack",
            "colour": "purple",
            "location": "Airport",
            "when": None,
            "distinctive": None,
        }
    )
    out = run(
        {"history": [{"role": "user", "text": "I lost my backpack"}], "vocabulary": VOCAB}, llm=fake
    )
    assert out["slots"]["item_type"] == "Backpack"
    assert out["slots"]["colour"] is None
    assert out["slots"]["location"] is None


def test_model_failure_never_stalls_the_conversation():
    class Broken:
        def generate_structured(self, request, response_model):
            raise RuntimeError("provider down")

    from app.llm.errors import LlmError

    class BrokenLlm:
        def generate_structured(self, request, response_model):
            raise LlmError("provider down")

    out = run(
        {
            "history": [{"role": "user", "text": "a blue backpack at the library"}],
            "vocabulary": VOCAB,
        },
        llm=BrokenLlm(),
    )
    assert out["phase"] == "ready_to_search"
    assert out["slots"]["colour"] == "blue"
