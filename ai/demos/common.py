"""Shared plumbing for the per-member agent demos.

Each demo sends its input through the AI service's real `/agents/run` endpoint (the same
route the ASP.NET API calls), then prints the agent's answer as a readable report. By default
the service runs in-process with the deterministic `fake` model, so a demo needs no keys or
servers. `--live` uses the model configured in the environment (LLM_PROVIDER=groq,
LLM_MODEL, LLM_API_KEY) instead.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import sys
import textwrap
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

DEMO_KEY = "agent-demo-service-key-0123456789abcdef"

WIDTH = min(shutil.get_terminal_size((100, 20)).columns, 100)
COLOUR = sys.stdout.isatty() and not os.environ.get("NO_COLOR")


def _c(code: str, text: str) -> str:
    return f"\033[{code}m{text}\033[0m" if COLOUR else text


def bold(text: str) -> str:
    return _c("1", text)


def green(text: str) -> str:
    return _c("32", text)


def amber(text: str) -> str:
    return _c("33", text)


def red(text: str) -> str:
    return _c("31", text)


def dim(text: str) -> str:
    return _c("2", text)


def parse_args(description: str, extra=None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=description)
    parser.add_argument("--live", action="store_true", help="use the model in the environment")
    parser.add_argument("--json", action="store_true", help="also print the raw JSON response")
    if extra:
        extra(parser)
    return parser.parse_args()


class AgentService:
    """The composed FastAPI app, called exactly as the API calls it."""

    def __init__(self, live: bool) -> None:
        if not live:
            os.environ["LLM_PROVIDER"] = "fake"
            os.environ["LLM_MODEL"] = "fake-structured-v1"
        os.environ["AI_SERVICE_KEY"] = DEMO_KEY
        os.environ["WORKFLOW_STATE_STORE"] = "memory"
        from fastapi.testclient import TestClient

        from app import main
        from app.service_auth import SERVICE_KEY_HEADER

        self.model = f"{os.environ.get('LLM_PROVIDER')} / {os.environ.get('LLM_MODEL')}"
        self._headers = {SERVICE_KEY_HEADER: DEMO_KEY}
        self._client = TestClient(main.app)
        self._client.__enter__()

    def run(self, agent: str, payload: dict) -> tuple[dict, float]:
        started = time.perf_counter()
        response = self._client.post(
            "/agents/run", json={"agent": agent, "payload": payload}, headers=self._headers
        )
        elapsed = (time.perf_counter() - started) * 1000
        response.raise_for_status()
        return response.json(), elapsed

    def post(self, path: str, body: dict) -> dict:
        response = self._client.post(path, json=body, headers=self._headers)
        return {"status_code": response.status_code, **response.json()}


def header(agent: str, member: str, component: str) -> None:
    print()
    print(bold("=" * WIDTH))
    print(bold(f" FoundU · {agent}"))
    print(f" {member}  ·  business component: {component}")
    print(bold("=" * WIDTH))


def section(title: str) -> None:
    print()
    print(bold(f"── {title} ") + dim("─" * max(0, WIDTH - len(title) - 4)))


def row(label: str, value: object, indent: int = 2) -> None:
    label_width = 16
    text = "—" if value is None or value == [] else str(value)
    lines = textwrap.wrap(text, WIDTH - indent - label_width - 1, break_on_hyphens=False) or [""]
    print(" " * indent + f"{label:<{label_width}}" + lines[0])
    for line in lines[1:]:
        print(" " * (indent + label_width) + line)


def bullet(text: str, mark: str = "•", indent: int = 4) -> None:
    lines = textwrap.wrap(text, WIDTH - indent - 2) or [""]
    print(" " * indent + f"{mark} " + lines[0])
    for line in lines[1:]:
        print(" " * (indent + 2) + line)


def plan(steps: list[str], inline: bool = False) -> None:
    if inline:
        row("Plan", "  →  ".join(steps))
        return
    section("Plan the agent followed")
    for line in textwrap.wrap("  →  ".join(steps), WIDTH - 2, break_on_hyphens=False):
        print("  " + line)


def trace(body: dict) -> None:
    section("Trace (what the service recorded)")
    for step in body.get("trace", []):
        bullet(step, mark="·")


def footer(service: AgentService, elapsed_ms: float, body: dict, show_json: bool) -> None:
    section("Run")
    row("Model", service.model)
    row("Status", body.get("status"))
    row("Run id", body.get("agent_run_id"))
    row("Time", f"{elapsed_ms:.0f} ms")
    if show_json:
        section("Raw JSON response")
        print(textwrap.indent(json.dumps(body, indent=2), "  "))
    print()


def plan_steps(plan_obj) -> list[str]:
    return [step.step_id for step in plan_obj.steps]


def prompt(text: str, default: str | None = None) -> str:
    suffix = f" [{default}]" if default else ""
    try:
        value = input(f"{text}{suffix}: ").strip()
    except EOFError:
        value = ""
    return value or (default or "")
