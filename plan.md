# FoundU — Build Plan (working checklist)

Derived from the SE3090 nine-week build plan. This file tracks **our** execution order:
**monorepo skeleton -> web dashboard -> Flutter app**, with the .NET API and AI service
scaffolded as we need them.

> Context line to prepend to any AI prompt:
> *"Repo: FoundU. Stack: ASP.NET Core 8 + PostgreSQL + React (Vite, TS) + Flutter + Python/LangGraph."*

---

## Environment status (checked 2026-08-06)

| Tool | Required | Installed | Note |
|------|----------|-----------|------|
| Node / npm | 20+ | Yes 24.13.1 / 11.8.0 | ready for `/web` |
| .NET SDK | 8 | Yes 8.0.423 | ready for `/api` |
| Python | 3.11 | No, have 3.9.6 | fine to defer; upgrade before `/ai` |
| Flutter | stable | No, not installed | install before mobile phase |
| Docker | any | No, not on PATH | needed for Postgres + Ollama compose |

**Action items before later phases:** install Flutter (`brew install --cask flutter`),
install Docker Desktop, and install Python 3.11 (`brew install python@3.11`).

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

### A1 · Web shell (subset of Step 4a)
- [ ] Scaffold Vite + React + TS in `/web`
- [ ] Router with public + role-guarded routes (React Router)
- [ ] TanStack Query set up
- [ ] Typed fetch client that attaches JWT and refreshes on 401
- [ ] App layout (sidebar + header)
- [ ] Login page
- [ ] Toast system
- [ ] Feature-first folder structure
- [ ] Env config for API base URL

### A2 · Auth wiring
- [ ] Login calls `POST /api/auth/login`, stores tokens, routes by role
- [ ] Route guards for Student / Staff / Admin
- [ ] (Uses mock/stub API responses until `/api` auth exists — see Track C)

### A3 · Feature screens (built as API endpoints come online)
- [ ] Found-item log form + items table (staff sees private fields) — needs Step 6 API
- [ ] Claims review queue + claim detail (approve/reject) — needs Step 7 API
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

- [ ] **Step 2** — EF Core domain model + initial migration (schema is the contract)
- [ ] **Step 3** — JWT auth + Identity, roles, ProblemDetails envelope, FluentValidation, Swagger, `/docs/api-conventions.md`
- [ ] **Step 6** — Reporting slice API
- [ ] **Step 7** — Claims + staff review API
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

## Working agreements (from the plan)

- Branch per step (e.g. `web/shell`, `api/auth`); no direct commits to `main`.
- Every step ends in a PR reviewed by a teammate.
- Definition of done: works where applicable, tests green, reviewed & merged,
  loading/empty/error states, role checks proven by a test, documented in `/docs`.
