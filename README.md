# FoundU

A smart campus lost & found platform that reunites students with their belongings —
quickly, fairly, and with a little help from AI.

## Monorepo layout

| Path      | Project                                   | Stack                               |
|-----------|-------------------------------------------|-------------------------------------|
| `/api`    | Web API (layered)                         | ASP.NET Core 8, EF Core, PostgreSQL |
| `/web`    | Staff & admin dashboard                   | React + Vite + TypeScript           |
| `/mobile` | Student app                               | Flutter                             |
| `/ai`     | Agent service (Description Parser, Matching, Verification, Coordinator) | Python 3.11, FastAPI, LangGraph, Ollama |
| `/docs`   | Shared contracts & diagrams               | Markdown / Mermaid                  |

`.github/workflows/ci.yml` builds and tests all four projects on every pull request.

## Prerequisites

- .NET SDK 8
- Node.js 20+ and npm
- Flutter (stable) — for `/mobile`
- Python 3.11 — for `/ai`
- Docker — for Postgres 16 + Ollama via `docker-compose.yml`

## Full-stack PowerShell demo startup

The development ports are PostgreSQL **5434**, Ollama **11434**, FastAPI **8000**, ASP.NET
**5292**, and React **5173**.

1. Start PostgreSQL and the repository's Ollama container:

```powershell
docker compose up -d
docker exec -it foundu-ollama ollama list
docker exec -it foundu-ollama ollama pull <model-name-you-choose>
```

Alternatively use a locally installed Ollama service on `http://localhost:11434`; use `ollama
list` to confirm a model, then `ollama pull <model>` if needed. No model is downloaded by CI.

2. In separate PowerShell terminals, set one shared, locally generated 32+ character key and
start the services:

```powershell
# Terminal 1 — FastAPI AI service
cd ai
.\.venv\Scripts\Activate.ps1
$env:AI_SERVICE_KEY = "<strong-shared-key>"
$env:LLM_PROVIDER = "ollama"
$env:LLM_MODEL = "<installed-model-name>"
$env:OLLAMA_BASE_URL = "http://localhost:11434"
$env:LLM_TIMEOUT_SECONDS = "30"
uvicorn app.main:app --reload --port 8000

# Terminal 2 — ASP.NET Core API
cd api
$env:AiService__ServiceKey = "<same-strong-shared-key>"
$env:DEV_ADMIN_PASSWORD = "choose-a-local-development-password"
$env:Jwt__SigningKey = "a-local-development-signing-key-with-at-least-32-bytes"
dotnet run --project src/FoundU.Api --launch-profile http

# Terminal 3 — React staff/admin dashboard
cd web
Copy-Item .env.example .env.local
npm ci
npm run dev

# Terminal 4 — Flutter Android emulator
cd mobile
flutter pub get
flutter run --dart-define=FOUND_U_API_BASE_URL=http://10.0.2.2:5292
```

`AI_SERVICE_KEY` and `AiService__ServiceKey` must be the same strong value. It is server-to-server
only: React and Flutter never receive it. Protected FastAPI endpoints require
`X-FoundU-Service-Key`; browser/mobile clients call ASP.NET only.

## Third-party integration: Firebase Cloud Messaging

FoundU uses Firebase Cloud Messaging (FCM) to complement its persisted in-app notification
centre. A possible match, claim verification/revision, approval/rejection, collection update, or
message event first creates the normal PostgreSQL `Notification`; after that transaction commits,
ASP.NET Core may send a minimal push to the recipient's registered device. This helps students
act on recovery updates without repeatedly opening the app. A push never makes a business
decision, and the app fetches full authorized details from FoundU after a tap.

```text
Flutter device → authenticated token registration → ASP.NET Core → PostgreSQL
business event → persisted FoundU notification → ASP.NET Core → FCM → device
```

### Firebase setup (manual, no credentials are committed)

1. Create a Firebase project and add Android package `com.example.foundu` (replace this package
   before production if the team changes the application ID).
2. Download `google-services.json` to `mobile/android/app/google-services.json`. It is ignored by
   Git. For iOS, add the Firebase iOS app and place `GoogleService-Info.plist` in `ios/Runner`
   following the Firebase Flutter documentation.
