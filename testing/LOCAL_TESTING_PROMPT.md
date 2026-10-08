# Prompt for Claude Code (local): per-member testing across all seven areas

> Paste this whole file into Claude Code on your machine, from the root of the FoundU repository.
> Lines marked **📸 SCREENSHOT** are where a person must take a screenshot for the report. Claude
> Code should stop and tell you when one is due, and say exactly what must be on screen.

---

## 0. Context (read first)

You are working in the FoundU repository (ASP.NET Core 8 API, PostgreSQL 16, React web, Flutter
mobile, Python FastAPI + LangGraph AI service). This is for **SE3110 Assignment 2: Software Testing
and Quality Evaluation**. Work on the branch `testing/QMSE-assignment`
(`git fetch origin && git checkout testing/QMSE-assignment && git pull`). Never rewrite history or
force-push; never skip, disable or delete a test to get green.

Already done on this branch:

- Each member's tests are in folders named after them:
  - `api/tests/FoundU.Tests/Member{N}_{Name}/`: xUnit, every class tagged `[Trait("Member", "Member{N}-{Name}")]`
  - `ai/tests/member{n}_{name}/`: pytest, incl. `test_member{n}_{name}_<agent>_agent.py`
  - `web/tests/member{n}_{name}/`: Vitest
  - `mobile/test/member{n}_{name}/`: flutter_test
