# FoundU — Build Plan (working checklist)

Derived from the SE3090 nine-week build plan. This file tracks **our** execution order:
**monorepo skeleton -> web dashboard -> Flutter app**, with the .NET API and AI service
scaffolded as we need them.

> Context line to prepend to any AI prompt:
> *"Repo: FoundU. Stack: ASP.NET Core 8 + PostgreSQL + React (Vite, TS) + Flutter + Python/LangGraph."*

---

## Environment status (re-checked 2026-08-18)

| Tool | Required | Installed | Note |
|------|----------|-----------|------|
| Node / npm | 20+ | Yes 24.13.1 / 11.8.0 | ready for `/web` |
| .NET SDK | 8 | Yes 8.0.423 (+ `dotnet ef` 8.0.11) | ready for `/api` |
| Docker | any | Yes, Desktop installed | Postgres 16 + Ollama running |
| Python | 3.11 | No, have 3.9.6 | upgrade before `/ai` |
| Flutter | stable | No, not installed | install before mobile phase |

**Action items before later phases:** install Flutter (`brew install --cask flutter`)
and Python 3.11 (`brew install python@3.11`).

### Local ports (this machine)

Two native EnterpriseDB PostgreSQL installs already occupy **5432** and **5433**, so
`docker-compose.yml` maps Docker's Postgres 16 to **5434**. Teammates on a clean machine
can use the default 5432 — just keep `appsettings.Development.json` in step.

| Service | URL |
|---------|-----|
| PostgreSQL 16 (Docker) | `localhost:5434` — db/user/pass all `foundu` |
| Ollama | `localhost:11434` |
| API | `http://localhost:5292` (Swagger at `/swagger`) |
| Web dev server | `http://localhost:5173` |

`api/src/FoundU.Api/appsettings.Development.json` is **gitignored** and missing from a fresh
clone. The API will not start without it — the JWT signing-key check rejects placeholders.
It needs `ConnectionStrings:FoundUDatabase`, `Jwt:SigningKey`, and `Seed:DevAdminPassword`.

**Dev accounts** (Development seed / registration):
`admin@foundu.com` · `student@foundu.com` · `student2@foundu.com`

---

## Phase 0 — Monorepo skeleton (Step 1) — DONE

Chosen build order: **skeleton first, then API + web together** (login end-to-end),
then feature slices, then Flutter, then AI.

Goal: the four sub-project folders exist and each builds on its own.

- [x] `/api` — ASP.NET Core 8 solution, layered: `Api`, `Application`, `Domain`, `Infrastructure` (+ `FoundU.Tests`); builds + 1 test passes; `HealthController` at `GET /api/health`
- [x] `/web` — React + Vite + TypeScript app (builds; `.env.example` with `VITE_API_BASE_URL`)
- [x] `/mobile` — Flutter placeholder (hand-written `pubspec.yaml`, `lib/main.dart`, widget test; regenerate with `flutter create .` once Flutter is installed)
- [x] `/ai` — Python 3.11 FastAPI service (`/health` + test; `ruff` passes; needs 3.11 to run locally)
- [x] `/docs` — docs folder with README
- [x] Root `README.md`, `.gitignore` (4 ecosystems), `.editorconfig`
- [x] `docker-compose.yml` — PostgreSQL 16 + Ollama
- [x] `.github/workflows/ci.yml` — on PR: build+test .NET, build web, `flutter analyze`, `ruff` + `pytest`

**Next:** commit this branch (`setup/monorepo-skeleton`) and open a PR, then start Step 2
(EF schema) and the web shell.

---

## Track A — WEB APP FIRST (our current focus)

We can build the web dashboard's structure now and wire it to the API as the API lands.

### A1 · Web shell (subset of Step 4a) — DONE 2026-08-18
- [x] Scaffold Vite + React + TS in `/web`
- [x] Router with public + role-guarded routes (React Router v7)
- [x] TanStack Query set up (status-aware retries: 4xx never retried)
- [x] Typed fetch client that attaches JWT and refreshes on 401 (single-flight refresh)
- [x] App layout — collapsible shadcn sidebar + header, role-filtered nav
- [x] Login page with field-level validation errors
- [x] Toast system (sonner)
- [x] Feature-first folder structure
- [x] Env config for API base URL
- [x] Tailwind v4 + shadcn/ui (`base-nova`, Base UI) — conventions in `/docs/design.md`