3. In Firebase Console, create a service account JSON key and keep it outside the repository.
   Set backend-only environment variables before starting ASP.NET Core:

```powershell
$env:Firebase__ProjectId = "your-firebase-project-id"
$env:Firebase__CredentialsPath = "C:\secure\foundu-firebase-service-account.json"
$env:Firebase__TimeoutSeconds = "5"
```

`CredentialsPath` and the FCM token are never returned by FoundU APIs or sent to browsers. The
backend only sends `title`, `body`, `type`, `notificationId`, and when applicable `entityId`; it
never sends verification evidence/answers, credentials, AI prompts, private notes, or user
contact details. Device registration and unregistration require the caller's FoundU JWT and are
owner-scoped.

FCM is best-effort: the backend uses a bounded 5-second delivery timeout and no unbounded retry.
Provider outage, auth/configuration errors, malformed responses, and rate limiting are logged
safely and leave the committed FoundU operation plus its in-app notification intact. Firebase
unregistered/invalid tokens are deactivated so delivery is not repeatedly attempted. A real
Firebase project, service-account credentials, and platform client files remain required for
live delivery; automated tests use fakes and never contact Firebase.

## Live AI demo flow

1. A student creates a lost report. ASP.NET preserves the user's data and optionally stores
   Description Parser enrichment; parser/Ollama failure falls back safely.
2. Staff records a found item, keeping private verification evidence out of screenshots.
3. In the React staff dashboard, open the item, select **Suggest to a report**, choose the lost
   report, and use **Generate AI Match Suggestion**. ASP.NET invokes Matching; only a validated
   `match_candidate` creates a suggestion. Staff can always use the manual suggestion action.
4. The student opens a claim from the suggestion. Staff generates verification questions; the
   Verification Agent may draft only safe wording, never reveal expected values.
5. The student answers. Evaluation and its recommendation are deterministic.
6. Staff alone approves or rejects the claim; AI never decides ownership, transfers custody, or
   resolves an item.

The Coordinator is a deterministic workflow-recommendation agent invoked by ASP.NET after a
verification recommendation. ASP.NET creates a claim-linked audit run with the stable opaque
workflow ID before calling FastAPI; staff discover its durable pause state through ASP.NET only.
It has no authority to mutate business state.

### AI observability and bounded resilience

Each AI operation has an opaque server-generated correlation ID. It travels from the ASP.NET
AgentRun/client request to FastAPI and its workflow logs. Coordinator durable workflows use their
stable workflow ID as the correlation identifier. `AgentRun` records the safe outcome, timestamps,
and `RetryCount`; Coordinator runs also record real plan, human-wait, and terminal audit steps.
No prompts, raw model responses, ownership evidence, answers, service keys, or reasoning are
recorded.

Recommendation-only Description Parser, Matching, Verification, and Coordinator-start calls make
at most two short retries after a transient network failure, timeout, or HTTP 408/429/502/503/504.
Validation, malformed responses, 400/401/403/404/409 responses, and business failures are never
retried. Approval and resume are deliberately single-shot because their durable transition status
is the idempotency boundary; staff can safely check state and explicitly retry through ASP.NET.
Timeouts remain configured by `AiService:TimeoutSeconds` (bounded to 1–30 seconds). Exhaustion
uses the existing safe manual-review/fallback behavior and preserves the underlying business work.

Demo evidence: submit a verification answer, open the staff claim’s Agent trail to show the
Coordinator run/workflow ID and retry count, simulate a transient AI 503, then show the bounded
retry or manual fallback. A waiting workflow shows the staff approval panel; approving resumes
safe coordination only—the existing ASP.NET claim decision remains authoritative.

## Safe fallback demonstrations

- Stop Ollama: Description Parser and Verification question wording use their deterministic
  fallbacks.
- Stop FastAPI before generating a match: the staff member can still create a manual suggestion.
- Call `POST /agents/run` without `X-FoundU-Service-Key`: FastAPI returns `401 Unauthorized`.
- The Verification tests demonstrate that unsafe drafted wording is rejected for deterministic
  safe templates.

## Working agreements

- No direct commits to `main`; branch per step, PR reviewed by a teammate.
- CI must be green before merge.
