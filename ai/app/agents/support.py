"""Support Agent: the first answer to "something is wrong", before anybody opens a ticket.

A student describes a problem; the agent answers from FoundU's own help guide when the guide
covers it, and drafts a ticket for the desk when it does not. Three outcomes, and ASP.NET acts
on the phase:

- ``answered``  - a guide entry fits. The reply is that entry's text, never model prose.
- ``clarify``   - nothing fits yet. Asks once for more detail.
- ``escalate``  - the guide cannot fix it, the person asked for a human, or a second try still
  matched nothing. The output carries a ticket draft the person reviews and sends.

The language model, when present, does exactly one bounded job: choose which guide entry (by
id) a message is about. It cannot write an answer, so it cannot invent a policy, a deadline or
a promise the desk never made. When it is absent or fails, keyword scoring chooses.

ASP.NET may pass a short summary of the person's own records - item names and statuses, never
a code, an answer or anything from another student - so an answer can say "your claim for the
black backpack is approved" instead of explaining every status in general.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, ValidationError

from app.agents.models import AgentName
from app.agents.plans import build_support_plan, validate_agent_plan
from app.agents.state import AgentState
from app.llm.client import LlmClient
from app.llm.errors import LlmError
from app.llm.models import StructuredGenerationRequest

TicketCategory = Literal[
    "Account", "LostReport", "FoundItem", "Claim", "Collection", "Technical", "Other"
]

# ------------------------------------------------------------------ contracts


class SupportTurn(BaseModel):
    model_config = ConfigDict(extra="forbid")

    role: Literal["user", "assistant"]
    text: str = Field(min_length=1, max_length=1000)


class ClaimSummary(BaseModel):
    model_config = ConfigDict(extra="forbid")

    item: str = Field(max_length=80)
    status: str = Field(max_length=40)


class ReportSummary(BaseModel):
    model_config = ConfigDict(extra="forbid")

    item: str = Field(max_length=80)
    status: str = Field(max_length=40)
    paused: bool = False


class HandoverSummary(BaseModel):
    model_config = ConfigDict(extra="forbid")

    item: str = Field(max_length=80)
    status: str = Field(max_length=40)
    role: Literal["finder", "owner"]
    hours_left: int | None = Field(default=None, ge=0, le=1000)


class AccountContext(BaseModel):
    """The person's own records, as names and statuses only."""

    model_config = ConfigDict(extra="forbid")

    claims: list[ClaimSummary] = Field(default_factory=list, max_length=20)
    reports: list[ReportSummary] = Field(default_factory=list, max_length=20)
    handovers: list[HandoverSummary] = Field(default_factory=list, max_length=20)


class SupportRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    history: list[SupportTurn] = Field(min_length=1, max_length=40)
    context: AccountContext = Field(default_factory=AccountContext)
    # The guide entry the previous answer came from, so "that didn't help" escalates it.
    last_topic: str | None = Field(default=None, max_length=40)


class TicketDraft(BaseModel):
    model_config = ConfigDict(extra="forbid")

    subject: str = Field(min_length=1, max_length=200)
    category: TicketCategory
    body: str = Field(min_length=10, max_length=4000)


class SupportResult(BaseModel):
    model_config = ConfigDict(extra="forbid")

    phase: Literal["answered", "clarify", "escalate"]
    reply: str
    topic: str | None = None
    ticket: TicketDraft | None = None


class TopicPick(BaseModel):
    """Strict, bounded model output: one guide id or none - never prose."""

    model_config = ConfigDict(extra="forbid", strict=True)

    topic_id: str | None = Field(default=None, max_length=40)


# ------------------------------------------------------------------ the guide


@dataclass(frozen=True)
class Topic:
    id: str
    title: str
    category: TicketCategory
    keywords: tuple[str, ...]
    answer: str
    # The guide can explain it, but only a person at the desk can fix it.
    needs_staff: bool = False
    extra: tuple[str, ...] = field(default_factory=tuple)


