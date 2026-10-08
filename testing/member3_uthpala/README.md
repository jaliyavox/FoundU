# Member 3: Uthpala W.A.S (IT24101028)

**Business component:** Found item management and matching
**Agent:** Matching agent

Staff pick a lost report for a found item. The agent reads both through its two allowed tools and scores only public evidence: different item types never match; then colour (20), identifying description (35), campus place (20) and time sequence (25). A score of 0.65 or more with matching details and no conflict is a candidate the student sees; anything uncertain goes back to staff. It never creates a claim and never sees the hidden detail.

## Where my tests are

Every test I own sits in a folder named after me, in each part of the system.

| Part | Folder | Tool | Tests |
| --- | --- | --- | ---: |
| API (ASP.NET Core) | `api/tests/FoundU.Tests/Member3_Uthpala/` | xUnit, WebApplicationFactory | 44 |
| AI service (agent) | `ai/tests/member3_uthpala/` | pytest, FastAPI TestClient | 48 |
| Web app (React) | `web/tests/member3_uthpala/` | Vitest, React Testing Library | 13 |
| Mobile app (Flutter) | `mobile/test/member3_uthpala/` | flutter_test | 7 |

The API tests also carry `[Trait("Member", "Member3-Uthpala")]`, so
`dotnet test --filter "Member=Member3-Uthpala"` runs exactly these. Mobile counts are the
number of `test`/`testWidgets` cases in the folder.

## Run my tests

```bash
testing/member3_uthpala/run_tests.sh            # API, AI, web and mobile
testing/member3_uthpala/run_tests.sh ai web     # only some parts
```

The script prints every test with its result and writes
`testing/member3_uthpala/results/SUMMARY.md`: a totals table and every test case marked Pass or Fail,
plus the raw reports (`api.trx`, `ai.xml`, `web.xml`, `mobile.json`).

Needs: .NET 8 SDK, the AI service's virtual environment (`cd ai && python -m venv .venv &&
.venv/bin/pip install -e ".[dev]"`), `npm ci` in `web/`, and Flutter for the mobile part. A part
whose tool is missing is reported as skipped. Set `TEST_DATABASE_URL` to a test-only PostgreSQL
database (its name must contain "test") to include the PostgreSQL integration tests.

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
