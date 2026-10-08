# Jaliya: hands-on testing guide and viva prep (SE3090 Assignment 2)

Deadline: **5 October 2026**. Viva straight after.

Work through the labs in order. Each one tells you what to run and what to screenshot. Each
also gives you the sentence to say in the viva. Run everything yourself, in your own terminal
and browser. Start every terminal with `whoami && date` so your screenshots prove it was you.

---

## 1. What the assignment marks (and where you stand)

| Marks | What the examiner wants | What you will show |
|---|---|---|
| Individual tool demo (15) | You run a testing tool on the real system and explain why you chose it and how it is set up | Postman/Newman, xUnit, Playwright (Labs 2–4) |
| Individual test implementation (15) | Tests in your area, with normal, invalid, boundary and failure cases, that you can run and explain | `AdminUserServiceTests.cs`, 19 runs plus the ones you add (Lab 3B) |
| Individual results, defects, retest (10) | A defect you found, why it happened, the fix, and the retest | SEC-27/28 headers and rate limit, the accessibility fixes, support FR02 (Lab 7) |
| Individual technical contribution (5) | Your commits in git | Your test commit from Lab 3B, plus your fix commits |
| Individual viva (15) | Run, explain, **modify** or troubleshoot a test live | Practise Lab 3C and section 9 |
| Group (40) | Strategy, E2E plus non-functional demo, documents | Already in the testing report. You present your part |

Your admin area now has its own tests (`AdminUserServiceTests.cs`, Lab 3B). Learn them,
extend them with one or two of your own, and commit them from your account.

---

## 0. Before every practice session (5 minutes)

Open Docker Desktop first. Then run each server in its own terminal:

```bash
cd ~/UNI/my/Github/FoundU && docker compose up -d postgres                     # T1
cd ~/UNI/my/Github/FoundU && testing/start-ai.sh                               # T2
cd ~/UNI/my/Github/FoundU/api && AiService__ServiceKey=$(cat ~/.foundu-ai-key) dotnet run --project src/FoundU.Api --launch-profile http   # T3
cd ~/UNI/my/Github/FoundU/web && npm run dev                                   # T4
cd ~/UNI/my/Github/FoundU && FOUNDU_ADMIN_PASSWORD=Admin123 python3 scripts/demo_seed.py   # T5, demo accounts
```

Demo accounts use the password `Demo!Pass2026`: `amara@foundu.test` (student) and
`priya@foundu.test` (staff). The admin is `admin@foundu.com` / `Admin123`.

---

## Lab 1: Swagger, manual API exploration (20 min)

**Why:** you need to understand the endpoints before testing them. Swagger is a documentation and
exploration tool, **not** an automated test tool. Use it to learn the API and to show 401/403
by hand. The marks come from the automated tools in the labs after this one.

1. Open http://localhost:5292/swagger.
2. Find **Auth → POST /api/auth/login** → **Try it out**. Use this body:
   ```json
   { "email": "admin@foundu.com", "password": "Admin123" }
   ```
   Click **Execute**. Copy the `accessToken` value from the response, without the quotes.
3. Click **Authorize** (top right), paste the token, then click **Authorize** and **Close**.
4. Call **GET /api/admin/users**. Expect **200** and a list of users. Screenshot it.
5. Pick a student's `id` from that list. Call **POST /api/admin/users/{id}/suspend** with:
   - `{ "reason": "too short" }` (9 characters) → expect **400**. The validator needs at least 10. This is a **boundary** case.
   - `{ "reason": "Posting fake found items" }` → expect **200**, `isSuspended: true`
   - the same call again → expect **409** "already suspended". This is a **failure** case.
   - **POST …/reinstate** → expect **200**.


Student	id
nadia@foundu.test	5145ed15-42a6-4e89-98be-668d199a7c33 (use this one)
dev@foundu.test	6ef1b913-80e0-452c-bd5e-136e0c1b40e0
kasun@foundu.test	f3e6483e-7123-4e98-9803-e3e7074ffcd9
amara@foundu.test	2bedd8c1-a310-44d9-8a9f-b940b9661b79

