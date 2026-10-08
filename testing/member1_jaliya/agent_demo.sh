#!/usr/bin/env bash
# Shows the AI support chatbot (the Support agent) working. Arguments go to the demo (--sample, --live, --json).
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PY="$ROOT/ai/.venv/bin/python"; [[ -x "$PY" ]] || PY=python3
cd "$ROOT/ai" && exec "$PY" demos/member1_jaliya_support_agent.py "$@"