- `testing/member{n}_{name}/` has `README.md`, `run_tests.sh` (parts: api ai web mobile) and
  `agent_demo.sh` (runs the member's agent on an input and prints a readable report).
- `testing/run_member_tests.sh` and `testing/summarize_member_results.py` drive the runs and write
  `testing/member{n}_{name}/results/SUMMARY.md`.

**Goal now:** every member tests *their own business component* in **all seven areas of the brief**,
and everything is runnable and presentable per member:

| # | Area (brief) | Tools |
|---|---|---|
| 1 | Backend / API | xUnit, WebApplicationFactory, Postman |
| 2 | Database | xUnit + real PostgreSQL, EF Core |
| 3 | React web | Vitest, React Testing Library |
| 4 | Flutter mobile | flutter_test (+ API integration) |
| 5 | Integration / E2E | Playwright |
| 6 | Non-functional | Postman/Newman + OWASP ZAP (security), k6 (performance), axe (accessibility) |
| 7 | Agentic AI | pytest through `/agents/run`, the member's agent demo |

Areas 1, 3, 4 and 7 already exist per member. **Areas 2, 5 and 6 are still group-owned: split them.**

## 1. Members, components and agents

| Member | Folder suffix | Business component(s) | Agent |
|---|---|---|---|
| 1 · Jaliya H. A. W (IT24101976) | `member1_jaliya` / `Member1_Jaliya` | Administration and user management (incl. mailing, Google sign-in), support tickets | AI support chatbot (Support agent) |
| 2 · Ranasinghe R.G.P.D (IT24100910) | `member2_ranasinghe` / `Member2_Ranasinghe` | Lost item reporting and tracking, "I found this" hand-ins | Description-Parsing agent |
| 3 · Uthpala W.A.S (IT24101028) | `member3_uthpala` / `Member3_Uthpala` | Found item management and matching | Matching agent |
| 4 · Braveena S (IT24100354) | `member4_braveena` / `Member4_Braveena` | Claims and ownership verification | Verification agent (+ Coordinator) |

## 2. Local environment

Start the stack exactly as `docs/startup.md` describes (one terminal each):

```bash
docker compose up -d postgres                       # PostgreSQL 16 on localhost:5434 (user/pass foundu)
testing/start-ai.sh                                 # AI service on :8000 (Groq; set LLM_PROVIDER=fake for repeatable runs)
cd api && AiService__ServiceKey=$(cat ~/.foundu-ai-key) dotnet run --project src/FoundU.Api --launch-profile http   # API on :5292
cd web && npm run dev                               # web on :5173
# emulator + mobile only for area 4:
flutter run -d emulator-5554 --dart-define=FOUND_U_API_BASE_URL=http://10.0.2.2:5292
```

Create a **separate test database** for the PostgreSQL tests (its name must contain `test`):

```bash
docker exec -it foundu-postgres psql -U foundu -c "CREATE DATABASE foundu_test;"
export TEST_DATABASE_URL="Host=localhost;Port=5434;Database=foundu_test;Username=foundu;Password=foundu"
export TEST_WORKFLOW_DATABASE_URL="postgresql://foundu:foundu@localhost:5434/foundu_test"
```

Check each service before testing: `curl localhost:5292/api/health`, `curl localhost:8000/health`,
open http://localhost:5173. If anything differs on this machine (ports, container name), read
`docs/startup.md` and `testing/README.md` and adapt; do not guess.

## 3. Tasks

Do them in order. After each task: run the affected suites, fix only what you broke, commit.

### Task A: Database testing per member (area 2)

`api/tests/FoundU.Tests/PostgresPersistenceIntegrationTests.cs` holds 11 `[PostgresFact]` tests and
shared helpers. Split it:

1. Move the shared helpers (`CreateClaimService`, `SeedClaimAsync`, `NewUser`, the fake agents and
   storage, `AddCompetingClaimAsync`, `AddCompletedVerificationAsync`) into
   `api/tests/FoundU.Tests/PostgresTestSupport.cs` as an `internal static` class (keep the fakes as
   nested or internal classes).
2. Create one file per member, each class tagged `[Trait("Category", "PostgreSql")]` and
   `[Trait("Member", "Member{N}-{Name}")]`:

| File | Tests to move in |
|---|---|
| `Member1_Jaliya/Member1DatabaseTests.cs` | `DeviceTokenUniqueIndexIsEnforcedByPostgreSql`, `DeviceReregistrationMovesTokenAndUnregisterDeactivatesIt` |
| `Member2_Ranasinghe/Member2DatabaseTests.cs` | `LostReportParserAttributesAndLifecycleHistoryPersistAndResolvedReportLeavesFeed` (his `LostReportsEndpointIntegrationTests` already uses PostgreSQL too) |
| `Member3_Uthpala/Member3DatabaseTests.cs` | **New tests (write them):** (a) a found item logged at the desk persists its storage location, status history row and private verification detail, and a student-facing query never returns the private detail; (b) two desks saving the same `FoundReport` at once: the second save fails with a concurrency conflict (xmin row version), not a lost update; (c) a found report's photo row is deleted with soft-delete rules respected (only if the model supports it; otherwise pick a real constraint on FoundReports/MatchSuggestions, for example the unique (LostReportId, FoundReportId) suggestion pair) |
| `Member4_Braveena/Member4DatabaseTests.cs` | `ClaimApprovalCommitsAuthoritativeStateAndNotificationsTogether`, `ClaimRejectionPersistsDecisionAndReopensMatchedLostReport`, `PartialUniqueIndexPreventsTwoApprovedClaimsForOneFoundReportAcrossContexts`, `ActiveClaimPairIsUniqueAcrossDatabaseContexts`, `ConflictingClaimApprovalRollsBackDecisionAndNotificationWrites`, `TwoStaffApprovingRivalClaimsAtOnceLeavesOneWinnerAndOneStory` |
| `PostgresPersistenceIntegrationTests.cs` (stays, `Member=Group`) | `MigrationsApplyAndModelHasNoPendingMigrations`, `AgentRunJsonbAndAgentStepForeignKeyRoundTrip` |

3. Run `dotnet test api/tests/FoundU.Tests --filter "Category=PostgreSql"` (with `TEST_DATABASE_URL`
   set): everything passes, **0 skipped**. Then the whole API suite.

📸 **SCREENSHOT (each member):** terminal of
`dotnet test api/tests/FoundU.Tests --filter "Category=PostgreSql&Member=Member{N}-{Name}" --logger "console;verbosity=normal"`
showing each test name Passed and the total.
📸 **SCREENSHOT (Uthpala):** her new database test file open in the editor next to its passing run.

### Task B: Integration / E2E per member (area 5)

Move the Playwright specs in `testing/e2e/tests/` into member folders and fix the relative imports
(`./support/api` becomes `../support/api`). `testDir` already searches subfolders.

| Folder | Specs |
|---|---|
| `testing/e2e/tests/member1_jaliya/` | `auth.spec.ts`, `admin-delete.spec.ts` + **new** `support-ticket.spec.ts`: a student opens a ticket in Help & support, the chatbot answers a how-to question, an escalated draft is sent, staff see it in the Support queue with the Assistant badge and reply, the student sees the reply |
| `testing/e2e/tests/member2_ranasinghe/` | `tracker.spec.ts`; also move `web/e2e/web-app.spec.ts` (+ its global setup) here or leave it in `web/e2e` and reference it from his runner |
| `testing/e2e/tests/member3_uthpala/` | `desk-handin.spec.ts`, `found-claim.spec.ts` |
| `testing/e2e/tests/member4_braveena/` | `claim-workflow.spec.ts` (E2E-WF-01, the complete workflow), `desk-in-person.spec.ts` |
| stays group-owned | `accessibility.spec.ts`, `csp.spec.ts`, `regressions.spec.ts`, `support/` |

Run each folder: `cd testing/e2e && npx playwright test tests/member{n}_{name}`. All must pass.

📸 **SCREENSHOT (each member):** the Playwright HTML report (`testing/reports/e2e/html/index.html`)
filtered to their folder, all passed; plus one step screenshot from inside the report that shows
their feature (Jaliya: the ticket in the Support queue; Ranasinghe: the tracker; Uthpala: the desk
receiving a hand-in code; Braveena: the collection code and "Collected").

### Task C: Security per member (area 6a)

`testing/security/build_collection.py` generates `foundu-security.postman_collection.json` (SEC-01
to SEC-36). Give the collection **one folder per member** plus a "Group" folder, by endpoint owner:

- Member 1: auth, admin (`/api/admin/*`), account email (forgot password, confirm, rate limit),
  Google, support tickets, security headers, NUL-byte cases.
- Member 2: lost reports (IDOR on another student's report, its messages, withdraw), report
  validation (1000/1001 characters, time window), photo upload checks.
- Member 3: found reports / desk endpoints (`/api/found-reports`, `/api/desk/codes`, match
  generation is Staff-only, a student cannot see the hidden detail).
- Member 4: claims (another student's claim, answering for another claim, approve as a student,
  collection by code needs Staff and the ID check).
- Group: CORS, the AI service key, generic token forgery.

Add missing cases so each member has at least 6, with normal, invalid and forbidden ones. Each
request has `pm.test` assertions on status and body. Regenerate the collection and run per folder:
`npx newman run testing/security/foundu-security.postman_collection.json -e testing/security/local.postman_environment.json --folder "Member 1"`.
Jaliya runs hers in the **Postman desktop app** (Collection Runner).

📸 **SCREENSHOT (each member):** Postman Collection Runner (or the Newman report) for their folder,
every assertion passed.
📸 **SCREENSHOT (Jaliya):** Postman request for SEC-28 showing **429 Too Many Requests** on the sixth
password-reset request, with the Tests tab results.

### Task D: Performance per member (area 6b)

`testing/performance/` has `common.js`, `load.js`, `stress.js`, `login-spike.js`. Add one k6 script
per member, reusing `common.js` for sign-in and the base URL, testing **their busiest endpoints**:

| Script | Endpoints | Load |
|---|---|---|
| `member1_jaliya.js` | `GET /api/admin/users`, `GET /api/support/tickets` (staff queue), sign-in | ramp to 20 VUs, 1 min |
| `member2_ranasinghe.js` | `GET /api/lost-reports` (feed), feed search, `GET` my reports | ramp to 50 VUs, 1 min |
| `member3_uthpala.js` | `GET` Found board, desk storage list, desk code lookup | ramp to 30 VUs, 1 min |
| `member4_braveena.js` | `GET` my claims, staff claims queue, a claim's detail | ramp to 30 VUs, 1 min |

Check the real routes in `api/src/FoundU.Api/Controllers/` before writing them. Thresholds:
`http_req_duration p(95) < 500 ms`, `http_req_failed < 1%`. Export
`--summary-export testing/reports/performance/member{n}-summary.json`. Run against the **local**
stack only, never Render.

📸 **SCREENSHOT (each member):** the k6 end-of-test summary for their script, thresholds ✓.

### Task E: Accessibility per member (area 6c, optional but quick)

In `testing/e2e/tests/accessibility.spec.ts`, the axe scan covers 11 pages. Split the page list so
each member's pages are a `test.describe('Member N')` block (Jaliya: Users, Support queue, Help &
support; Ranasinghe: report form, My reports, feed; Uthpala: Log an item, Found items, Found board;
Braveena: My claims, claims queue, claim page).

📸 **SCREENSHOT (each member):** their accessibility tests passing (0 violations).

### Task F: Mobile per member (area 4)

`flutter test test/member{n}_{name} --reporter expanded` for each member. If a moved test fails
because of a path, fix the import. If the API is running, also run
`flutter test test/integration` once.

📸 **SCREENSHOT (each member):** their flutter test run, all passed.
📸 **SCREENSHOT (each member):** one screen of their feature running in the emulator
(Jaliya: support chatbot or Google sign-in; Ranasinghe: report form with photo; Uthpala: Found board
or possible-match score; Braveena: answering verification questions).

### Task G: Extend the per-member runner

Update `testing/run_member_tests.sh` and `testing/summarize_member_results.py` so the parts are
`api db web mobile e2e security perf ai`:

- `api`: `--filter "Member=<tag>&Category!=PostgreSql"`
- `db`: `--filter "Member=<tag>&Category=PostgreSql"` (skips clearly if `TEST_DATABASE_URL` unset)
- `e2e`: Playwright on `tests/<folder>`, JUnit to `results/e2e.xml` (skip clearly if the web app
  is not reachable)
- `security`: Newman `--folder "Member N"` with the JUnit reporter to `results/security.xml`
  (skip if the API is not reachable)
- `perf`: k6 member script with `--summary-export results/perf.json`, show p95 and error rate in
  the summary
- keep `web`, `mobile`, `ai`

Update each `testing/member*/README.md` table (one row per area) and `testing/README.md`.

### Task H: Run everything and collect evidence

For each member: `testing/member{n}_{name}/run_tests.sh` (all parts, stack running, database
URL set). Every part must pass. Copy each `results/SUMMARY.md` to
`testing/reports/members/member{n}_{name}-SUMMARY.md` (that folder is committed as evidence).

📸 **SCREENSHOT (each member):** the final Summary table printed by their `run_tests.sh`, every
area listed and every count passed.
📸 **SCREENSHOT (each member):** `testing/member{n}_{name}/agent_demo.sh --sample`, the first
report (input, plan, output, trace).
📸 **SCREENSHOT (each member):** GitHub: their commits on `testing/QMSE-assignment`
(Insights › Contributors, or the commits list filtered by author).

### Task I: Commit and push

- Commit per task with clear messages. Run `dotnet test`, `pytest`, `npm test` (web), `npm run lint`
  (web) and `flutter analyze` before pushing; all green.
- Push to `origin testing/QMSE-assignment`. Then open a pull request to `main` so CI runs the four
  suites (including Flutter) on the new layout. Do not merge without the team agreeing.
- **Ownership:** the brief marks each student on their own Git history. Each member should make the
  commit for their own new tests from their own machine/account (for example Uthpala commits
  `Member3DatabaseTests.cs`, Jaliya commits `support-ticket.spec.ts`). If you are working on one
  machine, prepare the change and let that member commit it.

### Task J: Report text (Section 10 of the Software Testing Report)

Restructure each member's part of Section 10 to one subsection per area, in the brief's order, and
write the content from the real results:

```
10.x Member N: name (ID): business component(s), agent, group testing area
  10.x.1 Backend / API testing        tool and why, set-up command, test case table, results
  10.x.2 Database testing
  10.x.3 React web testing
  10.x.4 Flutter mobile testing
  10.x.5 Integration / E2E testing
  10.x.6 Non-functional testing        security (Postman/Newman, ZAP findings on their endpoints), performance (k6), accessibility (axe)
  10.x.7 Agentic AI testing            agent tests + agent demo input/output
  10.x.8 Defects found, fixes and retesting
  10.x.9 Traceability (folders, commits) and viva commands
```

Each test case table uses the brief's columns: **ID, feature, preconditions, steps/input, expected
result, actual result, Pass/Fail**, with normal (N), invalid (I), boundary (B) and failure (F)
cases. Defect rows: **ID, description, severity/priority, steps to reproduce, evidence, status,
retest result**. Put a figure placeholder `[ SCREENSHOT: … ]` wherever a 📸 item above belongs, so
the team can paste the images in. Write the text as `testing/reports/members/section10.md`. If the
team's Word file is in the repo or given to you, update it with python-docx, keeping its styles and
Times New Roman.

## 4. Rules

- Every result in the report must come from a run on this machine; never invent numbers.
- A failing test is investigated: fix the code if the test is right, fix the test only if it is
  wrong, and record real defects (ID, steps, fix, retest) for Section 10.
- Keep tests deterministic: AI tests use `LLM_PROVIDER=fake` unless a step says live.
- Never commit secrets (`.env`, keys, tokens). Screenshots must not show passwords or tokens: crop
  or blur them.
- When a 📸 item is reached, stop and tell the user: which member, what must be on screen, and the
  file name to save it as (`testing/reports/members/screenshots/m{n}-<area>-<short>.png`).

## 5. Done when

- [ ] Each member has tests in all seven areas, in folders named after them.
- [ ] `testing/member{n}_{name}/run_tests.sh` runs all of them and every count passes.
- [ ] Evidence summaries are in `testing/reports/members/`, screenshots in `testing/reports/members/screenshots/`.
- [ ] READMEs and `testing/README.md` match the new layout.
- [ ] CI is green on the pull request.
- [ ] Section 10 text is written with placeholders for every screenshot.
