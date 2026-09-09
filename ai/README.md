# FoundU — AI Service (Python / FastAPI)

The AI service currently provides a FastAPI health check and a shared LangGraph foundation.
The graph routes each request to exactly one of four deterministic agent stubs:

- `description_parser` — future owner of Lost Item Reporting & Tracking
- `matching` — future owner of Found Item Management & Intelligent Matching
- `verification` — future owner of Claims & Ownership Verification
- `coordinator` — future owner of Resolution, Notifications & Administration

The stubs do not use an LLM, make AI decisions, access external services, or persist data. Real
agent logic will be implemented separately.

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

## Run an agent stub

```bash
curl -X POST http://localhost:8000/agents/run \
  -H "Content-Type: application/json" \
  -d '{"agent":"verification","payload":{},"correlation_id":"example-123"}'
```

Example response:

```json
{
  "agent_run_id": "7ef18d04-2f59-4218-8453-87ef57d5c368",
  "agent": "verification",
  "status": "completed",
  "output": {
    "stub": true,
    "message": "Verification Agent foundation is ready."
  },
  "trace": [
    "request_received",
    "routed:verification",
    "executed:verification"
  ]
}
```

## Checks

```bash
ruff check .
pytest
```