### A2 · Auth wiring — DONE 2026-08-18
- [x] Login calls `POST /api/auth/login`, stores tokens, routes by role
- [x] Route guards for Student / Staff / Admin, with return-to-intended-URL
- [x] Wired to the real `/api` auth (no stubs needed — Step 3 landed first)

### A3 · Feature screens (built as API endpoints come online)
- [x] Found-item log form + items table (staff sees private fields) — DONE 2026-09-09
- [ ] Student: report-lost form + my-reports list with withdraw - needs Step 6 API **<- in progress**
- [x] Claims review queue + claim detail (approve/reject) — DONE 2026-09-09
- [ ] Staff notification log — needs Step 8 API
- [ ] Admin: users table, analytics (Recharts), dispute review — needs Step 9 API
- [ ] Agent-run panel on claim detail — needs Step 13 API

### A4 · Web polish
- [ ] Loading / empty / error states on every list, form, detail view
- [ ] Keyboard nav, focus rings, labelled fields, aria-live, 4.5:1 contrast

---

## Track B — FLUTTER APP (after web is functional)

### B1 · Mobile shell (subset of Step 4b)
- [ ] `flutter create` in `/mobile`, feature-first structure
- [ ] go_router, Riverpod
- [ ] Dio client with auth interceptor
- [ ] flutter_secure_storage for tokens
- [ ] Light theme + login screen against `POST /api/auth/login`, route by role

### B2 · Feature screens
- [ ] Report-lost form with camera/gallery picker + browse-found list (Step 6)
- [ ] Claim button, answer-question screen, claim status view (Step 7)
- [ ] FCM setup, inbox with unread badges + deep links (Step 8)

### B3 · Mobile polish
- [ ] Loading/empty/error states, 48dp touch targets, semantic labels, offline retry

---

## Track C — API + AI (built in parallel to unblock the clients)

The web app needs real endpoints to be more than a shell. Minimum to unblock Track A:

- [x] **Step 2** — EF Core domain model + initial migration (25 entities, 27 tables, taxonomy seeded via `HasData`)
- [x] **Step 3** — JWT auth + Identity, roles, ProblemDetails envelope, FluentValidation, Swagger
      *(still owed: `/docs/api-conventions.md` — referenced from code but not yet written)*
- [x] **Step 6** — Reporting slice API (reference lookups, found reports, lost reports)
- [x] **Step 7** — Claims + staff review API
- [ ] **Step 8** — Notifications + resolution API
- [ ] **Step 9** — Admin + analytics + dispute API
- [ ] **Step 5 / 10–13** — AI service + agent nodes + .NET<->AI integration

---

## Suggested execution order (single build path)

1. **Step 1** — monorepo skeleton (all four folders + CI + compose)
2. **Step 2** — DB schema / EF migration
3. **Step 3** — Auth + API conventions
4. **A1 + A2** — Web shell + auth wiring (login works end-to-end)
5. **Step 6 API + A3 reporting screens**
6. **Step 7 API + A3 claims screens**
7. **Step 8 API + A3 notification log**
8. **Step 9 API + A3 admin**
9. **A4** — web polish
10. **B1–B3** — Flutter app (login -> features -> polish)
11. **Step 5 / 10–13** — AI agents + integration
12. **Steps 14–17** — tests, docs, seed data, demo

---

---

## Progress log

Newest first. Record what landed, and anything a teammate would otherwise trip over.

### 2026-09-09 - staff found-item screens

The last A3 screen that Step 6 had already unblocked, and the staff half of the match bridge -
until now staff could only link an item to a report through curl.

- **`/items`** - the items table. Defaults to what is still in storage, because that is the
  working set: a desk needs to know what it is holding, not everything it ever held. Search,
  status filter, paging.
- **`/items/new`** - the log form. One screen, not a wizard: a student fills in a report on a
  phone after losing something, but this is typed at a counter with the item in hand.
- **`/items/:id`** - detail, plus **Suggest to a report**, which creates the match suggestion
  the student then sees.
- The hidden verification detail gets **its own panel** on both screens rather than sitting
  among the other fields. It is the one thing on the page that must not be read aloud to
  whoever is standing at the counter, and the form says so plainly - including "leave it blank
  if there is genuinely nothing distinctive, do not invent one".
