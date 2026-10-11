#!/usr/bin/env bash
# Runs one member's tests across the seven testing areas, then writes a results summary to
# testing/<member>/results/SUMMARY.md.
#
#   testing/run_member_tests.sh 1             # Member 1 - Jaliya, every part
#   testing/run_member_tests.sh 4 ai web      # Member 4 - only the AI and web parts
#
# Parts (default: all, in this order):
#   api       1  xUnit, the member's API tests (not the PostgreSQL ones)
#   db        2  xUnit on real PostgreSQL - needs TEST_DATABASE_URL naming a test-only database
#   web       3  Vitest
#   mobile    4  flutter_test
#   e2e       5  Playwright (the member's folder) + 6c axe accessibility - needs the web app and API
#   security  6a Newman, the member's folder of the security collection - needs the API and Docker
#   perf      6b k6, the member's load script - needs the API
#   ai        7  pytest, the member's agent (LLM_PROVIDER=fake)
# A part whose tool or service is missing is reported as skipped, with the reason.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
case "${1:-}" in
  1) TAG="Member1-Jaliya";     DIR="member1_jaliya"     NAME="Jaliya H. A. W (IT24101976)" ;;
  2) TAG="Member2-Ranasinghe"; DIR="member2_ranasinghe" NAME="Ranasinghe R.G.P.D (IT24100910)" ;;
  3) TAG="Member3-Uthpala";    DIR="member3_uthpala"    NAME="Uthpala W.A.S (IT24101028)" ;;
  4) TAG="Member4-Braveena";   DIR="member4_braveena"   NAME="Braveena S (IT24100354)" ;;
  *) echo "usage: $0 <1|2|3|4> [api] [db] [web] [mobile] [e2e] [security] [perf] [ai]" >&2; exit 2 ;;
esac
N="$1"
shift
PARTS="${*:-api db web mobile e2e security perf ai}"
OUT="$ROOT/testing/$DIR/results"
rm -rf "$OUT" && mkdir -p "$OUT"
STATUS=0
API_URL="${API_URL:-http://localhost:5292}"
WEB_URL="${WEB_URL:-http://127.0.0.1:5173}"

banner() { printf '\n\033[1m==== %s ====\033[0m\n' "$1"; }
has() { command -v "$1" >/dev/null 2>&1; }
up() { curl -sf -o /dev/null --max-time 5 "$1"; }
skip() { echo "$2 - skipped"; echo "$2" > "$OUT/$1.skipped"; }
wants() { [[ " $PARTS " == *" $1 "* ]]; }

# The admin pages and the Member 1 load script sign in as the local admin. Its password is
# read from the git-ignored development settings when it is not already set.
if [[ -z "${FOUNDU_ADMIN_PASSWORD:-}" && -f "$ROOT/api/src/FoundU.Api/appsettings.Development.json" ]]; then
  FOUNDU_ADMIN_PASSWORD="$(python3 -c "import json,sys; print(json.load(open(sys.argv[1])).get('Seed', {}).get('DevAdminPassword', ''))" \
    "$ROOT/api/src/FoundU.Api/appsettings.Development.json" 2>/dev/null)"
  export FOUNDU_ADMIN_PASSWORD
fi

banner "FoundU tests for $NAME"
echo "Results folder: testing/$DIR/results"

dotnet_part() { # part filter
  (cd "$ROOT/api" && dotnet test tests/FoundU.Tests --filter "$2" --nologo \
    --logger "console;verbosity=normal" --logger "trx;LogFileName=$1.trx" \
    --results-directory "$OUT") | grep -E "^\s+(Passed|Failed|Skipped) |Passed!|Failed!|error" || true
  [[ -f "$OUT/$1.trx" ]] || STATUS=1
  grep -q 'outcome="Failed"' "$OUT/$1.trx" 2>/dev/null && STATUS=1
}

if wants api; then
  banner "1 Backend / API (xUnit) - api/tests/FoundU.Tests/${TAG/-/_}/"
  if has dotnet; then dotnet_part api "Member=$TAG&Category!=PostgreSql"; else skip api "dotnet is not installed"; fi
fi

if wants db; then
  banner "2 Database (xUnit on PostgreSQL) - ${TAG/-/_}/Member${N}DatabaseTests.cs"
  if ! has dotnet; then skip db "dotnet is not installed"
  elif [[ -z "${TEST_DATABASE_URL:-}" ]]; then skip db "TEST_DATABASE_URL is not set (a test-only PostgreSQL database)"
  else dotnet_part db "Member=$TAG&Category=PostgreSql"; fi
fi

if wants web; then
  banner "3 React web (Vitest) - web/tests/$DIR/"
  if [[ -x "$ROOT/web/node_modules/.bin/vitest" ]]; then
    (cd "$ROOT/web" && ./node_modules/.bin/vitest run "tests/$DIR" --reporter=verbose \
      --reporter=junit --outputFile.junit="$OUT/web.xml") || STATUS=1
  else
    skip web "run 'npm ci' in web/ first"
  fi
