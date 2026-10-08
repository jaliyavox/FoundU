# Member 3: Uthpala W.A.S (IT24101028)

**Business component:** Found item management and matching
**Agent:** Matching agent

Staff pick a lost report for a found item. The agent reads both through its two allowed tools and scores only public evidence: different item types never match; then colour (20), identifying description (35), campus place (20) and time sequence (25). A score of 0.65 or more with matching details and no conflict is a candidate the student sees; anything uncertain goes back to staff. It never creates a claim and never sees the hidden detail.

## Where my tests are

Every test I own sits in a folder named after me, one row per testing area of the brief.
Counts are from the last full run of `run_tests.sh` (see `testing/reports/members/member3_uthpala-SUMMARY.md`).

| # | Area | Folder / file | Tool | Tests |
| --- | --- | --- | --- | ---: |
| 1 | Backend / API | `api/tests/FoundU.Tests/Member3_Uthpala/` | xUnit, WebApplicationFactory | 44 |
| 2 | Database | `api/tests/FoundU.Tests/Member3_Uthpala/Member3DatabaseTests.cs` | xUnit on real PostgreSQL 16, EF Core | 3 |
| 3 | React web | `web/tests/member3_uthpala/` | Vitest, React Testing Library | 13 |
| 4 | Flutter mobile | `mobile/test/member3_uthpala/` | flutter_test | 9 |
| 5 | Integration / E2E | `testing/e2e/tests/member3_uthpala/` (`desk-handin.spec.ts`, `found-claim.spec.ts`) | Playwright | 8 |
| 6a | Security | folder "Member 3" in `testing/security/cases/member3_uthpala.py` | Postman / Newman | 19 assertions |
| 6b | Performance | `testing/performance/member3_uthpala.js` | k6 | 6 thresholds |
| 6c | Accessibility | `testing/e2e/tests/accessibility.spec.ts`, "Member 3" block (Log an item, Found items, Found board, a found item) | axe-core | 4 pages |
| 7 | Agentic AI | `ai/tests/member3_uthpala/` | pytest through `/agents/run` | 48 |

The API and database tests carry `[Trait("Member", "Member3-Uthpala")]` (the database ones also
`[Trait("Category", "PostgreSql")]`), so `dotnet test --filter "Member=Member3-Uthpala"` runs exactly these.

## Run my tests

```bash
testing/start-stack.sh                         # PostgreSQL, AI (fake model), API, web
export TEST_DATABASE_URL="Host=localhost;Port=5434;Database=foundu_test;Username=foundu;Password=foundu"
testing/member3_uthpala/run_tests.sh                       # every area
testing/member3_uthpala/run_tests.sh db e2e security       # only some: api db web mobile e2e security perf ai
```

The script prints every test with its result and writes `testing/member3_uthpala/results/SUMMARY.md`: a
table with one row per area and every test case marked Pass or Fail, beside the raw reports
(`api.trx`, `db.trx`, `web.xml`, `mobile.json`, `e2e.xml`, `a11y.xml`, `security.xml`, `perf.json`,
`ai.xml`) and the Playwright and Newman HTML reports (`e2e-html/`, `newman/`).

Needs: .NET 8 SDK, the AI service's virtual environment, `npm ci` in `web/` and `testing/e2e/`,
Flutter, Docker (PostgreSQL and Newman) and k6. A part whose tool or service is missing is
reported as skipped, with the reason. The database part needs `TEST_DATABASE_URL` naming a
test-only database (its name must contain "test"). Performance runs only against the local stack.

## Show my agent working

```bash
testing/member3_uthpala/agent_demo.sh            # type your own input
testing/member3_uthpala/agent_demo.sh --sample   # prepared inputs, including an attack
testing/member3_uthpala/agent_demo.sh --live     # the same, against the real model in LLM_PROVIDER
```

The demo sends the input through the AI service's real `/agents/run` endpoint, the route the API
calls, and prints a report: the input, the plan the agent followed, its output, the trace the
service recorded, and the model and time. By default it uses the deterministic `fake` model, so
no keys are needed and every run gives the same answer. Add `--json` to see the raw response.

## My agent test cases

`ai/tests/member3_uthpala/test_member3_uthpala_matching_agent.py`, through the real `/agents/run` endpoint.

| ID | Type | What it checks |
| --- | --- | --- |
| M3-MAT-01 | Normal | Same bottle same place and time is a candidate |
| M3-MAT-02 | Normal | Result explains every factor |
| M3-MAT-03 | Boundary / edge | Different item types are never a match |
| M3-MAT-04 | Boundary / edge | Colour alone is never a match |
| M3-MAT-05 | Boundary / edge | Conflicting colour blocks a candidate |
| M3-MAT-06 | Boundary / edge | Item found before it was lost is a conflict |
| M3-MAT-07 | Normal | Score stays between zero and one |
| M3-MAT-08 | Invalid input | Unknown operation reads nothing and recommends nothing |
| M3-MAT-09 | Invalid input | A missing found report fails safely |
| M3-MAT-10 | Tool selection | Reads both reports with its tools and writes nothing |
| M3-MAT-11 | Prompt injection | Instructions in a description do not raise the score |
| M3-MAT-12 | Failure / safety | A request without the service key is rejected |