- The table shows only **whether** a hidden detail was recorded, never the detail.

Contracts checked against the live API: the table query, search, the detail (403 for a
student), the active-report picker, and the suggestions already on an item.

### 2026-09-09 - claims screens and the staff match bridge

Step 7 shipped an API with no way in: found reports are staff-only on purpose, and the thing
meant to introduce a student to an item - `MatchSuggestion`, written by the Matching Agent -
belongs to Step 11, which is blocked on Python 3.11. So the manual half of that bridge now
exists. **Staff link an item to a lost report by hand; the student sees it on their reports and
claims from there.** `GeneratedByAgentRunId` stays null, and when the agent lands it writes the
same rows without the screens changing.

- **New endpoints** - `POST /api/match-suggestions` (Staff), `GET /mine` (Student),
  `GET /for-item/{id}` (Staff), `POST /{id}/dismiss` (Student).
- **Migration** `20260909120456_AddMatchSuggestionStaffNote` - a line the student reads, which
  does not belong in the agent's `MatchingFactorsJson` column.
- `MatchScore` is **hidden** for hand-made links rather than faked. A person comparing two
  records has no confidence score, and a number that means nothing is worse than none.
- Opening a claim confirms the suggestion that led to it, with status history.
- **Screens:** suggestions panel on My reports and My claims; `/my-claims` list; `/claims`
  staff queue (oldest first, defaults to what needs attention); one `/claims/:id` detail for
  both sides - students answer, staff ask and decide. Two screens for one record drift apart,
  and the API already decides who may see it.
- Whether an answer was judged correct is never rendered, matching the API.

**Still no notifications.** A student only discovers a suggestion by opening the dashboard.
That is Step 8, and it is the next thing worth building.

### 2026-08-19 - Step 7 claims + staff review API

`Administration` was merged into `main` first, so the report flow, dashboard panels and found
claims are on one line of history again.

**Endpoints** (`/api/claims`) - no migration needed, Step 2 already modelled every entity:

| Verb | Route | Who | What |
| --- | --- | --- | --- |
| POST | `/api/claims` | Student | Claim a found item against your own lost report |
| GET | `/api/claims/mine` | Student | Your claims, newest first |
| GET | `/api/claims` | Staff/Admin | The review queue, **oldest first** - longest wait is worked next |
| GET | `/api/claims/{id}` | both | Students read only their own |
| POST | `/api/claims/{id}/questions` | Staff/Admin | Write the verification questions |
| POST | `/api/claims/{id}/answers` | Student | Answer every outstanding question at once |
| POST | `/api/claims/{id}/decision` | Staff/Admin | Approved / Rejected / RevisionRequested |
| POST | `/api/claims/{id}/cancel` | Student | Give up on your own claim |

**Status wiring.** A claim is what finally moves a lost report off `Active`: creating one sets
it to `Matched`, approval sets it to `Resolved` and the found item to `Returned`. Rejection or
cancellation puts the report back to `Active` unless another claim of theirs is still open, so
nothing gets stranded on `Matched`. Approving one claim rejects every other open claim on the
same item with "Another claim for this item was approved."

This is what makes the progress track on the report card real - before Step 7 nothing in the
API ever set `Matched` or `Resolved`, so a report could never advance past "someone found it".

**The privacy line.** `FoundReport.PrivateVerificationDetails` never enters a student-reachable
projection; claim detail projects the item through `FoundReportSummaryDto`, which is the
student-safe shape. `ClaimAnswer.IsCorrect` is also withheld from students - telling a claimant
which answers passed hands a fraudulent one a feedback loop for guessing the rest. Verified by
dumping every student-reachable payload and grepping it.

**Verified against the database** (spare API on 5299, so it never touched the dev instance):
duplicate claim 409; rival reading another student's claim 403; partial answers 400 with the
count outstanding; approve -> claim Approved, item Returned, report Resolved; deciding twice
409; reject -> report back to Active, item back to Unclaimed; RevisionRequested -> student
rewrites the same answer -> UnderReview; cancel -> report back to Active; student hitting the
staff queue 403.