GUIDE: tuple[Topic, ...] = (
    Topic(
        id="collection_code",
        title="Collecting an approved item",
        category="Collection",
        keywords=("collection code", "collect", "pick up", "pickup", "collect my"),
        answer=(
            "Once a claim is approved, your collection code is on the claim page under My "
            "claims. Bring it and your student ID to the desk shown there. Each code works "
            "once, and only for you - staff check your ID before handing anything over."
        ),
    ),
    Topic(
        id="claim_status",
        title="What a claim status means",
        category="Claim",
        keywords=("claim", "pending", "under review", "status", "approved", "waiting"),
        answer=(
            "A claim moves from Pending to Under review once you have answered its questions, "
            "then staff approve or reject it. If staff ask for a revision you can answer again "
            "from the claim page. You get a notification each time it changes."
        ),
    ),
    Topic(
        id="claim_rejected",
        title="A claim was rejected",
        category="Claim",
        keywords=("rejected", "reject", "denied", "declined", "wrong decision", "appeal"),
        answer=(
            "A rejection means staff could not confirm ownership from your answers. If you "
            "think the decision is wrong, the desk can look at it again - that needs a person, "
            "so I can send them a ticket with the details."
        ),
        needs_staff=True,
    ),
    Topic(
        id="verification_questions",
        title="Answering verification questions",
        category="Claim",
        keywords=("question", "questions", "verification", "verify", "prove", "answer"),
        answer=(
            "The questions ask about things only the owner would know, like what was inside "
            "or a mark on it. Answer from memory, in your own words - a close description is "
            "fine, and guessing does more harm than saying you are not sure. Never share the "
            "answers with anyone, including in a ticket."
        ),
    ),
    Topic(
        id="handover_code",
        title="Handing an item to security with a code",
        category="FoundItem",
        keywords=("handover", "hand over", "hand in", "hand it in", "security", "48", "expired"),
        answer=(
            "When you press I found this and choose to hand it to security, you and the owner "
            "both get a six-digit code. Take the item to any security desk within 48 hours and "
            "quote the code. The report is paused on the feed meanwhile; if the time runs out "
            "it simply goes back up, and you can start again from the report."
        ),
    ),
    Topic(
        id="found_item",
        title="Found something",
        category="FoundItem",
        keywords=("found", "picked up", "someone left", "post found", "found item"),
        answer=(
            "Thank you for picking it up. If someone has reported it, open their report on the "
            "lost feed and press I found this. If nobody has, use Post a found item so the "
            "owner can spot it - or simply hand it in at any security desk."
        ),
    ),
    Topic(
        id="lost_report",
        title="Reporting, editing or withdrawing a lost item",
        category="LostReport",
        keywords=("report", "lost", "edit", "withdraw", "delete my report", "post lost"),
        answer=(
            "Report it from My reports with Report a lost item - or describe it to Ask FoundU "
            "and it fills the form in for you. You can edit or withdraw a report from its page "
            "while it is open. A withdrawn report comes off the feed straight away."
        ),
    ),
    Topic(
        id="account_details",
        title="Changing name, email or password",
        category="Account",
        keywords=("change my name", "name", "email", "change password", "update password"),
        answer=(
            "Open Account settings from your profile menu. You can change your name there; an "
            "email change asks for your current password; changing your password signs you out "
            "everywhere else."
        ),
    ),
    Topic(
        id="cannot_sign_in",
        title="Cannot sign in",
        category="Account",
        keywords=("forgot", "reset", "locked out", "can't log in", "cannot log in", "sign in"),
        answer=(
            "If you signed up with Google, use Continue with Google. A forgotten password has "
            "to be reset by the desk after they check who you are - I can send them a ticket."
        ),
        needs_staff=True,
    ),
    Topic(
        id="suspended",
        title="Account suspended",
        category="Account",
        keywords=("suspended", "banned", "blocked", "disabled"),
        answer=(
            "Suspensions are decided by the desk, and only they can review one. I can send them "
            "a ticket so they can tell you why and what happens next."
        ),
        needs_staff=True,
    ),
    Topic(
        id="item_problem",
        title="A problem with a returned item",
        category="Collection",
        keywords=("damaged", "broken", "missing from", "not mine", "someone else collected",
                  "wrong item", "stolen"),
        answer=(
            "I am sorry - that needs someone at the desk to look into it. I can send them a "
            "ticket with what you have told me."
        ),
        needs_staff=True,
    ),
    Topic(
        id="notifications",
        title="Notifications",
        category="Technical",
        keywords=("notification", "notifications", "alert", "not notified", "push"),
        answer=(
            "Everything that changes on your reports and claims lands in the bell at the top. "
            "On the phone app, push alerts also need notifications allowed for FoundU in your "
            "phone's settings."
        ),
    ),
    Topic(
        id="honor_points",
        title="Honor points",
        category="Other",
        keywords=("honor", "honour", "points", "help to find", "reward"),
        answer=(
            "You earn honor points for handing items in at a desk and more when one you found "
            "reaches its owner. Your total and history are on Help to find."
        ),
    ),
)

