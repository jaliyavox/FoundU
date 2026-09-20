# FoundU — AI Service (Python / FastAPI)

The AI service currently provides a FastAPI health check and a shared LangGraph foundation.
The graph routes each request to exactly one of four deterministic agents:

- `description_parser` — future owner of Lost Item Reporting & Tracking
- `matching` — future owner of Found Item Management & Intelligent Matching
- `verification` — future owner of Claims & Ownership Verification
- `coordinator` — future owner of Resolution, Notifications & Administration

The agents do not use an LLM, make approval decisions, access external services, or persist data.
Additional agent logic will be implemented separately.

## Shared LLM foundation (Phase 1)

`app.llm` now defines the provider-neutral `LlmClient` structured-generation interface, typed
request contract, safe failure types, environment-backed settings, and a deterministic
`FakeLlmClient`. Agents are not migrated to it yet, and **no real provider is connected**.
The fake is network-free and is used to make future provider adapters and agent tests
deterministic.

The Phase 1 configuration is non-secret and defaults to:

```text
LLM_PROVIDER=fake
LLM_MODEL=fake-structured-v1
LLM_TIMEOUT_SECONDS=5
```

Only `fake` is available in this phase; selecting another provider fails safely at composition.
`OLLAMA_BASE_URL` and `OLLAMA_MODEL` remain documented for a future team-approved Ollama adapter,
but are not read or instantiated today. The next phase is one agreed real provider adapter behind
`LlmClient`, followed by agent-specific schema validation and migration.

## Verification Agent

The Verification Agent currently performs deterministic ownership-verification support only. It
generates non-leading questions from private staff evidence and compares submitted answers using
normalization and token overlap. It returns a `likely_match`, `manual_review`, or
`unlikely_match` recommendation only; it never approves or rejects claims or changes claim status.

Private verification evidence is never returned to students, included in traces, or exposed in
errors. The current implementation is deterministic and does not yet use a real LLM.

## Local setup

Python 3.11 or newer is required.

```bash
cd ai
python -m venv .venv
# Windows PowerShell: .\.venv\Scripts\Activate.ps1
# macOS/Linux: source .venv/bin/activate
pip install -r requirements-dev.txt
uvicorn app.main:app --reload
```

The service is available at `http://localhost:8000`; health is at `GET /health`.

## Run an agent

```bash
curl -X POST http://localhost:8000/agents/run \
  -H "Content-Type: application/json" \
  -d '{"agent":"verification","payload":{"operation":"generate_questions","claim_id":"claim-123","private_verification_details":{"distinctive_mark":"staff-only value"}},"correlation_id":"example-123"}'
```

Example response:

```json
{
  "agent_run_id": "7ef18d04-2f59-4218-8453-87ef57d5c368",
  "agent": "verification",
  "status": "completed",
  "output": {"operation":"generate_questions","claim_id":"claim-123","questions":[{"question_id":"verification-1","question":"What distinctive mark or damage does the item have?"}],"recommendation":"manual_review"},
  "trace": [
    "request_received",
    "routed:verification",
    "verification:received",
    "verification:generate_questions",
    "verification:completed"
  ]
}
```

## Checks

```bash
ruff check .
pytest
```
