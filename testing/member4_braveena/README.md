# Member 4: Braveena S (IT24100354)

**Business component:** Claims and ownership verification
**Agent:** Verification agent (with the Coordinator agent)

Staff record a hidden detail when an item is logged. The Verification agent drafts up to three questions about the kind of detail (never its value) and grades the claimant's answers: match, partial match or no match, then likely match, unlikely match or manual review. The Coordinator agent maps the claim's state to the next step and pauses the workflow until a staff member decides. Neither agent can approve or reject a claim.

## Where my tests are

Every test I own sits in a folder named after me, one row per testing area of the brief.
Counts are from the last full run of `run_tests.sh` (see `testing/reports/members/member4_braveena-SUMMARY.md`).

| # | Area | Folder / file | Tool | Tests |
| --- | --- | --- | --- | ---: |
| 1 | Backend / API | `api/tests/FoundU.Tests/Member4_Braveena/` | xUnit, WebApplicationFactory | 190 |
| 2 | Database | `api/tests/FoundU.Tests/Member4_Braveena/Member4DatabaseTests.cs` | xUnit on real PostgreSQL 16, EF Core | 6 |
| 3 | React web | `web/tests/member4_braveena/` | Vitest, React Testing Library | 13 |
| 4 | Flutter mobile | `mobile/test/member4_braveena/` | flutter_test | 32 |
| 5 | Integration / E2E | `testing/e2e/tests/member4_braveena/` (`claim-workflow.spec.ts`, `desk-in-person.spec.ts`) | Playwright | 2 |
| 6a | Security | folder "Member 4" in `testing/security/cases/member4_braveena.py` | Postman / Newman | 20 assertions |
| 6b | Performance | `testing/performance/member4_braveena.js` | k6 | 6 thresholds |
| 6c | Accessibility | `testing/e2e/tests/accessibility.spec.ts`, "Member 4" block (My claims, claims queue, a claim) | axe-core | 3 pages |
| 7 | Agentic AI | `ai/tests/member4_braveena/` | pytest through `/agents/run` | 136 |

The API and database tests carry `[Trait("Member", "Member4-Braveena")]` (the database ones also
`[Trait("Category", "PostgreSql")]`), so `dotnet test --filter "Member=Member4-Braveena"` runs exactly these.

## Run my tests

```bash
testing/start-stack.sh                         # PostgreSQL, AI (fake model), API, web
export TEST_DATABASE_URL="Host=localhost;Port=5434;Database=foundu_test;Username=foundu;Password=foundu"
testing/member4_braveena/run_tests.sh                       # every area
testing/member4_braveena/run_tests.sh db e2e security       # only some: api db web mobile e2e security perf ai
```

The script prints every test with its result and writes `testing/member4_braveena/results/SUMMARY.md`: a
table with one row per area and every test case marked Pass or Fail, beside the raw reports
(`api.trx`, `db.trx`, `web.xml`, `mobile.json`, `e2e.xml`, `a11y.xml`, `security.xml`, `perf.json`,
`ai.xml`) and the Playwright and Newman HTML reports (`e2e-html/`, `newman/`).

Needs: .NET 8 SDK, the AI service's virtual environment, `npm ci` in `web/` and `testing/e2e/`,
Flutter, Docker (PostgreSQL and Newman) and k6. A part whose tool or service is missing is
reported as skipped, with the reason. The database part needs `TEST_DATABASE_URL` naming a
test-only database (its name must contain "test"). Performance runs only against the local stack.

## Show my agent working

```bash
testing/member4_braveena/agent_demo.sh            # type your own input
testing/member4_braveena/agent_demo.sh --sample   # prepared inputs, including an attack
testing/member4_braveena/agent_demo.sh --live     # the same, against the real model in LLM_PROVIDER
```

The demo sends the input through the AI service's real `/agents/run` endpoint, the route the API
calls, and prints a report: the input, the plan the agent followed, its output, the trace the
service recorded, and the model and time. By default it uses the deterministic `fake` model, so
no keys are needed and every run gives the same answer. Add `--json` to see the raw response.

## My agent test cases

`ai/tests/member4_braveena/test_member4_braveena_verification_agent.py`, through the real `/agents/run` endpoint.

| ID | Type | What it checks |
| --- | --- | --- |
| M4-VER-01 | Normal | Drafts one to three questions from the hidden detail |
| M4-VER-02 | Failure / safety | Questions ask about the category never the value |
| M4-VER-03 | Normal | A faithful answer is a match |
| M4-VER-04 | Normal | A wrong answer is not a match |
| M4-VER-05 | Boundary / edge | An empty answer is insufficient |
| M4-VER-06 | Boundary / edge | Keyword stuffing never reaches a match |
| M4-VER-07 | Failure / safety | Grading never returns the hidden detail |
| M4-VER-08 | Prompt injection | An answer that instructs the grader is not a match |
| M4-VER-09 | Invalid input | A malformed request fails safely without echoing evidence |
| M4-COO-10 | Normal | A claim under review pauses for staff |
| M4-COO-11 | Normal | A claim waiting for answers waits for the claimant |
| M4-COO-12 | Approval enforcement | The workflow cannot resume without a person |
| M4-COO-13 | Approval enforcement | After staff approve it resumes once and only recommends |
| M4-AP-14 | Approval enforcement | Neither agent holds approval permission |
| M4-VER-15 | Failure / safety | A request without the service key is rejected |
