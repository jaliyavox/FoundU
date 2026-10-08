#!/usr/bin/env bash
# Starts the AI service locally against the real model (Groq openai/gpt-oss-20b), with workflow
# state in the Docker PostgreSQL. Reads the shared service key from ~/.foundu-ai-key and the
# Groq key from ~/.foundu-groq-key; neither is ever stored in the repository.
# Usage: testing/start-ai.sh            (Ctrl+C stops it)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"

[ -s ~/.foundu-ai-key ] || openssl rand -hex 32 > ~/.foundu-ai-key
[ -s ~/.foundu-groq-key ] || {
  echo "No Groq key yet. Save it once with:"
  echo "  read -s \"k?Groq key: \" && print -r -- \"\$k\" > ~/.foundu-groq-key && chmod 600 ~/.foundu-groq-key && unset k"
  exit 1
}

# The workflow store needs PostgreSQL; start it if it has stopped.
if ! nc -z localhost 5434 2>/dev/null; then
  docker compose -f "$root/docker-compose.yml" up -d postgres >/dev/null
  for _ in $(seq 1 30); do
    [ "$(docker inspect -f '{{.State.Health.Status}}' foundu-postgres 2>/dev/null)" = healthy ] && break
    sleep 2
  done
fi

export AI_SERVICE_KEY="$(cat ~/.foundu-ai-key)"
export WORKFLOW_STATE_STORE=postgres
export WORKFLOW_DATABASE_URL=postgresql://foundu:foundu@localhost:5434/foundu
export LLM_PROVIDER=groq
export LLM_MODEL=openai/gpt-oss-20b
export LLM_TIMEOUT_SECONDS=20
export LLM_API_KEY="$(cat ~/.foundu-groq-key)"

cd "$root/ai"
exec .venv/bin/uvicorn app.main:app --reload --port 8000
