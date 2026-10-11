"""Member 3 - Uthpala W.A.S (IT24101028): the Matching agent.

Business component: found item management and matching. Staff pick a lost report for a found
item; the agent reads both through its two allowed tools and scores the public evidence.
Different item types never match. Then: colour 20, identifying description 35, campus place 20
and time sequence 25 points. 0.65 or more with matching details and no conflict is a candidate;
anything uncertain goes to staff for manual review.

  python demos/member3_uthpala_matching_agent.py            # enter both reports
  python demos/member3_uthpala_matching_agent.py --sample   # three prepared pairs
"""

from __future__ import annotations

from common import (
    AgentService,
    amber,
    bold,
    bullet,
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


def report(rid, item_type, colour, description, location, start, end):
    return {
        "report_id": rid,
        "item_type": item_type,
        "primary_color": colour or None,
        "description": description or None,
        "location": location or None,
        "event_start_at": start or None,
        "event_end_at": end or None,
    }


LOST = report(
    "lost-25", "Water Bottle", "Blue", "blue bottle with a NASA sticker and a dent",
    "Library", "2026-10-01T08:00:00Z", "2026-10-01T12:00:00Z",
)
FOUND = report(
    "found-81", "Water Bottle", "Blue", "blue water bottle, NASA sticker, dented bottom",
    "Library", "2026-10-01T13:00:00Z", "2026-10-01T13:00:00Z",
)
SAMPLES = [
    ("Same bottle, same place, found soon after", LOST, FOUND),
    ("Same type and colour only, nothing else known",
     report("lost-26", "Backpack", "Black", "", "", "", ""),
     report("found-82", "Backpack", "Black", "", "", "", "")),
    ("Different item types", LOST, {**FOUND, "report_id": "found-83", "item_type": "Umbrella"}),
]
VERDICT = {
    "match_candidate": green("MATCH CANDIDATE - shown to the student as a possible match"),
    "manual_review": amber("MANUAL REVIEW - staff decide whether to suggest it"),
    "no_match": red("NO MATCH - nothing is shown to the student"),
}


def describe(label: str, r: dict) -> None:
    row(label, f"{r['item_type']} · {r['primary_color'] or 'colour ?'} · "
        f"{r['location'] or 'place ?'}")
    row("", f"\"{r['description'] or ''}\"")
    row("", f"{r['event_start_at'] or '?'}  to  {r['event_end_at'] or '?'}")


def show(service: AgentService, title: str, lost: dict, found: dict, show_json: bool) -> None:
    from app.agents.plans import build_matching_plan

    body, ms = service.run(
        "matching", {"operation": "match_reports", "lost_report": lost, "found_report": found}
    )
    out = body["output"]
    header("Matching agent", "Member 3 · Uthpala W.A.S (IT24101028)",
           "found item management and matching")
    section(f"Input: {title}")
    describe("Lost report", lost)
    describe("Found item", found)
    plan(plan_steps(build_matching_plan(True)))
    section("Output (a recommendation, never a decision)")
    row("Score", f"{out['score']:.2f} of 1.00")
    row("Verdict", VERDICT.get(out["recommendation"], out["recommendation"]))
    for title_, key, mark in (("Matched", "matched_factors", "+"),
                              ("Missing", "missing_factors", "?"),
                              ("Conflicting", "conflicting_factors", "x")):
        if out.get(key):
            row(title_, "")
            for factor in out[key]:
                bullet(factor, mark=mark)
    trace(body)
    footer(service, ms, body, show_json)


def ask_report(label: str, rid: str, default: dict) -> dict:
    print(bold(f"\n{label} (press Enter to keep the example value)"))
    return report(
        rid,
        prompt("  Item type", default["item_type"]),
        prompt("  Colour", default["primary_color"]),
        prompt("  Description", default["description"]),
        prompt("  Campus place", default["location"]),
        prompt("  From (ISO time)", default["event_start_at"]),
        prompt("  To (ISO time)", default["event_end_at"]),
    )


def main() -> None:
    args = parse_args(
        "FoundU Matching agent demo",
        lambda p: p.add_argument("--sample", action="store_true", help="three prepared pairs"),
    )
    service = AgentService(args.live)
    if args.sample:
        for title, lost, found in SAMPLES:
            show(service, title, lost, found, args.json)
        return
    lost = ask_report("Lost report", "lost-1", LOST)
    found = ask_report("Found item", "found-1", FOUND)
    show(service, "your reports", lost, found, args.json)


if __name__ == "__main__":
    main()
