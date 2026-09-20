# FoundU — AI Service (Python / FastAPI)

The AI service currently provides a FastAPI health check and a shared LangGraph foundation.
The graph routes each request to exactly one of four logical agents:

- `description_parser` — future owner of Lost Item Reporting & Tracking
- `matching` — future owner of Found Item Management & Intelligent Matching
- `verification` — future owner of Claims & Ownership Verification
- `coordinator` — future owner of Resolution, Notifications & Administration

Only the Description Parser can optionally use the shared LLM client; the remaining agents are
deterministic. No agent makes approval decisions, accesses external services directly, or persists
model reasoning.

## Shared LLM foundation (Phase 2)

`app.llm` now defines the provider-neutral `LlmClient` structured-generation interface, typed
request contract, safe failure types, environment-backed settings, deterministic `FakeLlmClient`,
and an `OllamaLlmClient` adapter. The Description Parser is the first consumer: it receives the
client through application composition, validates a strict internal response schema, grounds every
accepted attribute in the supplied description, and falls back to the original deterministic parser
on any provider, schema, or post-validation failure. The fake is network-free and keeps CI
deterministic.

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
responses. Verification remains deterministic and does not use Ollama.

## Executable tool registry (Phase 4)

`app.tools.ToolRegistry` is the sole executable boundary for agent tools. It validates a typed
input model, checks the requesting agent from trusted graph/application context against the
existing `AGENT_PERMISSIONS` allow-list, invokes a registered callable, and validates a typed
output model. Models and agents never receive a callable directly, so future model-selected tools
must go through `ToolRegistry.execute(...)`.

The current tools are deterministic in-memory adapters only; they do not access PostgreSQL or
change FoundU business state. Safe trace metadata is limited to
`tool:attempt:<name>`, `tool:success:<name>`, `tool:denied:<name>`, and `tool:failure:<name>`.
It never includes arguments, outputs, descriptions, private verification evidence, or prompts.

Verification can use only `getLostReportDetails`, `getFoundReportDetails`,
`createVerificationChallenge`, and `recordVerificationResult`. It has no approval, rejection,
custody-transfer, item-resolution, or staff-decision-overturn tool; staff authority remains in
ASP.NET.

## Read-only Matching execution (Phase 5)

Matching is the first real tool-consuming LangGraph path. For an explicit `match_reports` request,
the trusted request context flows as follows:

```text
Trusted request/context
        ↓
LangGraph Matching Agent
        ↓
ToolRegistry.execute(...)
        ↓
allow-list check + typed validation
        ↓
context-backed read-only report adapter
        ↓
validated public report summary
        ↓
deterministic match recommendation
```

The shared registry is composed once during FastAPI lifespan and injected into the Matching graph
node. Its immutable `ToolExecutionContext` is built only from graph-owned agent/run state. Matching
executes `getLostReportDetails` and `getFoundReportDetails`; it compares only the returned public
item type and primary colour, and emits a recommendation rather than creating a claim or writing a
match candidate.

The lookup provider currently reads validated report context supplied with the AI request. It is
local, deterministic, and read-only: it has no PostgreSQL connection and does not reproduce
ASP.NET business rules. A future application-owned provider can replace it without changing tool
contracts. Tool failures yield a bounded `manual_review` result; Matching never falls back to raw
context or adapter calls after a registry failure. Report descriptions remain data and are not
returned, scored, used for tool selection, or able to affect workflow authority.

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

Then start FastAPI and submit a description (this does not run in pytest):

```bash
uvicorn app.main:app --reload
curl -X POST http://localhost:8000/agents/parse-description \
  -H "Content-Type: application/json" \
  -d '{"description":"Blue backpack with a red keychain"}'
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
