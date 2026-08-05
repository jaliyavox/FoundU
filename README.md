# FoundU

A smart campus lost & found platform that reunites students with their belongings —
quickly, fairly, and with a little help from AI.

## Monorepo layout

| Path      | Project                                   | Stack                               |
|-----------|-------------------------------------------|-------------------------------------|
| `/api`    | Web API (layered)                         | ASP.NET Core 8, EF Core, PostgreSQL |
| `/web`    | Staff & admin dashboard                   | React + Vite + TypeScript           |
| `/mobile` | Student app                               | Flutter                             |
| `/ai`     | Agent service (coordinator/reader/verifier/messenger) | Python 3.11, FastAPI, LangGraph, Ollama |
| `/docs`   | Shared contracts & diagrams               | Markdown / Mermaid                  |

`.github/workflows/ci.yml` builds and tests all four projects on every pull request.

## Prerequisites

- .NET SDK 8
- Node.js 20+ and npm
- Flutter (stable) — for `/mobile`
- Python 3.11 — for `/ai`
- Docker — for Postgres 16 + Ollama via `docker-compose.yml`

## Quick start

```bash
# infra: PostgreSQL 16 + Ollama
docker compose up -d

# api
cd api && dotnet build && dotnet test

# web
cd web && npm install && npm run dev      # http://localhost:5173

# ai
cd ai && python3.11 -m venv .venv && source .venv/bin/activate \
  && pip install -r requirements-dev.txt && uvicorn app.main:app --reload

# mobile (after installing Flutter)
cd mobile && flutter pub get && flutter run
```

See `plan.md` for the step-by-step build order and `docs/` for shared contracts.

## Working agreements

- No direct commits to `main`; branch per step, PR reviewed by a teammate.
- CI must be green before merge.
