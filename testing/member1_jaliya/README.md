# Member 1: Jaliya H. A. W (IT24101976)

**Business component:** Administration and user management (with mailing and Google sign-in), and support tickets
**Agent:** AI support chatbot (the Support agent)

Two business components: **administration and user management** (the Users page and API: search, roles, suspend and reinstate, delete; reference data; analytics; confirmation and reset email; Google sign-in) and **support tickets** (students open tickets, staff work the Support queue). The agent is the **AI support chatbot**: a student describes a problem, and it answers from FoundU's own help guide, asks once for more detail, or drafts a ticket for the Support queue. The language model only picks which guide topic fits, so it cannot invent a policy, and the chatbot can never open a ticket, approve anything or reveal a code.

## Where my tests are

Every test I own sits in a folder named after me, one row per testing area of the brief.
Counts are from the last full run of `run_tests.sh` (see `testing/reports/members/member1_jaliya-SUMMARY.md`).

| # | Area | Folder / file | Tool | Tests |
| --- | --- | --- | --- | ---: |
| 1 | Backend / API | `api/tests/FoundU.Tests/Member1_Jaliya/` | xUnit, WebApplicationFactory | 125 |
| 2 | Database | `api/tests/FoundU.Tests/Member1_Jaliya/Member1DatabaseTests.cs` | xUnit on real PostgreSQL 16, EF Core | 2 |
| 3 | React web | `web/tests/member1_jaliya/` | Vitest, React Testing Library | 19 |
| 4 | Flutter mobile | `mobile/test/member1_jaliya/` | flutter_test | 50 |
| 5 | Integration / E2E | `testing/e2e/tests/member1_jaliya/` (`auth.spec.ts`, `admin-delete.spec.ts`, `support-ticket.spec.ts`) | Playwright | 16 |
| 6a | Security | folder "Member 1" in `testing/security/cases/member1_jaliya.py` | Postman / Newman | 28 assertions |
| 6b | Performance | `testing/performance/member1_jaliya.js` | k6 | 6 thresholds |
| 6c | Accessibility | `testing/e2e/tests/accessibility.spec.ts`, "Member 1" block (Login, register, admin Users, Support queue, Help & support) | axe-core | 5 pages |
| 7 | Agentic AI | `ai/tests/member1_jaliya/` | pytest through `/agents/run` | 23 |

The API and database tests carry `[Trait("Member", "Member1-Jaliya")]` (the database ones also
`[Trait("Category", "PostgreSql")]`), so `dotnet test --filter "Member=Member1-Jaliya"` runs exactly these.

## Run my tests

```bash
testing/start-stack.sh                         # PostgreSQL, AI (fake model), API, web
export TEST_DATABASE_URL="Host=localhost;Port=5434;Database=foundu_test;Username=foundu;Password=foundu"
testing/member1_jaliya/run_tests.sh                       # every area
testing/member1_jaliya/run_tests.sh db e2e security       # only some: api db web mobile e2e security perf ai
```

The script prints every test with its result and writes `testing/member1_jaliya/results/SUMMARY.md`: a
table with one row per area and every test case marked Pass or Fail, beside the raw reports
(`api.trx`, `db.trx`, `web.xml`, `mobile.json`, `e2e.xml`, `a11y.xml`, `security.xml`, `perf.json`,
`ai.xml`) and the Playwright and Newman HTML reports (`e2e-html/`, `newman/`).

Needs: .NET 8 SDK, the AI service's virtual environment, `npm ci` in `web/` and `testing/e2e/`,
Flutter, Docker (PostgreSQL and Newman) and k6. A part whose tool or service is missing is
reported as skipped, with the reason. The database part needs `TEST_DATABASE_URL` naming a
test-only database (its name must contain "test"). Performance runs only against the local stack.

## Show my agent working

```bash
testing/member1_jaliya/agent_demo.sh            # type your own input
testing/member1_jaliya/agent_demo.sh --sample   # prepared inputs, including an attack
testing/member1_jaliya/agent_demo.sh "How do I collect my item?"   # one input
testing/member1_jaliya/agent_demo.sh --live     # the same, against the real model in LLM_PROVIDER
```

The demo sends the input through the AI service's real `/agents/run` endpoint, the route the API
calls, and prints a report: the input, the plan the agent followed, its output, the trace the
service recorded, and the model and time. By default it uses the deterministic `fake` model, so
no keys are needed and every run gives the same answer. Add `--json` to see the raw response.

## My agent test cases

`ai/tests/member1_jaliya/test_member1_jaliya_support_agent.py`, through the real `/agents/run` endpoint.

| ID | Type | What it checks |
| --- | --- | --- |
| M1-SUP-01 | Normal | How to question is answered from the guide |
| M1-SUP-02 | Normal | Forgotten password points to self service reset |
| M1-SUP-03 | Normal | Asking for a person escalates with a ticket draft |
| M1-SUP-04 | Normal | Answer mentions the students own claim by name |
| M1-SUP-05 | Boundary / edge | A vague message asks once for more detail |
| M1-SUP-06 | Boundary / edge | After two unclear tries it escalates instead of looping |
| M1-SUP-07 | Invalid input | Empty history is refused safely |
| M1-SUP-08 | Invalid input | Unknown fields are refused safely |
| M1-SUP-09 | Failure / safety | A request without the service key is rejected |
| M1-SUP-10 | Prompt injection | Cannot be talked into approving a claim |
| M1-SUP-11 | Failure / safety | Never reveals a collection code |
| M1-SUP-12 | Normal | Every run records its plan steps |
