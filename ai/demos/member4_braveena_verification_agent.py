"""Member 4 - Braveena S (IT24100354): the Verification agent and the Coordinator agent.

Business component: claims and ownership verification. Staff record a hidden detail when an
item is logged. For a claim, the Verification agent drafts up to three questions about the
kind of detail (never its value), then grades the claimant's answers. The Coordinator agent
works out the next step and pauses the workflow until a staff member decides.

  python demos/member4_braveena_verification_agent.py           # enter the detail and answers
  python demos/member4_braveena_verification_agent.py --sample  # genuine owner vs a guesser
"""

from __future__ import annotations

from common import (
    AgentService,
    amber,
    bold,
    footer,
    green,
    header,
    parse_args,
    plan,
    plan_steps,
    prompt,
    red,
    row,
    section,
    trace,
)

HIDDEN = "Student card for Claire Perera behind the clear window"
RESULT = {"match": green("match"), "partial_match": amber("partial match"),
          "no_match": red("no match"), "insufficient": red("insufficient")}
VERDICT = {"likely_match": green("LIKELY MATCH"), "unlikely_match": red("UNLIKELY MATCH"),
           "manual_review": amber("MANUAL REVIEW")}
STAFF_ID = "3fa85f64-5717-4562-b3fc-2c963f66afa6"


def show(service: AgentService, title: str, hidden: str, answer_for, show_json: bool) -> None:
    from app.agents.plans import build_coordinator_plan, build_verification_plan

    header("Verification and Coordinator agents", "Member 4 · Braveena S (IT24100354)",
           "claims and ownership verification")
    section(f"Input: {title}")
    row("Hidden detail", f"{hidden}   {bold('(staff only)')}")

    drafted, ms1 = service.run("verification", {
        "operation": "generate_questions", "claim_id": "claim-demo",
        "private_verification_details": {"contents": hidden},
    })
    questions = drafted["output"]["questions"]
    section("Step 1 · Verification agent drafts questions")
    plan(plan_steps(build_verification_plan()), inline=True)
    for q in questions:
        row("Question", q["question"])
    leaked = any(w.lower() in " ".join(q["question"] for q in questions).lower()
                 for w in hidden.split() if len(w) > 4)
    row("Leaks detail?", red("YES") if leaked else green("no - asks about the kind of detail"))

    answers = [{"question_id": q["question_id"], "answer": answer_for(q)} for q in questions]
    graded, ms2 = service.run("verification", {
        "operation": "evaluate_answers", "claim_id": "claim-demo", "questions": questions,
        "private_verification_details": {"contents": hidden}, "answers": answers,
    })
    out = graded["output"]
    section("Step 2 · Verification agent grades the answers")
    for answer, evaluation in zip(answers, out.get("evaluations", []), strict=False):
        row("Answer", f"\"{answer['answer']}\"")
        row("Result", f"{RESULT.get(evaluation['result'], evaluation['result'])}"
                      f"   (overlap {evaluation['score']:.2f})")
    row("Recommends", VERDICT.get(out.get("recommendation"), out.get("recommendation")))

    status = "UnderReview"
    coordinated, ms3 = service.run("coordinator", {
        "workflow_id": "claim-demo", "workflow_type": "claim_verification",
        "claim_status": status, "verification_recommendation": out.get("recommendation"),
        "decision_status": "no_decision", "notification_state": "not_required",
    })
    co = coordinated["output"]
    section("Step 3 · Coordinator agent picks the next step")
    plan(plan_steps(build_coordinator_plan(co["requires_human_action"])), inline=True)
    row("Next step", co["recommended_action"])
    row("Needs a person", bold("yes") if co["requires_human_action"] else "no")
    row("Workflow", amber(coordinated["status"]))
    wid = coordinated["agent_run_id"]
    early = service.post(f"/agents/workflows/{wid}/resume", {"agent": "coordinator"})
    row("Resume alone?", f"refused with HTTP {early['status_code']} - a person must decide first")
    # In the app a staff member makes this call from the claim page; the demo plays that part.
    decision = "approved" if out.get("recommendation") == "likely_match" else "rejected"
    decided = service.post(f"/agents/workflows/{wid}/approval", {
        "agent": "coordinator", "decision": decision, "decision_maker_id": STAFF_ID})
    resumed = service.post(f"/agents/workflows/{wid}/resume", {"agent": "coordinator"})
    row("Staff decide", f"{decision} (HTTP {decided['status_code']})")
    after = resumed.get("output", {}).get("recommended_action") or (
        f"workflow stays stopped (HTTP {resumed['status_code']})")
    row("After resume", after)
    row("Who approves", bold("the staff member - no agent holds approval permission"))
    trace(graded)
    footer(service, ms1 + ms2 + ms3, coordinated, show_json)


def main() -> None:
    args = parse_args(
        "FoundU Verification and Coordinator agents demo",
        lambda p: p.add_argument("--sample", action="store_true",
                                 help="a genuine owner, then a guesser"),
    )
    service = AgentService(args.live)
    if args.sample:
        show(service, "the genuine owner", HIDDEN,
             lambda q: "my student card, Claire Perera, behind the clear window", args.json)
        show(service, "someone guessing", HIDDEN,
             lambda q: "cash coins card bank card id licence keys photo receipt", args.json)
        return
    hidden = prompt("Hidden detail staff recorded", HIDDEN)
    print(bold("\nAnswer each question as the claimant:"))
    show(service, "your claimant", hidden,
         lambda q: prompt(f"  {q['question']}"), args.json)


if __name__ == "__main__":
    main()