Demo!Pass2026


6. Click **Logout** in Authorize, log in as `amara@foundu.test` instead, authorize, and call
   **GET /api/admin/users** → expect **403**. Remove the token completely and call it again →
   expect **401**.

**Screenshot:** the 200, the 400, the 403 and the 401 responses.
**Say in the viva:** "Swagger shows every endpoint and its policy. I used it to explore
the admin API and to check by hand that a student gets 403 and an anonymous caller 401. The
same checks are automated in Newman (SEC-12) and Playwright (E2E-AUTH-08/09)."

---

## Lab 2: Postman and Newman, API security testing (30 min)

**Why Postman/Newman:** it sends real HTTP requests to the running API and checks each response
with assertions. That makes it right for authentication, authorisation and input attacks.
Newman is Postman's command-line runner, so the same collection runs in a terminal and in CI.

### 2A: in the Postman app (best screenshot)
1. Open Postman → **Import** → choose
   `testing/security/foundu-security.postman_collection.json` and
   `testing/security/local.postman_environment.json`.
2. Top right, select the **local** environment.
3. Right-click the collection → **Run collection** → **Run**.
4. Expect every request to pass. Click **SEC-12** ("staff cannot open admin user
   management") and open its **Tests** tab to see the assertion.

### 2B: in the terminal with Newman
```bash
cd ~/UNI/my/Github/FoundU && testing/security/run-newman.sh practice
open testing/reports/security/practice/newman-report.html
```

**Know these checks (your area):**

| ID | Checks | Expected |
|---|---|---|
| SEC-01 | Anonymous call to a protected endpoint | 401 |
| SEC-05 | Wrong password and unknown email get the same answer | Same 401, no account enumeration |
| SEC-08 | Registration ignores a smuggled `"role":"Admin"` | Account is still a Student |
| SEC-12 | Staff open admin user management | 403 |
| SEC-27 | Responses carry security headers | Headers present |
| SEC-28 | Forgot-password is rate limited | The 6th request in a minute gets 429 |

**Your defect story:** before the fix, SEC-27 and SEC-28 **failed** (see
`testing/reports/security/before-fix/newman-report.html`). You added rate limiting and security
headers in commit `408b827`, re-ran Newman, and all checks passed (`after-fix`).

**Modify it live (practice this):** in Postman, open SEC-12. In **Tests**, change the expected
status from 403 to 200 and run it. It **fails**. Change it back and it passes. This shows the
examiner you understand the assertion.

---

## Lab 3: xUnit, backend unit and service tests (60 min, the most important lab)

**Why xUnit:** it is .NET's standard test framework. It tests the C# service logic directly
against an in-memory database, so the tests are fast and repeatable and need no running server.

### 3A: run the tests in your area
```bash
cd ~/UNI/my/Github/FoundU/api
dotnet test --filter "FullyQualifiedName~SupportTicket|FullyQualifiedName~ReferenceAdmin|FullyQualifiedName~SupportAssistant|FullyQualifiedName~AccountEmail|FullyQualifiedName~GoogleTokenVerifier"
```
Expect 34 passed. Open `tests/FoundU.Tests/ReferenceAdminTests.cs` and read
`SomethingInUseCannotBeDeletedButCanBeRetired`. Be able to explain it line by line: arrange the
data, act by calling the service, assert the result.

### 3B: your admin tests (already written, so understand and re-run them)
The file is `api/tests/FoundU.Tests/AdminUserServiceTests.cs`. It has 19 test runs in 14 tests,
covering every case type the rubric asks for:

| Test | Type | What it proves |
|---|---|---|
| `AnAdminCanSuspendAStudentWithAReason` | Normal | Suspended, reason, who and when are all saved |
| `AnAdminCannotSuspendThemselves` | Invalid | `ValidationAppException` (400) |
| `AnotherAdminCannotBeSuspended` | Invalid / security | `ForbiddenAppException` (403) |
| `SuspendingTwiceIsAConflict` | Failure | `ConflictAppException` (409) |
| `SuspendingAnUnknownUserIsNotFound` | Failure | `NotFoundAppException` (404) |
| `SuspendingRevokesTheUsersRefreshTokens` | Security | The user's sessions end immediately |
| `ReinstatingClearsTheSuspension` | Normal | The flag, reason and who are cleared |
| `ReinstatingSomeoneNotSuspendedIsAConflict` | Failure | 409 |
| `AStudentCanBeMadeStaff` | Normal | The role changes |
| `AnAdminCannotChangeTheirOwnRole` | Invalid | 400 |
| `AnUnknownRoleIsRefused` (3 inputs) | Invalid | `"Superuser"`, `"9"` and `""` are refused |
| `GivingSomeoneTheRoleTheyHaveIsAConflict` | Failure | 409 |
| `TheSuspensionReasonMustBe10To500Characters` (5 inputs) | Boundary | 0 ✗, 9 ✗, 10 ✓, 500 ✓, 501 ✗ |

Run them:
```bash
cd ~/UNI/my/Github/FoundU/api && dotnet test --filter "FullyQualifiedName~AdminUserService" --logger "console;verbosity=normal"
```
Expect `Passed: 19`. The `verbosity=normal` option lists every test name, so use it for your
screenshot.

**Understand one test completely.** Open the file and read `AnAdminCanSuspendAStudentWithAReason`.
Every test has three parts. Be ready to point at each:
- **Arrange:** `SetUpAsync()` makes a fresh in-memory database with one admin and one student.
- **Act:** `service.SuspendAsync(student.Id, admin.Id, "...")`
- **Assert:** read the user back from the database and check each field.

Then open `api/src/FoundU.Infrastructure/Administration/AdminUserService.cs`, method
`SuspendAsync`. Find the line each test checks: the self-check throws `ValidationAppException`,
the admin check throws `ForbiddenAppException`, and so on. Being able to jump from a test to the
code it tests is what "technical understanding" means in the rubric.

**Build your confidence: add one test yourself.** Write this new test at the bottom of the class
and make it pass:
- `ReinstatingAnUnknownUserIsNotFound`: copy `SuspendingAnUnknownUserIsNotFound` and call
  `service.ReinstateAsync(Guid.NewGuid())` instead.
- Then try `AStudentCanBeMadeAdmin`: copy `AStudentCanBeMadeStaff` and use `"Admin"` and `UserRole.Admin`.

Re-run. Expect `Passed: 21`.

**Commit it from your own account** so it shows in your git history:
```bash
cd ~/UNI/my/Github/FoundU
git checkout -b test/admin-user-management
git add api/tests/FoundU.Tests/AdminUserServiceTests.cs
git commit -m "test: admin user management - suspend, reinstate, role change and reason boundaries"
git push -u origin test/admin-user-management      # then open a PR on GitHub and merge it
```
Declare honestly in your AI usage log that Claude drafted the first version, and that you
reviewed it, ran it, extended it and can explain it. The assignment allows this as long as you
declare it and understand it.

### 3C: practise modifying a test (examiners ask for this)
In `AnAdminCannotSuspendThemselves`, change `ValidationAppException` to `ConflictAppException` and run it. It fails with
"Assert.Throws() Failure… Expected: ConflictAppException, Actual: ValidationAppException".
Explain why, then change it back.

---

## Lab 4: Playwright, end-to-end and protected routes (30 min)

**Why Playwright:** it drives a real Chrome through the real web app, API and database, the same
way a user would. That's the right tool for login flows, route guards and complete workflows.

```bash
cd ~/UNI/my/Github/FoundU/testing/e2e
npx playwright test auth.spec.ts --ui
```
The Playwright window opens. Click ▶ next to `auth.spec.ts` and watch each test run in the
browser panel. **Screenshot this window**: it's the best visual evidence you have.

Know these tests (they are your area: login, roles, guards):
- **E2E-AUTH-03:** a wrong password is refused and the user stays on the sign-in page. *Invalid case.*
- **E2E-AUTH-05:** a 7-character password is refused and an 8-character one accepted. *Boundary.*
- **E2E-AUTH-08/09:** a student can't open the desk or admin area, and staff can't open admin pages. *Authorisation.*
- **E2E-AUTH-10:** sign-out revokes the refresh token. *Security.*

The group's complete integrated workflow is `claim-workflow.spec.ts`: the student claims, staff
ask questions, the student answers, staff approve, and the desk hands the item over. It covers
web, API, AI and PostgreSQL. Run it once too:
`npx playwright test claim-workflow.spec.ts --ui`

HTML report: `npx playwright show-report ../reports/e2e/html`

---

## Lab 5: Lighthouse and axe, accessibility and performance (20 min)

**Why:** you built the UI/UX. Lighthouse scores performance and accessibility. axe finds WCAG
2.1 AA violations.

1. **Lighthouse (by hand):** Chrome → https://foundu-web.onrender.com → right-click →
   **Inspect** → **Lighthouse** tab → Mode: Navigation, Device: Mobile → **Analyze page load**.
   Screenshot the four scores.
2. **axe (automated):**
   ```bash
   cd ~/UNI/my/Github/FoundU/testing/e2e && npx playwright test accessibility.spec.ts
   ```
   Expect 11 passed: 11 pages with 0 violations.

**Your defect story:**
- **Accessibility.** axe first found low colour contrast, buttons with no accessible name, and
  stray `<li>` elements on 6 pages (`testing/reports/accessibility/before/`). You fixed them in
  `8afce39`, and the retest shows 0 violations.
- **Performance.** Lighthouse showed one 1.4 MB JavaScript bundle. You split each page into its
  own chunk in `fbf1f23`. Performance went from 45 to 73 on the landing page, 49 to 74 on the
  feed, and 56 to 64 on sign-in.

---

## Lab 6: OWASP ZAP, security scan of the deployed site (20 min)

**Why:** ZAP is an automated web security scanner. The baseline scan is passive: it only reads
responses and attacks nothing, so it is safe to run against the live site. You deployed that
site.

```bash
cd ~/UNI/my/Github/FoundU
docker run --rm -v "$PWD/testing/reports/security":/zap/wrk:rw zaproxy/zap-stable \
  zap-baseline.py -t https://foundu-web.onrender.com -r zap-practice.html -I
open testing/reports/security/zap-practice.html
```

**Your defect story:** before the fix, ZAP raised 2 Medium alerts: no Content-Security-Policy
and no anti-clickjacking header. You added production security headers in `render.yaml`
(`107d80c`). The retest shows CSP and X-Frame-Options are present. Only `style-src
'unsafe-inline'` remains, because Google sign-in needs it. That's a known, justified risk.

Never point ZAP's *active* scan, or k6, at the production site. Run those only locally.

---

## Lab 7: pytest, the support AI agent (20 min)

**Why pytest:** the AI service is Python. pytest runs the agents directly, with deterministic
inputs and exact assertions.

```bash
cd ~/UNI/my/Github/FoundU/ai
.venv/bin/pytest tests/test_support.py -v                                   # 11 tests, your agent
.venv/bin/pytest tests/evaluation -v -k "TC08 or TC09 or FR02 or BR07 or PI03"   # its evaluation cases
```

**Your defect story:** the first evaluation run failed **FR02**. When the model timed out,
a forgotten-password question escalated to a ticket instead of pointing to the self-service
reset. **Cause:** the guide entry still said the desk resets passwords. **Fix:** the
guide now points to "Forgot password?" (`f4267c0`). **Retest:** 68/68 pass in deterministic and
live modes (`testing/reports/ai-eval/`).

---

## Lab 8: k6, performance (group, 10 min; performance testing is required)

```bash
cd ~/UNI/my/Github/FoundU/testing/performance && k6 run load.js
```
Read the summary. `http_req_duration p(95)` should be about 6 ms, `http_req_failed` 0%, and
every threshold ✓. **Load** is the expected traffic (50 users). **Stress** pushes to 300 users
to find the breaking point. **Spike** is 30 sign-ins at the same instant.

---

## 8. Screenshot checklist (save to ~/Desktop/foundu-evidence/)

- [ ] Swagger: 200 as admin, 400 on a 9-character reason, 403 as a student, 401 anonymous
- [ ] Postman collection runner, all passed
- [ ] Newman HTML report
- [ ] `AdminUserServiceTests`: 19 or more passing, with the test names listed
- [ ] Your PR or commit on GitHub
- [ ] Playwright UI mode running `auth.spec.ts`
- [ ] Lighthouse scores on the deployed site
- [ ] axe `accessibility.spec.ts` passing
- [ ] ZAP report
- [ ] pytest support tests passing
- [ ] GitHub Actions: the latest CI run, green

Add the screenshots to your individual section of the testing report.

---

## 9. Viva questions: practise answering these out loud

**Strategy**
- *Why these tools?* Each layer gets the tool built for it: xUnit for C# services, pytest for the
  Python agents, Playwright for real-browser workflows, Newman for HTTP-level security checks,
  k6 for load, ZAP for scanning, axe and Lighthouse for accessibility.
- *Unit vs integration vs E2E?* A unit test checks one service in isolation, with an in-memory
  database. Integration checks parts together: the API with real PostgreSQL, or mobile against a
  live API. E2E goes through the real UI, API, AI service and database together.
- *Why an in-memory database in your xUnit tests?* It's fast and isolated, with a fresh database
  per test. Provider-specific behaviour (unique indexes, `xmin` concurrency, migrations) is
  covered separately by the PostgreSQL integration tests.

**Your tests**
- *Normal, invalid, boundary, failure: show me one of each.* Normal: suspending a student.
  Invalid: suspending yourself. Boundary: a reason of 9, 10, 500 or 501 characters. Failure:
  suspending twice gives 409.
- *Why can't an admin suspend themselves or another admin?* They could lock themselves out
  for good, or two admins could lock each other out. Recovery would need direct database access.
- *What happens to a suspended user's existing session?* Their refresh tokens are revoked, and
  the token check refuses their access token on the next request (fix 40).

**Results and defects**
- *Tell me about a defect you found.* Pick SEC-28: forgot-password wasn't rate limited, so
  anyone could flood a mailbox. Found by the Newman suite. Fixed with a fixed-window limit of
  5 per minute per client. Retested: SEC-28 passes and the 6th request returns 429.
- *What does p95 mean?* 95% of requests were faster than this time. It's more honest than the
  average, because a few slow requests stand out.
- *Is your AI tested even though it's non-deterministic?* Yes, in two modes: deterministic (the
  fake model, so every fallback path is tested) and live against Groq. The model only picks a
  topic id that a schema checks, so the assertions stay exact.

**Live tasks they may give you**
- "Run your tests": `dotnet test --filter "FullyQualifiedName~AdminUserService"`
- "Make one fail and explain": Lab 3C
- "Add a case": add `[InlineData(1, false)]` to `TheSuspensionReasonMustBe10To500Characters` and re-run
- "Show me the CI": GitHub → Actions → latest run → the four jobs

---

## 10. Submission checklist

- [ ] Testing report PDF: test plan, scope, execution summary, defects, conclusion
- [ ] Test case document with expected result, actual result and Pass/Fail for each case
- [ ] Defect report with retest evidence (before and after files in `testing/reports`)
- [ ] Tool evidence: the screenshots above and `testing/reports/*`
- [ ] Test source code: `api/tests`, `ai/tests`, `web/tests`, `mobile/test`, `testing/e2e`, `testing/performance`, `testing/security`
- [ ] GitHub link and commit evidence (your test commit)
- [ ] Your AI usage declaration (CLEAR framework): what AI helped with, and what you checked and ran yourself
