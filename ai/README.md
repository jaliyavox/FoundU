# FoundU — AI Service (Python / FastAPI)

The AI service currently provides a FastAPI health check and a shared LangGraph foundation.
The graph routes each request to exactly one of four deterministic agents:

- `description_parser` — future owner of Lost Item Reporting & Tracking
- `matching` — future owner of Found Item Management & Intelligent Matching
- `verification` — future owner of Claims & Ownership Verification
- `coordinator` — future owner of Resolution, Notifications & Administration

The agents do not use an LLM, make approval decisions, access external services, or persist data.
Additional agent logic will be implemented separately.

## Shared LLM foundation (Phase 2)

`app.llm` now defines the provider-neutral `LlmClient` structured-generation interface, typed
request contract, safe failure types, environment-backed settings, deterministic `FakeLlmClient`,
and an `OllamaLlmClient` adapter. Agents are not migrated to either client yet, so existing agent
behaviour remains deterministic. The fake is network-free and keeps CI deterministic.

The Phase 1 configuration is non-secret and defaults to:

```text
LLM_PROVIDER=fake
LLM_MODEL=fake-structured-v1
LLM_TIMEOUT_SECONDS=5
```

`fake` remains the default. The first real adapter is selected explicitly with:

```text
LLM_PROVIDER=ollama
LLM_MODEL=<your-installed-model>
LLM_TIMEOUT_SECONDS=30
OLLAMA_BASE_URL=http://localhost:11434
```

`LLM_MODEL` is canonical; the existing `OLLAMA_MODEL` is used only as a backward-compatible
fallback when `LLM_MODEL` is absent. The adapter sends a non-streaming `POST /api/chat`, supplies
the caller's Pydantic JSON schema through Ollama's `format` field, and validates only
`message.content` as strict JSON against that schema. It neither logs nor retains prompts or raw
responses. No agent uses Ollama yet; Phase 3 is a safe, schema-validated migration of one agreed
agent.

### Optional local Ollama smoke test

This is not part of pytest or CI. Install/run Ollama separately, then pull the model selected by
`LLM_MODEL` and run a short local script:

```bash
ollama serve
ollama pull <your-installed-model>
```

Set the environment before opening a Python shell from `ai`:

```bash
export LLM_PROVIDER=ollama
export LLM_MODEL=<your-installed-model>
export LLM_TIMEOUT_SECONDS=30
export OLLAMA_BASE_URL=http://localhost:11434
```

```powershell
$env:LLM_PROVIDER = "ollama"
$env:LLM_MODEL = "<your-installed-model>"
$env:LLM_TIMEOUT_SECONDS = "30"
$env:OLLAMA_BASE_URL = "http://localhost:11434"
```

Then run:

```python
from pydantic import BaseModel
from app.llm import LlmSettings, StructuredGenerationRequest, create_llm_client

class Reply(BaseModel):
    answer: str

client = create_llm_client(LlmSettings.from_environment())
print(client.generate_structured(
    StructuredGenerationRequest(
        operation="smoke", system_instruction="Return JSON only.", input="Return an answer."
    ),
    Reply,
))
client.close()
```

Do not download a model automatically from project scripts.

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
