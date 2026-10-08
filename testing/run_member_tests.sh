#!/usr/bin/env bash
# Runs one member's tests across the API, AI service, web app and mobile app, then writes a
# results summary to testing/<member>/results/SUMMARY.md.
#
#   testing/run_member_tests.sh 1          # Member 1 - Jaliya
#   testing/run_member_tests.sh 4 ai web   # Member 4 - only the AI and web parts
#
# Parts: api ai web mobile (default: all four). A part whose tool is not installed is skipped
# and reported as skipped. Set TEST_DATABASE_URL to a test-only PostgreSQL database to include
# the API's PostgreSQL integration tests; without it those tests report as skipped.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
case "${1:-}" in
  1) TAG="Member1-Jaliya";     DIR="member1_jaliya"     NAME="Jaliya H. A. W (IT24101976)" ;;
  2) TAG="Member2-Ranasinghe"; DIR="member2_ranasinghe" NAME="Ranasinghe R.G.P.D (IT24100910)" ;;
  3) TAG="Member3-Uthpala";    DIR="member3_uthpala"    NAME="Uthpala W.A.S (IT24101028)" ;;
  4) TAG="Member4-Braveena";   DIR="member4_braveena"   NAME="Braveena S (IT24100354)" ;;
  *) echo "usage: $0 <1|2|3|4> [api] [ai] [web] [mobile]" >&2; exit 2 ;;
esac
shift
PARTS="${*:-api ai web mobile}"
OUT="$ROOT/testing/$DIR/results"
rm -rf "$OUT" && mkdir -p "$OUT"
STATUS=0

banner() { printf '\n\033[1m==== %s ====\033[0m\n' "$1"; }
has() { command -v "$1" >/dev/null 2>&1; }

banner "FoundU tests for $NAME"
echo "Results folder: testing/$DIR/results"

if [[ " $PARTS " == *" api "* ]]; then
  banner "API (xUnit) - api/tests/FoundU.Tests/${TAG/-/_}/"
  if has dotnet; then
    (cd "$ROOT/api" && dotnet test tests/FoundU.Tests --filter "Member=$TAG" --nologo \
      --logger "console;verbosity=normal" --logger "trx;LogFileName=api.trx" \
      --results-directory "$OUT") | grep -E "^\s+(Passed|Failed|Skipped) |Passed!|Failed!" || true
    [[ -f "$OUT/api.trx" ]] || STATUS=1
    grep -q 'outcome="Failed"' "$OUT/api.trx" 2>/dev/null && STATUS=1
  else
    echo "dotnet is not installed - skipped"; echo "skipped" > "$OUT/api.skipped"
  fi
fi

if [[ " $PARTS " == *" ai "* ]]; then
  banner "AI service (pytest) - ai/tests/$DIR/"
  PY="$ROOT/ai/.venv/bin/python"; [[ -x "$PY" ]] || PY="$(command -v python3 || true)"
  if [[ -n "$PY" ]] && "$PY" -c "import pytest, fastapi" 2>/dev/null; then
    (cd "$ROOT/ai" && "$PY" -m pytest "tests/$DIR" -v -p no:cacheprovider \
      --junitxml="$OUT/ai.xml") || STATUS=1
  else
    echo "the AI service's Python environment is not set up - skipped"
    echo "skipped" > "$OUT/ai.skipped"
  fi
fi

if [[ " $PARTS " == *" web "* ]]; then
  banner "Web app (Vitest) - web/tests/$DIR/"
  if [[ -x "$ROOT/web/node_modules/.bin/vitest" ]]; then
    (cd "$ROOT/web" && ./node_modules/.bin/vitest run "tests/$DIR" --reporter=verbose \
      --reporter=junit --outputFile.junit="$OUT/web.xml") || STATUS=1
  else
    echo "run 'npm ci' in web/ first - skipped"; echo "skipped" > "$OUT/web.skipped"
  fi
fi

if [[ " $PARTS " == *" mobile "* ]]; then
  banner "Mobile app (flutter_test) - mobile/test/$DIR/"
  if has flutter; then
    (cd "$ROOT/mobile" && flutter test "test/$DIR" --reporter expanded \
      --file-reporter "json:$OUT/mobile.json") || STATUS=1
  else
    echo "flutter is not installed - skipped"; echo "skipped" > "$OUT/mobile.skipped"
  fi
fi

banner "Summary"
PY="$ROOT/ai/.venv/bin/python"; [[ -x "$PY" ]] || PY="python3"
"$PY" "$ROOT/testing/summarize_member_results.py" "$OUT" "$NAME" || STATUS=1
exit $STATUS
