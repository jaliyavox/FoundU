"""Turns one member's raw test reports into a readable summary.

Reads whatever run_member_tests.sh produced in the results folder (xUnit TRX, JUnit XML from
pytest, Vitest, Playwright and Newman, flutter_test JSON, the k6 summary export), prints a
totals table with one row per testing area and writes SUMMARY.md listing every test case with
its result.

    python testing/summarize_member_results.py testing/member1_jaliya/results "Jaliya H. A. W"
"""

from __future__ import annotations

import json
import sys
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path

# key, label, report files (any that exist are read)
PARTS = [
    ("api", "1 Backend / API (xUnit)", ["api.trx"]),
    ("db", "2 Database (PostgreSQL)", ["db.trx"]),
    ("web", "3 React web (Vitest)", ["web.xml"]),
    ("mobile", "4 Flutter mobile", ["mobile.json"]),
    ("e2e", "5 Integration / E2E", ["e2e.xml", "e2e-web.xml"]),
    ("security", "6a Security (Newman)", ["security.xml"]),
    ("perf", "6b Performance (k6)", ["perf.json"]),
    ("a11y", "6c Accessibility (axe)", ["a11y.xml"]),
    ("ai", "7 Agentic AI (pytest)", ["ai.xml"]),
]


def trx_cases(path: Path) -> list[tuple[str, str]]:
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    root = ET.parse(path).getroot()
    cases = []
    for result in root.iterfind(".//t:UnitTestResult", ns):
        name = result.get("testName", "")
        outcome = {"Passed": "Pass", "Failed": "Fail", "NotExecuted": "Skipped"}.get(
            result.get("outcome", ""), result.get("outcome", "")
        )
        cases.append((name.removeprefix("FoundU.Tests."), outcome))
    return cases


def junit_cases(path: Path) -> list[tuple[str, str]]:
    root = ET.parse(path).getroot()
    cases = []
    suites = [root] if root.tag == "testsuite" else list(root.iter("testsuite")) or [root]
    for suite, case in ((s, c) for s in suites for c in s.findall("testcase")):
        name = case.get("name", "")
        owner = case.get("classname", "")
        # Newman names each suite "<folder> / <request>": the request is the test case's owner.
        if " / " in suite.get("name", ""):
            owner = suite.get("name").split(" / ", 1)[1]
            label = f"{owner} › {name}"
        elif owner.endswith((".ts", ".js")):  # Playwright: the spec file
            label = f"{Path(owner).name} › {name}"
        else:
            label = f"{owner.split('.')[-1]} › {name}" if owner else name
        if case.find("failure") is not None or case.find("error") is not None:
            outcome = "Fail"
        elif case.find("skipped") is not None:
            outcome = "Skipped"
        else:
            outcome = "Pass"
        cases.append((label, outcome))
    return cases


def flutter_cases(path: Path) -> list[tuple[str, str]]:
    names, outcomes = {}, {}
    for line in path.read_text().splitlines():
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        if event.get("type") == "testStart":
            test = event["test"]
            if not test.get("name", "").startswith("loading "):
                names[test["id"]] = test["name"]
        elif event.get("type") == "testDone" and event["testID"] in names:
            if event.get("hidden"):
                names.pop(event["testID"], None)
                continue
            outcomes[event["testID"]] = (
                "Skipped" if event.get("skipped") else
                "Pass" if event.get("result") == "success" else "Fail"
            )
    return [(names[i], outcomes.get(i, "Fail")) for i in names]


def k6_cases(path: Path) -> list[tuple[str, str]]:
    """Each k6 threshold is a case. In the summary export, true means the threshold was crossed."""
    metrics = json.loads(path.read_text())["metrics"]
    return [
        (f"{metric} {rule}", "Fail" if crossed else "Pass")
        for metric, values in sorted(metrics.items())
        for rule, crossed in values.get("thresholds", {}).items()
        if not rule.startswith("count>")
    ]


def k6_note(path: Path) -> str:
    metrics = json.loads(path.read_text())["metrics"]
    p95 = metrics["http_req_duration"]["p(95)"]
    failed = metrics["http_req_failed"].get("value", 0) * 100
    count = metrics["http_reqs"]["count"]
    return f"{count} requests, p95 {p95:.1f} ms, {failed:.2f}% failed"


def main() -> int:
    out, member = Path(sys.argv[1]), sys.argv[2]
    reader_for = {".trx": trx_cases, ".xml": junit_cases, ".json": flutter_cases}
    rows, details, failed = [], [], 0
    for key, label, files in PARTS:
        reports = [out / f for f in files if (out / f).exists()]
        if reports:
            cases = []
            for report in reports:
                reader = k6_cases if key == "perf" else reader_for[report.suffix]
                cases += reader(report)
            passed = sum(1 for _, o in cases if o == "Pass")
            fails = sum(1 for _, o in cases if o == "Fail")
            skipped = sum(1 for _, o in cases if o == "Skipped")
            failed += fails
            note = k6_note(reports[0]) if key == "perf" else "ran"
            rows.append((label, len(cases), passed, fails, skipped, note))
            details.append((label, cases))
        elif (out / f"{key}.skipped").exists():
            rows.append((label, 0, 0, 0, 0, "skipped: " + (out / f"{key}.skipped").read_text().strip()))
        else:
            rows.append((label, 0, 0, 0, 0, "not run"))

    print(f"{'Part':<28}{'Total':>7}{'Pass':>7}{'Fail':>7}{'Skip':>7}  Note")
    for label, total, passed, fails, skipped, note in rows:
        print(f"{label:<28}{total:>7}{passed:>7}{fails:>7}{skipped:>7}  {note}")
    total = sum(r[1] for r in rows)
    passed = sum(r[2] for r in rows)
    print(f"{'All parts':<28}{total:>7}{passed:>7}{failed:>7}")

    md = [
        f"# Test results: {member}",
        "",
        f"Run on {datetime.now():%Y-%m-%d %H:%M}. Generated by `testing/run_member_tests.sh`.",
        "",
        "| Part | Total | Pass | Fail | Skipped | Note |",
        "| --- | ---: | ---: | ---: | ---: | --- |",
        *[f"| {r[0]} | {r[1]} | {r[2]} | {r[3]} | {r[4]} | {r[5]} |" for r in rows],
        f"| **All parts** | **{total}** | **{passed}** | **{failed}** | | |",
    ]
    for label, cases in details:
        md += ["", f"## {label}", "", "| # | Test case | Result |", "| ---: | --- | --- |"]
        md += [f"| {i} | `{name}` | {outcome} |" for i, (name, outcome) in enumerate(cases, 1)]
    (out / "SUMMARY.md").write_text("\n".join(md) + "\n")
    print(f"\nEvery test case and its result: {out / 'SUMMARY.md'}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
