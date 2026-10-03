# FoundU: starting every server

This guide starts the whole system on one machine: PostgreSQL, the AI service, the API, the web
app and the mobile app. It ends with how the deployed system on Render is started. Run the steps
in order, because each part depends on the one before it.

| Part | Port | Depends on | Check it is up |
|---|---|---|---|
| PostgreSQL 16 (Docker) | 5434 | Docker | `docker ps` shows `foundu-postgres` (healthy) |
| AI service (FastAPI) | 8000 | PostgreSQL | http://localhost:8000/health |
| API (ASP.NET Core 8) | 5292 | PostgreSQL, AI service | http://localhost:5292/api/health |
| Web app (React + Vite) | 5173 | API | http://localhost:5173 |
| Mobile app (Flutter) | n/a | API | Opens on the emulator |
| Ollama (optional) | 11434 | Docker | `docker exec foundu-ollama ollama list` |

## Prerequisites

- Docker Desktop, running
- .NET SDK 8
- Python 3.11 or 3.12 (the AI service's Docker image uses 3.12)
- Node.js 20 or later, with npm (Render builds with Node 22)
- Flutter (stable) and an Android emulator or phone, for the mobile app

All commands run from the repository root unless a step says `cd`.

## 0. One shared secret

The API and the AI service authenticate each other with a shared key. Make one and keep it for
every terminal:

```bash
openssl rand -hex 32 > ~/.foundu-ai-key
export AI_SERVICE_KEY=$(cat ~/.foundu-ai-key)
```

PowerShell: `$env:AI_SERVICE_KEY = "<a long random string>"`

## 1. PostgreSQL

```bash
docker compose up -d postgres
docker ps --filter name=foundu-postgres      # wait for "(healthy)"
```

The database is `foundu`, user `foundu`, password `foundu`, on `localhost:5434`. Data persists
in the `pgdata` volume. The API creates the tables itself on its first start (EF Core migrations).

Open a SQL prompt for demos: `docker exec -it foundu-postgres psql -U foundu -d foundu`

## 2. AI service

First time only:

```bash
cd ai
python3 -m venv .venv
.venv/bin/pip install -r requirements-dev.txt      # requirements.txt alone if you won't run tests
```

Start it. Choose **one** model provider:

```bash
cd ai
export AI_SERVICE_KEY=$(cat ~/.foundu-ai-key)
export WORKFLOW_STATE_STORE=postgres
export WORKFLOW_DATABASE_URL=postgresql://foundu:foundu@localhost:5434/foundu

# a) Deterministic, no key, no network (default for demos and tests)
export LLM_PROVIDER=fake LLM_MODEL=fake-structured-v1

# b) Groq, the hosted model used in production
# export LLM_PROVIDER=groq LLM_MODEL=openai/gpt-oss-20b LLM_API_KEY=<your Groq key> LLM_TIMEOUT_SECONDS=20

# c) Ollama on this machine (docker compose up -d ollama, then ollama pull <model>)
# export LLM_PROVIDER=ollama LLM_MODEL=<installed model> OLLAMA_BASE_URL=http://localhost:11434 LLM_TIMEOUT_SECONDS=30

.venv/bin/uvicorn app.main:app --reload --port 8000
```

PowerShell: activate with `.\.venv\Scripts\Activate.ps1`, set each variable as
`$env:NAME = "value"`, then run `uvicorn app.main:app --reload --port 8000`.

`ai/.env.example` lists every setting. Every route except `/health` needs the
`X-FoundU-Service-Key` header, so only the API can call the AI service.

## 3. API

Settings come from `api/src/FoundU.Api/appsettings.json`, and then from the git-ignored
`appsettings.Development.json`, then from environment variables (`Section__Key`). The
connection string already points at the Docker database. On a fresh clone, set at least:

```bash
cd api
export AiService__ServiceKey=$(cat ~/.foundu-ai-key)                # must equal AI_SERVICE_KEY
export Jwt__SigningKey="a-local-development-signing-key-with-at-least-32-bytes"
export DEV_ADMIN_PASSWORD="choose-a-local-admin-password"         # admin@foundu.com, first start only
dotnet run --project src/FoundU.Api --launch-profile http
```

PowerShell: `$env:AiService__ServiceKey = "<same key>"` and so on, then the same `dotnet run`.

The `http` profile runs in Development on http://localhost:5292. On start it applies
migrations and, on an empty database, creates the admin account and the starting reference data. Swagger is at
http://localhost:5292/swagger. Click **Authorize** and paste an access token from
`POST /api/auth/login`.

Optional integrations stay off until configured:

| Feature | Setting | Without it |
|---|---|---|
| Email (Resend) | `Email__ResendApiKey`, `Email__From`, `Email__WebBaseUrl` | Emails, links included, are written to the API log |
| Google sign-in | `Google__ClientId` (the **Web** OAuth client id) | The Google button is hidden on web and mobile |

The README has the step-by-step setup for each one.

## 4. Web app

```bash
cd web
cp .env.example .env.local        # VITE_API_BASE_URL=http://localhost:5292
npm ci
npm run dev
```

Open http://localhost:5173. Sign in as `admin@foundu.com` with your `DEV_ADMIN_PASSWORD`, or
register a student.

## 5. Mobile app

```bash
cd mobile
flutter pub get
flutter emulators                                   # list emulators
flutter emulators --launch <emulator id>
flutter run --dart-define=FOUND_U_API_BASE_URL=http://10.0.2.2:5292
```

`10.0.2.2` is the Android emulator's address for your computer. On a **physical phone**, use your
computer's LAN address, e.g. `http://192.168.1.20:5292`. Start the API so it listens on the
network: `dotnet run --project src/FoundU.Api --launch-profile http --urls http://0.0.0.0:5292`.

The mobile app is for students. Staff and admin accounts are told to use the web dashboard.

## 6. Demo data (optional)

With the API running:

```bash
FOUNDU_ADMIN_PASSWORD="<your DEV_ADMIN_PASSWORD>" python3 scripts/demo_seed.py
```

This **wipes** all activity in the local database, then rebuilds a demo through the real API.
Student and staff accounts use the password `Demo!Pass2026`: `amara`, `dev`, `nadia` and
`kasun` (students), and `priya` (staff), all `@foundu.test`. Write down the two codes it prints.
Never point it at a deployed database.

## Start or stop everything at once

Steps 1 to 4 in one command (the AI service uses the fake model unless `LLM_PROVIDER` is set). It
waits until each part is healthy and writes logs to `testing/reports/logs/`:

```bash
testing/start-stack.sh        # reads the key from AI_SERVICE_KEY or ~/.foundu-ai-key
testing/stop-stack.sh         # stops the AI service, API and web app; the database keeps running
docker compose stop           # stops the database too
```

## Production (Render)

`render.yaml` defines all four services, and Render starts them itself on every push to `main`:

| Service | Starts with | Health check |
|---|---|---|
| `foundu-db` | Managed PostgreSQL | n/a |
| `foundu-ai` | `ai/Dockerfile` (uvicorn) | https://foundu-ai.onrender.com/health |
| `foundu-api` | `api/Dockerfile` (migrations run on start) | https://foundu-api.onrender.com/api/health |
| `foundu-web` | `npm ci && npm run build`, served as a static site | https://foundu-web.onrender.com |

The first deploy is **New → Blueprint** in the Render dashboard. The secrets to enter are in the
README's "Deploying to Render" section. On the free plan a service sleeps after 15 minutes
without requests, so open each health URL about a minute before a demo. Swagger is off in
production.

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| API exits with a database connection error | PostgreSQL isn't up yet. Run `docker ps` and wait for `(healthy)` |
| `port 5434 already in use` | Another PostgreSQL is running. Stop it, or change the port in `docker-compose.yml` and the connection string |
| AI features say "unavailable" or 401 in the API log | `AiService__ServiceKey` and `AI_SERVICE_KEY` differ, or the AI service isn't running |
| AI service won't start, asking for a model key | `LLM_PROVIDER` is `groq` with no `LLM_API_KEY`. Set the key, or use `fake` |
| Web app shows network errors | The API isn't on 5292, or `web/.env.local` points elsewhere. Restart `npm run dev` after editing it |
| Phone can't reach the API | Use `10.0.2.2` on the emulator. On a phone use the LAN IP and `--urls http://0.0.0.0:5292` |
| Can't sign in as admin | The admin is created once, on the API's first start against an empty database. If `DEV_ADMIN_PASSWORD` wasn't set then, the password is `DevOnly-ChangeMe-123!`. Setting it later changes nothing; to start over, `docker compose down -v` (deletes all local data) |
