# FoundU — AI Service (Python / FastAPI)

The AI service provides a FastAPI health check and a shared LangGraph workflow.
The graph routes each request to exactly one of four bounded logical agents:

- `description_parser` — structured lost-item attribute extraction
- `matching` — read-only match recommendation
- `verification` — safe question drafting and deterministic answer recommendation
- `coordinator` — deterministic safe workflow next-action recommendation

The Description Parser and Verification question drafting can use the shared LLM client; Matching
and Coordinator are deterministic. No agent makes approval decisions, accesses external services
directly, or persists model reasoning.

## ASP.NET-to-FastAPI service authentication (Phase 8)

FastAPI is an internal AI service, not a client-facing backend. The application path is:

```text
Mobile / Web
        ↓
ASP.NET API (authoritative application backend)
        ↓  X-FoundU-Service-Key
FastAPI AI service
```

`POST /agents/run` and `POST /agents/parse-description` require the
`X-FoundU-Service-Key` header. FastAPI reads `AI_SERVICE_KEY` only from server configuration,
requires a nonblank key of at least 32 characters, and compares it with `secrets.compare_digest`.
Missing, blank, or wrong headers receive the same generic `401 Unauthorized` response; an absent
or invalid server configuration fails closed with a generic service-unavailable response. Payload
fields cannot authenticate a request. `GET /health` is intentionally public for service health
checks.

ASP.NET sends the same secret from `AiService:ServiceKey` (or the
`AiService__ServiceKey` environment variable) through its DI-managed verification and matching
clients. The key is never added to agent state, tool traces, prompts, API responses, or logs. This
authentication only establishes the trusted service caller; it does not alter agent identity,
plans, tool permissions, Matching, checkpointing, or Verification's recommendation-only boundary.
ASP.NET remains the only authority for staff claim decisions.

For local development, generate a value without committing it:

```powershell
python -c "import secrets; print(secrets.token_urlsafe(32))"
$env:AI_SERVICE_KEY = "<same-strong-random-secret>"
$env:AiService__ServiceKey = "<same-strong-random-secret>"
```

Set `AI_SERVICE_KEY` before starting FastAPI and `AiService__ServiceKey` before starting the
ASP.NET API. `.env.example` contains a placeholder only; no mobile or web client should receive
this value or call FastAPI directly.

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

## Structured execution plans (Phase 6)

Every agent now creates a deterministic, typed execution plan in graph state before its work. Plans
are internal audit metadata rather than API response fields, chain-of-thought, prompts, or
scratchpads. They use a finite action set (`inspect_input`, `call_model`, `call_tool`,
`validate_result`, `produce_recommendation`, `request_human_review`, and `complete`) plus fixed safe labels; they
cannot contain raw reports, private verification evidence, or arbitrary reasoning.

```text
Agent request
        ↓
trusted agent identity
        ↓
deterministic structured plan
        ↓
plan validation
        ↓
ToolRegistry permission validation
        ↓
execution and deterministic result
        ↓
human approval where required
```

Plan validation checks trusted agent identity, bounded and contiguous step ordering, unique IDs,
valid action shapes, and registered/allow-listed tool references. A plan only states intent; it
never grants a permission, and `ToolRegistry` remains the final authorization boundary. Matching's
`match_reports` plan explicitly names its two lookups and checks that each tool is planned before
calling the registry. Invalid plans result in a safe bounded response and no tool execution.

Description Parser, Verification, and Coordinator also create deterministic plans reflecting their
current non-authoritative paths. The Coordinator validates only typed workflow metadata and returns
a bounded recommendation such as awaiting staff review, notifying a claimant, or workflow
completion. It does not mutate claims, send notifications, or make staff decisions. Verification
plans have no decision actions or authoritative tool steps: staff approval/rejection authority
remains exclusively in ASP.NET.

A plan records intended, permitted execution metadata, not a guarantee that every step will run.
For example, Description Parser's plan contains `call_model`, but deterministic preconditions skip
the real LLM request for unusable input; its existing fallback and trace behavior remain unchanged.

## Safe LangGraph checkpoints (Phase 7)

FastAPI composition creates one LangGraph `InMemorySaver`-based checkpointer and compiles the
agent graph with it. Each `/agents/run` execution uses its server-generated `agent_run_id` as the
trusted LangGraph thread ID:

```text
request
        ↓
server-generated agent_run_id / thread_id
        ↓
LangGraph execution
        ↓
SafeInMemorySaver
        ↓
sanitized checkpoint
        ↓
trusted internal checkpoint retrieval
```

The in-memory saver is wrapped with a checkpoint sanitization boundary. Checkpoints retain only
safe execution metadata such as the agent/run identity, structured plan, safe trace labels, and
already-safe result metadata. Request payloads, correlation IDs, prompts, descriptions, private
verification evidence, reasoning, scratchpads, and model-response-like fields are removed before
serialization. An internal snapshot-load path can safely retrieve completed state without
replaying model or tool calls, and it rejects unknown or cross-agent thread requests.

This demonstrates checkpointed agent state and thread-isolated continuity within one FastAPI
process only; it does not survive process restart. It is not interrupted-workflow execution resume,
human-approval pause/resume, or durable restart persistence: the current graph has no pause point
and each run is already complete when its state is retrieved. A durable production backend can
replace the in-memory saver later without moving the ASP.NET human approval boundary or granting AI
claim-decision authority.

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
  -H "X-FoundU-Service-Key: <your-service-key>" \
  -d '{"description":"Blue backpack with a red keychain"}'
```

Do not download a model automatically from project scripts.

## Verification Agent

The Verification Agent currently performs deterministic ownership-verification support only. It
generates non-leading questions from private staff evidence and compares submitted answers using
normalization and token overlap. It returns a `likely_match`, `manual_review`, or
`unlikely_match` recommendation only; it never approves or rejects claims or changes claim status.

Private verification evidence is never returned to students, included in traces, or exposed in
errors. Question wording can use the application-owned shared LLM client, but it receives only
canonical question IDs and fixed evidence-category labels. The deterministic server-side binding
from ID to expected value, answer scoring, recommendation mapping, and staff decision boundary
remain unchanged. Provider, schema, or safety-validation failures use the deterministic templates.

For an optional local Ollama wording smoke test (never pytest or CI), set the service and LLM
configuration before starting FastAPI:

```powershell
$env:LLM_PROVIDER = "ollama"
$env:LLM_MODEL = "<installed-model>"
$env:OLLAMA_BASE_URL = "http://localhost:11434"
$env:AI_SERVICE_KEY = "<strong-key>"
uvicorn app.main:app --reload
```

Call `POST /agents/run` with the authenticated `X-FoundU-Service-Key` header and a normal
`verification` / `generate_questions` payload supplied server-to-server. Do not paste real
ownership evidence into shell history, issue trackers, or documentation; use an isolated local
test claim instead. A successful wording draft may add `verification:llm_attempt` and
`verification:llm_success` to the safe trace. A rejected or unavailable provider instead adds
`verification:fallback`; no prompt, raw response, or evidence values are traced.

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
  -H "X-FoundU-Service-Key: <your-service-key>" \
  -d '{"agent":"verification","payload":{"operation":"generate_questions","claim_id":"claim-123","private_verification_details":{"distinctive_mark":"<server-held evidence>"}},"correlation_id":"example-123"}'
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
