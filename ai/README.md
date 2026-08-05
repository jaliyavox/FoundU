# FoundU — AI Service (Python / FastAPI)

FastAPI service that will host the LangGraph agent graph (coordinator, reader, verifier,
messenger) talking to a local Ollama model. This is the skeleton; the graph and the
`POST /agents/run` contract land in Step 5.

Requires **Python 3.11** (the skeleton machine had 3.9 — upgrade before running locally).

## Local setup

```bash
cd ai
python3.11 -m venv .venv
source .venv/bin/activate
pip install -r requirements-dev.txt
uvicorn app.main:app --reload   # http://localhost:8000/health
```

## Checks (same as CI)

```bash
ruff check .
pytest
```
