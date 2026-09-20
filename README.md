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

## Claims verification local demo

The automated Claims tests use a controlled verification-agent stub, so they do not need a
running Python service. For a live local demo, start the real services in three terminals after
starting PostgreSQL with `docker compose up -d`.

```powershell
# Terminal 1 — FastAPI verification agent
cd ai
.\.venv\Scripts\Activate.ps1
uvicorn app.main:app --reload

# Terminal 2 — ASP.NET Core API (Development seeds only reference data and a development admin)
cd api
$env:DEV_ADMIN_PASSWORD = "choose-a-local-development-password"
$env:Jwt__SigningKey = "a-local-development-signing-key-with-at-least-32-bytes"
dotnet run --project src/FoundU.Api --launch-profile http

# Terminal 3 — Flutter Android emulator
cd mobile
flutter pub get
flutter run --dart-define=FOUND_U_API_BASE_URL=http://10.0.2.2:5292
```

For a repeatable ownership-verification walkthrough, use a student lost report for **Blue
backpack**, then have staff create the matching found report with general description **Blue
backpack** and private verification details **blue keychain; small tear inside front pocket**.
Those details belong only in the staff form. Have the student create the claim from its match,
have staff generate questions, and submit the remembered details in the app. The agent's result
only moves the claim to **Under review** or **Staff review required**; finish the walkthrough by
having staff explicitly approve or reject it in the staff queue. Never enter real credentials or
private ownership evidence in screenshots, API logs, or demo notes.

## Working agreements

- No direct commits to `main`; branch per step, PR reviewed by a teammate.
- CI must be green before merge.
