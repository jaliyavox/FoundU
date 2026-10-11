"""The support assistant: answers from the guide, and hands the desk what it cannot fix."""

from app.agents.models import AgentName, AgentRunRequest
from app.agents.state import create_initial_state
from app.agents.support import GUIDE, support_node
from app.llm.fake import FakeLlmClient


def run(history, context=None, last_topic=None, llm=None):
    payload = {"history": history}
    if context is not None:
        payload["context"] = context
    if last_topic is not None:
        payload["last_topic"] = last_topic
    state = create_initial_state(AgentRunRequest(agent=AgentName.SUPPORT, payload=payload))
    return support_node(state, llm_client=llm)["output"]


def user(text):
    return {"role": "user", "text": text}


def bot(text):
    return {"role": "assistant", "text": text}


def test_a_known_problem_is_answered_from_the_guide():
    out = run([user("Where do I find my collection code?")])

    assert out["phase"] == "answered"
    assert out["topic"] == "collection_code"
    assert "claim page" in out["reply"]
    assert out["ticket"] is None
    assert out["reply"].endswith("Did that solve it?")


def test_the_answer_mentions_the_persons_own_claim():
    out = run(
        [user("how do I collect my item")],
        context={"claims": [{"item": "Backpack", "status": "Approved"}]},
    )

    assert "your claim for the backpack is approved" in out["reply"]


def test_a_live_handover_is_named_with_the_time_left():
    out = run(
        [user("my handover code - how long do I have?")],
        context={
            "handovers": [
                {"item": "Wallet", "status": "AwaitingHandIn", "role": "finder", "hours_left": 30}
            ]
        },
    )

    assert out["topic"] == "handover_code"
    assert "about 30 hours left" in out["reply"]


def test_something_only_the_desk_can_fix_goes_straight_to_a_ticket():
    out = run([user("My account was suspended and I don't know why")])

    assert out["phase"] == "escalate"
    assert out["ticket"]["category"] == "Account"
    assert "account was suspended" in out["ticket"]["body"]
    assert "needs the desk" in out["ticket"]["body"]


def test_a_forgotten_password_is_answered_with_the_self_service_reset():
    out = run([user("I forgot my password and I'm locked out")])

    assert out["phase"] == "answered"
    assert out["topic"] == "cannot_sign_in"
    assert "Forgot password?" in out["reply"]


def test_that_did_not_help_escalates_the_topic_it_was_about():
    out = run(
        [
            user("where is my collection code"),
            bot("Once a claim is approved... Did that solve it?"),
            user("that didn't help"),
        ],
        last_topic="collection_code",
    )

    assert out["phase"] == "escalate"
    assert out["ticket"]["category"] == "Collection"
    # The ticket carries the problem, not the "didn't help".
    assert "where is my collection code" in out["ticket"]["body"]
    assert "that didn't help" not in out["ticket"]["body"]
    assert out["ticket"]["subject"] == "where is my collection code"


def test_asking_for_a_person_is_always_honoured():
    out = run([user("can I talk to a human please")])

    assert out["phase"] == "escalate"
    assert out["ticket"] is not None


def test_something_unknown_is_asked_about_once_more_then_passed_on():
    first = run([user("the thing with the stuff is weird")])
    assert first["phase"] == "clarify"

    second = run(
        [
            user("hello"),
            bot("Hi - tell me what is going wrong"),
            user("the thing with the stuff is weird"),
            bot(first["reply"]),
            user("it just looks odd"),
        ]
    )
    assert second["phase"] == "escalate"
    assert second["ticket"]["category"] == "Other"
    assert "no answer" in second["ticket"]["body"]


def test_the_model_may_only_pick_a_guide_entry_never_write_the_answer():
    llm = FakeLlmClient()
    llm.queue_response({"topic_id": "honor_points"})
    out = run([user("what are these stars on my profile")], llm=llm)
    assert out["topic"] == "honor_points"
    assert out["reply"].startswith(next(t.answer for t in GUIDE if t.id == "honor_points"))

    # An id the guide does not have is ignored; the keyword choice stands.
    llm.queue_response({"topic_id": "refund_policy"})
    out = run([user("where is my collection code")], llm=llm)
    assert out["topic"] == "collection_code"


def test_a_failing_model_never_stalls_the_answer():
    llm = FakeLlmClient()
    llm.queue_provider_failure()
    out = run([user("how do notifications work")], llm=llm)

    assert out["phase"] == "answered"
    assert out["topic"] == "notifications"


def test_every_reply_is_safe_to_show_and_every_draft_fits_a_ticket():
    for topic in GUIDE:
        out = run([user(" ".join(topic.keywords[:2]))])
        if out["ticket"]:
            assert 10 <= len(out["ticket"]["body"]) <= 4000
            assert 1 <= len(out["ticket"]["subject"]) <= 200
