#!/usr/bin/env bash
# Shows the Matching agent working. Arguments go to the demo (--sample, --live, --json).
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PY="$ROOT/ai/.venv/bin/python"; [[ -x "$PY" ]] || PY=python3
cd "$ROOT/ai" && exec "$PY" demos/member3_uthpala_matching_agent.py "$@"
