#!/usr/bin/env bash
# Shows the Verification agent (with the Coordinator agent) working. Arguments go to the demo (--sample, --live, --json).
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PY="$ROOT/ai/.venv/bin/python"; [[ -x "$PY" ]] || PY=python3
cd "$ROOT/ai" && exec "$PY" demos/member4_braveena_verification_agent.py "$@"