**Not built, deliberately.** `VerificationQuestion.GeneratedByAgentRunId` stays null and
`ClaimAnswer.IsCorrect` stays null - both belong to the Verification Agent in Step 12. Staff
write the questions and judge the answers by hand until then, which is a working desk process,
not a stub.

### 2026-08-19 - public landing, brand, and the lost feed

- **Brand applied** - four greens (mist/sage/green/forest) as `--brand-*` tokens, logo mark
  (`FoundUMark`/`FoundULogo`) plus favicon. Full reference in **`/docs/design.md`**.
- **Landing page** - dark bento sections: animated hero, capability strip, How it works,
  parallax divider (light band), Features, FAQ (light band), CTA, footer. Reusable shells live
  in `components/landing/bento.tsx`.
- **Auth pages** - `/login` and `/register` share `AuthLayout` (split form + brand panel with a
  four-step process animation). Register wires to `POST /api/auth/register`.
- **Lost feed** - **new public endpoint `GET /api/lost-reports/feed`**, anonymous, active
  reports only, paginated and searchable. Returns the poster's display name and **no other
  personal data** (no email, student number or user id). Page at `/feed`, open to visitors;
  posting requires an account.
- **Model change:** lost reports are now publicly listable. This reverses the earlier
  "nothing student-facing is published" stance - found items remain staff-only, which is what
  protects claim verification.
- **House style:** no pill/badge "tags" anywhere - eyebrows are plain
  `text-sm font-medium text-brand-green`. Plain `-` instead of en/em dashes in UI copy.

### 2026-08-18 — Step 6 reporting API

- **Reference lookups** — `GET /api/reference/{categories,locations,storage-locations}`.
  Categories return their item types nested so one call fills both dropdowns.
- **Found reports** (Staff/Admin) — create, paged/filtered/sorted list, detail.
- **Lost reports** — students create, read and withdraw their own; Staff/Admin list and read all.
- `PrivateVerificationDetails` appears only in the Staff detail DTO. The list DTO exposes a
  `hasVerificationDetails` boolean instead, and free-text search deliberately does **not** cover
  that column — a searchable secret is not a secret.
- Sorting goes through an allow-list, never string-interpolated SQL.
- **Behaviour change:** `PolicyNames.Staff` was `RequireRole(Staff)`, which locked Admins out of
  every staff endpoint. Now `RequireRole(Staff, Admin)`. `Student` stays exclusive.
- Verified against the live DB: 201 on create, 400 on category/item-type mismatch and unknown
  status, 401 unauthenticated, 403 for student→staff, admin→student and student→other-student's
  report, 409 on double withdraw.

### 2026-08-18 — A1/A2 web shell

- Replaced the Vite starter with the real shell (see A1/A2 above).
- Tailwind v4 + shadcn/ui installed; UI conventions written up in **`/docs/design.md`** — read it
  before writing screens.
- **Gotcha:** this shadcn style is built on Base UI, so composition uses `render={<Link/>}`,
  **not** Radix's `asChild`. Most tutorials online show the wrong one.
- **Gotcha:** the `@/` alias must be declared in **both** `tsconfig.json` and `tsconfig.app.json`.
  Without the root one, `shadcn add` silently writes components into a literal `./@` folder.

### 2026-08-18 — environment repairs

- Fixed `FoundUDbContextFactory`: it resolved config relative to the working directory, but
  `dotnet ef` runs from `bin/Debug/net8.0`, so both `AddJsonFile` calls silently no-opped and it
  fell back to a hardcoded `localhost:5432 / postgres` string. Now walks up from
  `AppContext.BaseDirectory` to find `FoundU.Api`, and **throws** instead of guessing.
- Docker Postgres remapped to **5434** (see Local ports above).
- Wiped a stale `foundu_pgdata` volume still holding the superseded `20260806081522_InitialSchema`.
  **If migrations fail with "relation already exists", the volume is stale** — drop and re-apply.
- Dev admin seed is now `admin@foundu.com`. The seeder no-ops when any Admin exists, so changing
  the seed requires dropping the database, not just editing the constant.

---

## Working agreements (from the plan)

- Branch per step (e.g. `web/shell`, `api/auth`); no direct commits to `main`.
- Every step ends in a PR reviewed by a teammate.
- Definition of done: works where applicable, tests green, reviewed & merged,
  loading/empty/error states, role checks proven by a test, documented in `/docs`.
