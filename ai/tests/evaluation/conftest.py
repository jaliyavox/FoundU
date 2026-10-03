"""Collects the evaluation results by category and writes them for the testing report."""

import json
import os
import re
import time
from pathlib import Path

CATEGORIES = {
    "TC": "Task completion",
    "AS": "Agent selection",
    "TS": "Tool selection",
    "SO": "Structured-output validation",
    "BR": "Business-rule compliance",
    "PI": "Prompt-injection resistance",
    "AP": "Approval enforcement",
    "FR": "Failure recovery",
    "SF": "Safe failure",
}
_results: list[dict] = []


def pytest_runtest_logreport(report):
    if report.when != "call" and not (report.when == "setup" and report.outcome != "passed"):
        return
    name = report.nodeid.split("::")[-1]
    match = re.match(r"test_([A-Z]{2})(\d+)_", name)
    if not match:
        return
    _results.append(
        {
            "id": f"AIE-{match.group(1)}-{match.group(2)}"
            + (name[name.index("[") :] if "[" in name else ""),
            "category": CATEGORIES[match.group(1)],
            "test": name,
            "outcome": report.outcome,
            "seconds": round(report.duration, 3),
            "detail": None if report.passed else str(report.longrepr).splitlines()[-1][:300],
        }
    )


def pytest_sessionfinish(session):
    if not _results:
        return
    provider = os.environ.get("LLM_PROVIDER", "fake")
    mode = "deterministic" if provider == "fake" else "live"
    summary = {}
    for r in _results:
        s = summary.setdefault(r["category"], {"passed": 0, "failed": 0})
        s["passed" if r["outcome"] == "passed" else "failed"] += 1
    out = Path(__file__).resolve().parents[3] / "testing" / "reports" / "ai-eval"
    out.mkdir(parents=True, exist_ok=True)
    (out / f"{mode}-results.json").write_text(
        json.dumps(
            {
                "mode": mode,
                "provider": provider,
                "model": os.environ.get("LLM_MODEL"),
                "run_at": time.strftime("%Y-%m-%d %H:%M:%S"),
                "totals": {
                    "cases": len(_results),
                    "passed": sum(r["outcome"] == "passed" for r in _results),
                },
                "by_category": summary,
                "cases": _results,
            },
            indent=2,
        )
    )