_BY_ID = {topic.id: topic for topic in GUIDE}

# A person asking for a person, or saying the answer did not work.
_HUMAN_PHRASES = (
    r"\b(?:talk|speak) to (?:a |an |some)?(?:one|body|human|person|staff|admin)",
    r"\b(?:real|actual) person\b",
    r"\bhuman\b",
    r"\bopen (?:a )?ticket\b",
    r"\bcontact (?:the )?(?:desk|staff|admin|support)\b",
    r"\b(?:did ?n'?t|does ?n'?t|not) (?:help|work|solve|fix)",
    r"\bstill (?:not|doesn'?t|can'?t|broken|stuck)\b",
)

_GREETINGS = ("hi", "hello", "hey", "thanks", "thank you", "ok", "okay")


# ------------------------------------------------------------------ choosing


def _score(topic: Topic, lowered: str) -> int:
    score = 0
    for keyword in topic.keywords:
        if re.search(rf"\b{re.escape(keyword)}\b", lowered):
            # Longer phrases are more specific than single words.
            score += 2 if " " in keyword else 1
    return score


def _keyword_pick(text: str) -> Topic | None:
    lowered = text.lower()
    scored = [(topic, _score(topic, lowered)) for topic in GUIDE]
    best = max(scored, key=lambda pair: pair[1])
    return best[0] if best[1] > 0 else None


def wants_a_person(text: str) -> bool:
    lowered = text.lower()
    return any(re.search(pattern, lowered) for pattern in _HUMAN_PHRASES)


# ------------------------------------------------------------------ wording


def _personal_line(topic: Topic, context: AccountContext) -> str:
    """One sentence about the person's own records, when the topic is about one of them."""
    if topic.id in {"collection_code", "claim_status", "claim_rejected"}:
        if not context.claims:
            return "I can't see any claims on your account yet."
        parts = [f"your claim for the {c.item.lower()} is {_status_words(c.status)}"
                 for c in context.claims[:3]]
        return "Right now " + "; ".join(parts) + "."
    if topic.id == "handover_code":
        live = [h for h in context.handovers if h.status == "AwaitingHandIn"]
        if not live:
            return ""
        h = live[0]
        left = f", with about {h.hours_left} hours left" if h.hours_left is not None else ""
        who = "you are taking" if h.role == "finder" else "a finder is bringing in"
        return f"Right now {who} the {h.item.lower()} to a desk{left}."
    if topic.id == "lost_report":
        paused = [r for r in context.reports if r.paused]
        if paused:
            return (
                f"Your report for the {paused[0].item.lower()} is paused because a finder is "
                "taking it to a desk - that is good news, not a fault."
            )
    return ""


def _status_words(status: str) -> str:
    return {
        "Pending": "pending",
        "WaitingForAnswer": "waiting for your answers",
        "UnderReview": "under review",
        "RevisionRequested": "waiting for a revision from you",
        "Approved": "approved - the collection code is on the claim page",
        "Rejected": "rejected",
        "Cancelled": "cancelled",
        "ManualReviewRequired": "with staff for a closer look",
    }.get(status, status.lower())


def _draft(history: list[SupportTurn], topic: Topic | None) -> TicketDraft:
    said = [turn.text.strip() for turn in history if turn.role == "user"]
    # The person's own words, without the "that didn't help" at the end.
    problem = [line for line in said if not wants_a_person(line)] or said
    first = problem[0]
    subject = first if len(first) <= 80 else first[:77].rstrip() + "..."
    tried = (
        f'The assistant suggested: "{topic.title}" - it did not solve it.'
        if topic and not topic.needs_staff
        else (
            f"The assistant recognised this as: {topic.title}. It needs the desk."
            if topic
            else "The assistant had no answer for this."
        )
    )
    body = (
        "Raised through the FoundU assistant.\n\n"
        "What I said:\n" + "\n".join(f"- {line}" for line in problem[-5:]) + f"\n\n{tried}"
    )
    return TicketDraft(
        subject=subject or "Help with FoundU",
        category=topic.category if topic else "Other",
        body=body[:4000],
    )


