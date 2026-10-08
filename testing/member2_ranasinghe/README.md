# Member 2: Ranasinghe R.G.P.D (IT24100910)

**Business component:** Lost item reporting and tracking (with "I found this" hand-ins)
**Agent:** Description-Parsing agent

When a lost report is posted, the agent reads the student's description and returns the item type, colours and up to five identifying features as strict JSON. Every fact must appear in the student's own words; anything else is dropped, and if the model fails a rule-based parser gives the same kind of result.

## Where my tests are

Every test I own sits in a folder named after me, one row per testing area of the brief.
Counts are from the last full run of `run_tests.sh` (see `testing/reports/members/member2_ranasinghe-SUMMARY.md`).

| # | Area | Folder / file | Tool | Tests |
| --- | --- | --- | --- | ---: |
| 1 | Backend / API | `api/tests/FoundU.Tests/Member2_Ranasinghe/` | xUnit, WebApplicationFactory | 35 |
| 2 | Database | `api/tests/FoundU.Tests/Member2_Ranasinghe/Member2DatabaseTests.cs` | xUnit on real PostgreSQL 16, EF Core | 1 |
| 3 | React web | `web/tests/member2_ranasinghe/` | Vitest, React Testing Library | 34 |
| 4 | Flutter mobile | `mobile/test/member2_ranasinghe/` | flutter_test | 37 |
| 5 | Integration / E2E | `testing/e2e/tests/member2_ranasinghe/` (`tracker.spec.ts`, + `web/e2e/web-app.spec.ts`) | Playwright | 8 |
| 6a | Security | folder "Member 2" in `testing/security/cases/member2_ranasinghe.py` | Postman / Newman | 29 assertions |
| 6b | Performance | `testing/performance/member2_ranasinghe.js` | k6 | 6 thresholds |
| 6c | Accessibility | `testing/e2e/tests/accessibility.spec.ts`, "Member 2" block (Report form, My reports, lost feed) | axe-core | 3 pages |
| 7 | Agentic AI | `ai/tests/member2_ranasinghe/` | pytest through `/agents/run` | 55 |

The API and database tests carry `[Trait("Member", "Member2-Ranasinghe")]` (the database ones also
`[Trait("Category", "PostgreSql")]`), so `dotnet test --filter "Member=Member2-Ranasinghe"` runs exactly these.

## Run my tests

```bash
testing/start-stack.sh                         # PostgreSQL, AI (fake model), API, web
export TEST_DATABASE_URL="Host=localhost;Port=5434;Database=foundu_test;Username=foundu;Password=foundu"
testing/member2_ranasinghe/run_tests.sh                       # every area
testing/member2_ranasinghe/run_tests.sh db e2e security       # only some: api db web mobile e2e security perf ai
```

The script prints every test with its result and writes `testing/member2_ranasinghe/results/SUMMARY.md`: a
table with one row per area and every test case marked Pass or Fail, beside the raw reports
(`api.trx`, `db.trx`, `web.xml`, `mobile.json`, `e2e.xml`, `a11y.xml`, `security.xml`, `perf.json`,
`ai.xml`) and the Playwright and Newman HTML reports (`e2e-html/`, `newman/`).

Needs: .NET 8 SDK, the AI service's virtual environment, `npm ci` in `web/` and `testing/e2e/`,
Flutter, Docker (PostgreSQL and Newman) and k6. A part whose tool or service is missing is
reported as skipped, with the reason. The database part needs `TEST_DATABASE_URL` naming a
test-only database (its name must contain "test"). Performance runs only against the local stack.

## Show my agent working

```bash
testing/member2_ranasinghe/agent_demo.sh            # type your own input
testing/member2_ranasinghe/agent_demo.sh --sample   # prepared inputs, including an attack
testing/member2_ranasinghe/agent_demo.sh "Black laptop bag, grey zipper, small keychain."   # one input
testing/member2_ranasinghe/agent_demo.sh --live     # the same, against the real model in LLM_PROVIDER
```

The demo sends the input through the AI service's real `/agents/run` endpoint, the route the API
calls, and prints a report: the input, the plan the agent followed, its output, the trace the
service recorded, and the model and time. By default it uses the deterministic `fake` model, so
no keys are needed and every run gives the same answer. Add `--json` to see the raw response.

## My agent test cases

`ai/tests/member2_ranasinghe/test_member2_ranasinghe_parser_agent.py`, through the real `/agents/run` endpoint.

| ID | Type | What it checks |
| --- | --- | --- |
| M2-PAR-01 | Normal | Conversational description gives type colour and feature |
| M2-PAR-02 | Normal | Golden case from the design document |
| M2-PAR-03 | Normal | Output always has the strict schema |
| M2-PAR-04 | Boundary / edge | Vague description is not guessed |
| M2-PAR-05 | Boundary / edge | Empty description is marked invalid not crashing |
| M2-PAR-06 | Boundary / edge | Never more than five features |
| M2-PAR-07 | Failure / safety | Every feature is grounded in the students words |
| M2-PAR-08 | Prompt injection | Injected instructions are not stored as features |
| M2-PAR-09 | Invalid input | Missing description field is refused safely |
| M2-PAR-10 | Failure / safety | A failing model falls back to the rule based parser |
| M2-PAR-11 | Failure / safety | A request without the service key is rejected |
| M2-PAR-12 | Normal | Trace shows the planned steps |