fi

if wants mobile; then
  banner "4 Flutter mobile (flutter_test) - mobile/test/$DIR/"
  if has flutter; then
    (cd "$ROOT/mobile" && flutter test "test/$DIR" --reporter expanded \
      --file-reporter "json:$OUT/mobile.json") || STATUS=1
  else
    skip mobile "flutter is not installed"
  fi
fi

if wants e2e; then
  banner "5 Integration / E2E (Playwright) - testing/e2e/tests/$DIR/"
  if [[ ! -d "$ROOT/testing/e2e/node_modules/@playwright/test" ]]; then
    skip e2e "run 'npm ci' in testing/e2e/ first"; skip a11y "run 'npm ci' in testing/e2e/ first"
  elif ! up "$WEB_URL" || ! up "$API_URL/api/health"; then
    skip e2e "the web app ($WEB_URL) or API ($API_URL) is not running"; skip a11y "the web app or API is not running"
  else
    pw() { # junit-file html-folder args...
      local junit="$1" html="$2"; shift 2
      (cd "$ROOT/testing/e2e" && PLAYWRIGHT_JUNIT_OUTPUT_FILE="$junit" PLAYWRIGHT_HTML_OUTPUT_DIR="$html" \
        PLAYWRIGHT_HTML_OPEN=never npx playwright test "$@" --reporter=list,junit,html) || STATUS=1
    }
    pw "$OUT/e2e.xml" "$OUT/e2e-html" "tests/$DIR"
    if [[ "$N" == 2 && -d "$ROOT/web/node_modules/@playwright/test" ]]; then
      echo "-- web/e2e/web-app.spec.ts (Member 2's browser journeys and form boundaries)"
      (cd "$ROOT/web" && FOUNDU_E2E_BASE_URL="$WEB_URL" PLAYWRIGHT_JUNIT_OUTPUT_FILE="$OUT/e2e-web.xml" \
        npx playwright test --reporter=list,junit) || STATUS=1
    fi
    banner "6c Accessibility (axe-core, WCAG 2.1 AA) - Member $N pages"
    pw "$OUT/a11y.xml" "$OUT/a11y-html" tests/accessibility.spec.ts -g "Member $N -"
  fi
fi

if wants security; then
  banner "6a Security (Newman) - folder \"Member $N\" of testing/security/foundu-security.postman_collection.json"
  if ! has docker || ! docker info >/dev/null 2>&1; then skip security "Docker is not running (Newman runs in a container)"
  elif ! up "$API_URL/api/health"; then skip security "the API ($API_URL) is not running"
  else
    NEWMAN_OUT="$DIR/results/newman" "$ROOT/testing/security/run-newman.sh" "" "Member $N" || STATUS=1
    cp "$OUT/newman/newman-junit.xml" "$OUT/security.xml" 2>/dev/null || STATUS=1
  fi
fi

if wants perf; then
  SCRIPT="$(cd "$ROOT/testing/performance" && ls member${N}_*.js 2>/dev/null | head -1)"
  banner "6b Performance (k6) - testing/performance/${SCRIPT:-member${N}_*.js}"
  if ! has k6; then skip perf "k6 is not installed"
  elif [[ -z "$SCRIPT" ]]; then skip perf "no k6 script for Member $N yet"
  elif ! up "$API_URL/api/health"; then skip perf "the API ($API_URL) is not running"
  else
    RAW="$(mktemp)"
    (cd "$ROOT/testing/performance" && k6 run --summary-export "$RAW" "$SCRIPT") || STATUS=1
    # setup_data holds the access tokens from setup(); it is dropped before anything is saved.
    python3 -c "import json,sys; d=json.load(open(sys.argv[1])); d.pop('setup_data', None); json.dump(d, open(sys.argv[2], 'w'), indent=2)" \
      "$RAW" "$OUT/perf.json" || STATUS=1
    rm -f "$RAW"
  fi
fi

if wants ai; then
  banner "7 Agentic AI (pytest) - ai/tests/$DIR/"
  PY="$ROOT/ai/.venv/bin/python"; [[ -x "$PY" ]] || PY="$(command -v python3 || true)"
  if [[ -n "$PY" ]] && "$PY" -c "import pytest, fastapi" 2>/dev/null; then
    (cd "$ROOT/ai" && LLM_PROVIDER=fake "$PY" -m pytest "tests/$DIR" -v -p no:cacheprovider \
      --junitxml="$OUT/ai.xml") || STATUS=1
  else
    skip ai "the AI service's Python environment is not set up"
  fi
fi

banner "Summary"
PY="$ROOT/ai/.venv/bin/python"; [[ -x "$PY" ]] || PY="python3"
"$PY" "$ROOT/testing/summarize_member_results.py" "$OUT" "$NAME" || STATUS=1
exit $STATUS
