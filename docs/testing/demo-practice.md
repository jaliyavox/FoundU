# FoundU: demo practice runbook (17.1 checklist)

Run every step yourself, in your own terminal and browser, and take the screenshots there.
To make each screenshot clearly yours:

- Start each terminal session with `whoami && date && git log -1 --oneline`. Your user, the
  date and the commit then appear above the test output.
- Leave the macOS menu-bar clock visible, and your GitHub avatar where the page shows it.
- Save screenshots to a single folder, e.g. `~/Desktop/foundu-evidence/NN-what.png`
  (Cmd+Shift+4, then Space, captures one window).
- Use the **GUI** version of a tool where one exists (Playwright UI mode, the Postman app,
  Lighthouse in Chrome DevTools, the GitHub Actions page). A screenshot of a GUI is much
  more convincing than a pasted log.

---

## 0. Setup (about 10 minutes, do it before every practice run)

```bash
cd ~/UNI/my/Github/FoundU
open -a Docker                                   # wait for the whale icon to settle
export AI_SERVICE_KEY=$(cat ~/.foundu-ai-key)    # or any long random string
testing/start-stack.sh                           # Postgres :5434, AI :8000, API :5292, web :5173
FOUNDU_ADMIN_PASSWORD=<your admin pw> python3 scripts/demo_seed.py   # note the 2 codes it prints
```

Accounts (password `Demo!Pass2026`): `amara@foundu.test` (student/owner), `kasun@foundu.test`
(student/finder), `priya@foundu.test` (staff), `admin@foundu.com` (admin, your own password).

Stop everything afterwards with `testing/stop-stack.sh`.

---

## 1. Log in with different roles and show protected operations

**In the browser** (http://localhost:5173):
1. Log in as `amara`. You land on My reports. Open `/admin`. **Screenshot: Forbidden.**
2. Log in as `priya` (staff). You land on Found items. There are no admin links. Open `/admin`
   again. Still forbidden.
3. Log in as `admin`. The Users, Analytics and Places pages all show. **Screenshot.**

**In the terminal** (proves the server enforces it, not just React):
```bash
TOK=$(curl -s localhost:5292/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"amara@foundu.test","password":"Demo!Pass2026"}' | python3 -c 'import sys,json;print(json.load(sys.stdin)["accessToken"])')
curl -i localhost:5292/api/admin/users -H "Authorization: Bearer $TOK" | head -1   # 403
curl -i localhost:5292/api/admin/users | head -1                                   # 401
```
**Screenshot: 401 and 403 together.** (If the login JSON uses a different field name, run the
first curl without the pipe to see it.)

## 2. CRUD + a business workflow, PostgreSQL changes, Swagger

**Swagger:** open http://localhost:5292/swagger. Click **Authorize** and paste `$TOK` (run
`echo $TOK`). Call `GET /api/lost-reports/mine` and **Try it out**. **Screenshot of the 200 response.**

**CRUD on the web as `amara`:** Create a lost report, Read it on My reports, Update (edit the
description), then Delete (Withdraw).

**Show the database after each step.** Keep a second terminal open:
```bash
docker exec -it foundu-postgres psql -U foundu -d foundu
\dt
SELECT "Id", LEFT("Description",40), "Status", "UpdatedAt" FROM "LostReports" ORDER BY "CreatedAt" DESC LIMIT 3;
SELECT * FROM "LostReportStatusHistories" ORDER BY 1 DESC LIMIT 5;
```
Column names may differ slightly; `\d "LostReports"` lists them. **Screenshot the row
before and after the edit, and the status after the withdraw.**

**Business workflow (handover):** follow Member 2's script, steps 3–5, in
[evaluation-guide.md](evaluation-guide.md): Kasun gets "I found this", then "give to security"
and a 6-digit code. Priya enters the code at the Handover desk, then Receive, then Release. Release
without ticking the ID check is refused. Run `SELECT * FROM "Handovers" ...` (or the
`StorageTransfers` table) to show the state change.

## 3. React and Flutter on the same ASP.NET Core API

```bash
cd mobile
flutter emulators --launch Pixel_10_Pro
flutter run --dart-define=FOUND_U_API_BASE_URL=http://10.0.2.2:5292
```
Put the emulator and the browser side by side. Create a report on the phone as `amara`, then
refresh My reports on the web. The new report appears there. **Screenshot of both windows**, plus
the API terminal log (`tail -f testing/reports/logs/api.log`) showing requests from both.

## 4. Agentic AI: the full minimum acceptance workflow

Do it in the UI and point at each item as you go:

| Requirement | Where to show it |
|---|---|
| Domain objective | Claim verification: decide whether this claimant owns the item, without leaking the private details |
| Structured plan | `ai/app/agents/plans.py`, and the plan steps listed in the agent-runs panel |
| Distinct agent roles | `ai/app/agents/graph.py`: parser, matching, verification, intake, coordinator, support |
| Allow-listed tools | `ai/app/tools/registry.py`. A request without the service key is refused: `curl -i -X POST localhost:8000/agents/run` gives **401** |
| Persisted state | `docker exec -it foundu-postgres psql -U foundu -d foundu -c 'SELECT * FROM ai_workflow_states LIMIT 3;'` and `SELECT * FROM "AgentRuns" ORDER BY 1 DESC LIMIT 5;` |
| Deterministic validation | Questions never contain the private detail. A schema-breaking model reply is refused (eval test SO04) |
| Authorized approval | Only staff can approve. As `amara`, calling `POST /api/claims/{id}/agent-workflows/{wid}/approval` in Swagger gives 403 |
| Auditable result / safe failure | The agent-runs panel, plus the `AgentSteps` and `ApprovalDecisions` rows. Stop the AI service (`lsof -ti:8000 \| xargs kill`): the report form still works through the fallback |

UI flow: `amara` claims an item. `priya` opens the claim and clicks **Generate questions**.
`amara` answers. `priya` sees the AI recommendation and the **workflow approval panel**.

Then run the evaluation yourself:
```bash
cd ai && .venv/bin/pytest tests/evaluation -v      # TC, AS, TS, SO, BR, PI categories by name
```
**Screenshot of the green list.**

## 5. Human approval and execution-history summaries

As `priya`, on the claim detail page:
1. In the workflow approval panel, **Approve** (or Reject with a reason). **Screenshot before and after.**
2. In the **Agent runs** panel, show each run's status, agent and outcome lines. **Screenshot.**
3. As admin, **Overturn** a rejection with a reason. Both decisions are kept:
   `SELECT * FROM "ApprovalDecisions" ORDER BY 1 DESC LIMIT 5;` shows `IsOverride`.

## 6. Error handling, tests, CI, deployment, contribution history

**Error handling:** run each case and screenshot the error the app shows:
- A lost report dated in the future gives a validation message (400).
- Registering the same email twice gives "already exists" (409).
- A description over 1000 characters gives a clean 400, not a 500.
- Stop the AI service while the report form is open. The form still works.

**Tests, one tool at a time** (each makes a good screenshot):

| Tool | Command (from the repo root) | What to screenshot |
|---|---|---|
| xUnit (.NET) | `cd api && dotnet test FoundU.sln` | Passed total |
| pytest | `cd ai && .venv/bin/pytest -q` | "N passed" |
| Vitest | `cd web && npx vitest --ui` | The Vitest UI in the browser |
| flutter_test | `cd mobile && flutter test` | "All tests passed!" |
| **Playwright** | `cd testing/e2e && npx playwright test --ui` | The UI mode window, after clicking ▶ on `auth.spec.ts` |
| Playwright report | `npx playwright test && npx playwright show-report` | HTML report in the browser |
| axe (a11y) | `npx playwright test accessibility.spec.ts` | Passing specs |
| **Lighthouse** | Chrome → http://localhost:5173 → DevTools → Lighthouse → Analyze | The score circles |
| **k6** | `cd testing/performance && k6 run load.js` | Live summary: checks, p95, http_req_failed |
| **Postman** | Postman app → Import `testing/security/foundu-security.postman_collection.json` + `local.postman_environment.json` → Run collection | Runner results with passes |
| Newman | `testing/security/run-newman.sh practice` then `open testing/reports/security/practice/newman-report.html` | HTML report |
| **OWASP ZAP** | `docker run --rm -v "$PWD/testing/reports/security":/zap/wrk:rw zaproxy/zap-stable zap-baseline.py -t http://host.docker.internal:5173 -r zap-practice.html -I` | Terminal summary, then the HTML report |

Run k6 and ZAP **only against localhost**, never against the Render deployment.

**CI:** open https://github.com/jaliyavox/FoundU/actions and click the latest green **CI** run.
**Screenshot of the four jobs** (.NET API, web, mobile, ai), each with a ✓. To show a run you
started yourself, make a small commit or PR and screenshot that run.

**Deployed apps:** open https://foundu-web.onrender.com and log in. Also open
https://foundu-api.onrender.com/api/lost-reports/feed (JSON). The free tier sleeps, so open them
about 2 minutes early.

**Contribution history:** https://github.com/jaliyavox/FoundU/graphs/contributors, plus the
Pull requests tab (#46 by a teammate, merged). In the terminal:
`git shortlog -sne --all`.

---

## Practice order (about 20 minutes per full run)

0 Setup → 1 Roles → 2 Swagger + CRUD + psql → 3 Web + Flutter → 4 AI workflow → 5 Approval
→ 6 Errors → tests (pick 3–4 tools; Playwright UI and k6 look best live) → CI → Render → GitHub.

Practise it twice. On the second run, say aloud *why* each thing happens. Section 4 of
[test-report.md](test-report.md) lists the known issues to raise before an examiner finds them.
