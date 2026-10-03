#!/usr/bin/env bash
# Starts the full FoundU stack locally for the end-to-end, performance and security runs:
# PostgreSQL (Docker), the AI service (deterministic "fake" model), the API and the web app.
# Needs AI_SERVICE_KEY in the environment, or a key in ~/.foundu-ai-key.
# Logs go to testing/reports/logs. Stop everything with testing/stop-stack.sh.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
logs="$root/testing/reports/logs"
mkdir -p "$logs"
key="${AI_SERVICE_KEY:-$(cat ~/.foundu-ai-key 2>/dev/null || true)}"
[ -n "$key" ] || { echo "Set AI_SERVICE_KEY (any long random string)"; exit 1; }

docker compose -f "$root/docker-compose.yml" up -d postgres >/dev/null

if ! curl -sf localhost:8000/health >/dev/null; then
  (cd "$root/ai" && AI_SERVICE_KEY="$key" LLM_PROVIDER="${LLM_PROVIDER:-fake}" LLM_MODEL="${LLM_MODEL:-fake-structured-v1}" \
    WORKFLOW_STATE_STORE=postgres WORKFLOW_DATABASE_URL=postgresql://foundu:foundu@localhost:5434/foundu \
    nohup .venv/bin/uvicorn app.main:app --port 8000 >"$logs/ai.log" 2>&1 </dev/null &)
fi
if ! curl -sf localhost:5292/api/lost-reports/feed >/dev/null; then
  (cd "$root/api" && AiService__ServiceKey="$key" \
    nohup dotnet run --project src/FoundU.Api --launch-profile http >"$logs/api.log" 2>&1 </dev/null &)
fi
if ! curl -sf localhost:5173 >/dev/null; then
  (cd "$root/web" && nohup npm run dev -- --host 127.0.0.1 >"$logs/web.log" 2>&1 </dev/null &)
fi

wait_for() {
  for _ in $(seq 1 90); do curl -sf "$2" >/dev/null && { echo "$1 up"; return; }; sleep 2; done
  echo "$1 did not start - see $logs"; exit 1
}
wait_for "AI service" localhost:8000/health
wait_for "API" localhost:5292/api/lost-reports/feed
wait_for "Web" localhost:5173
