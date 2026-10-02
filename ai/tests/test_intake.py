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
        "intent": None,
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
    assert "lost report draft" in out["reply"]


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


# ------------------------------------------------------------------ finders


def test_a_finder_is_recognised_and_asked_where_they_found_it():
    out = run(
        {
            "history": [{"role": "user", "text": "I found a water bottle"}],
            "vocabulary": VOCAB,
        }
    )
    assert out["slots"]["intent"] == "found"
    assert out["phase"] == "collecting"
    assert "colour" in out["reply"].lower()

    # Asked for the place, the finder is asked where they found it - not where they left it.
    out = run(
        {
            "history": [{"role": "user", "text": "no idea of the colour"}],
            "slots": {"item_type": "Water Bottle", "intent": "found"},
            "vocabulary": VOCAB,
        }
    )
    assert "colour" in out["reply"].lower()


def test_the_side_that_comes_first_wins():
    from app.agents.intake import _detect_intent

    assert _detect_intent("I lost my wallet and someone found it") == "lost"
    assert _detect_intent("Someone left a jacket in the gym") == "found"
    assert _detect_intent("just picked up a set of keys") == "found"
    assert _detect_intent("my keys are missing") == "lost"
    assert _detect_intent("a blue bottle") is None


def test_a_finder_does_not_turn_into_an_owner_mid_conversation():
    out = run(
        {
            "history": [{"role": "user", "text": "I left it at the library desk already"}],
            "slots": {"item_type": "Keys", "colour": "silver", "intent": "found"},
            "vocabulary": VOCAB,
        }
    )
    assert out["slots"]["intent"] == "found"


def test_a_finder_is_pointed_at_the_owner_who_reported_it():
    out = run(
        {
            "history": [{"role": "assistant", "text": "Search the available candidates."}],
            "slots": {
                "item_type": "Water Bottle",
                "colour": "blue",
                "location": "Cafeteria",
                "intent": "found",
            },
            "vocabulary": VOCAB,
            "candidates": [
                {
                    "id": "report-1",
                    "item_type": "Water Bottle",
                    "colour": "blue",
                    "location": "Cafeteria",
                    "description": "Blue steel bottle with a dent",
                    "kind": "lost",
                }
            ],
        }
    )
    assert out["phase"] == "matched"
    assert out["match_candidate_id"] == "report-1"
    assert "I found this" in out["reply"]


def test_a_finder_with_no_owner_yet_is_told_to_post_it_or_hand_it_in():
    out = run(
        {
            "history": [{"role": "assistant", "text": "Search the available candidates."}],
            "slots": {"item_type": "Keys", "colour": "silver", "intent": "found"},
            "vocabulary": VOCAB,
            "candidates": [],
        }
    )
    assert out["phase"] == "no_match"
    assert "found item" in out["reply"]
    assert "security desk" in out["reply"]


def test_the_first_colour_named_is_the_items_and_gray_is_grey():
    def colour_of(text):
        return run(
            {
                "history": [{"role": "user", "text": text}],
                "slots": dict.fromkeys(("item_type", "colour", "location", "when", "distinctive")),
                "vocabulary": VOCAB,
            }
        )["slots"]["colour"]

    assert colour_of("a blue water bottle with black stickers") == "blue"
    assert colour_of("a gray water bottle") == "grey"


def test_a_name_inside_another_word_is_not_a_match():
    vocab = {"item_types": ["Phone", "Headphones", "Earphones", "Keys"], "locations": ["Library"]}
    out = run({"history": [{"role": "user", "text": "I lost my headphone"}], "vocabulary": vocab})
    assert out["slots"]["item_type"] == "Headphones"

    out = run({"history": [{"role": "user", "text": "I lost my phone"}], "vocabulary": vocab})
    assert out["slots"]["item_type"] == "Phone"

    out = run({"history": [{"role": "user", "text": "lost my earphones"}], "vocabulary": vocab})
    assert out["slots"]["item_type"] == "Earphones"