def _escalate(history: list[SupportTurn], topic: Topic | None, lead: str) -> SupportResult:
    return SupportResult(
        phase="escalate",
        reply=lead + " Check the ticket below - send it and someone on the desk will pick it up.",
        topic=topic.id if topic else None,
        ticket=_draft(history, topic),
    )


# ------------------------------------------------------------------ node


def support_node(state: AgentState, llm_client: LlmClient | None = None) -> AgentState:
    plan = build_support_plan()
    validate_agent_plan(plan, state.get("requested_agent", AgentName.SUPPORT))
    return {**_run_support(state, llm_client), "plan": plan}


def _run_support(state: AgentState, llm_client: LlmClient | None) -> AgentState:
    trace = [*state["trace"], "executed:support"]
    try:
        request = SupportRequest.model_validate(state["payload"])
    except ValidationError:
        return {"output": {"error": "invalid_request"}, "error": "invalid_request", "trace": trace}

    history = request.history
    latest = history[-1]
    if latest.role != "user":
        return {"output": {"error": "invalid_request"}, "error": "invalid_request", "trace": trace}

    previous_topic = _BY_ID.get(request.last_topic or "")
    asked_before = sum(1 for turn in history[:-1] if turn.role == "assistant")

    if wants_a_person(latest.text):
        topic = _keyword_pick(latest.text) or previous_topic
        result = _escalate(history, topic, "Of course - this needs a person.")
        return {"output": result.model_dump(), "trace": [*trace, "support:escalate:asked"]}

    topic = _keyword_pick(latest.text)
    if llm_client is not None:
        try:
            pick = llm_client.generate_structured(
                StructuredGenerationRequest(
                    operation="support_topic",
                    system_instruction=(
                        "Choose the one help-guide entry this student's message is about, by "
                        "id, or null if none fits. Only ids from the list are allowed."
                    ),
                    input={
                        "message": latest.text,
                        # Keywords as well as titles: "pickup code" and "handover code"
                        # sound alike, and the words each entry is about tell them apart.
                        "topics": [
                            {"id": t.id, "title": t.title, "about": list(t.keywords)} for t in GUIDE
                        ],
                    },
                    correlation_id=state.get("correlation_id"),
                ),
                TopicPick,
            )
            # Only a known id counts; anything else leaves the keyword choice standing.
            if pick.topic_id in _BY_ID:
                topic = _BY_ID[pick.topic_id]
                trace.append("support:llm_picked")
        except (LlmError, ValidationError):
            trace.append("support:llm_unavailable")

    lowered = latest.text.strip().lower().rstrip("!.")
    if topic is None and lowered in _GREETINGS:
        reply = (
            "Hi - tell me what is going wrong, in a sentence or two, and I will either sort it "
            "or pass it to the desk."
        )
        result = SupportResult(phase="clarify", reply=reply)
        return {"output": result.model_dump(), "trace": [*trace, "support:greeting"]}

    if topic is None:
        if asked_before >= 2:
            result = _escalate(history, None, "I don't have an answer for that one.")
            return {"output": result.model_dump(), "trace": [*trace, "support:escalate:unknown"]}
        reply = (
            "I'm not sure I follow yet. What were you trying to do, and what happened instead? "
            "For example: \"my collection code is not accepted at the desk\"."
        )
        result = SupportResult(phase="clarify", reply=reply)
        return {"output": result.model_dump(), "trace": [*trace, "support:clarify"]}

    personal = _personal_line(topic, request.context)
    if topic.needs_staff:
        lead = topic.answer + (f" {personal}" if personal else "")
        result = _escalate(history, topic, lead)
        return {"output": result.model_dump(), "trace": [*trace, f"support:escalate:{topic.id}"]}

    reply = topic.answer + (f" {personal}" if personal else "") + " Did that solve it?"
    result = SupportResult(phase="answered", reply=reply, topic=topic.id)
    return {"output": result.model_dump(), "trace": [*trace, f"support:answered:{topic.id}"]}
