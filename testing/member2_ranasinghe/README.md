# Member 2: Ranasinghe R.G.P.D (IT24100910)

**Business component:** Lost item reporting and tracking (with "I found this" hand-ins)
**Agent:** Description-Parsing agent

When a lost report is posted, the agent reads the student's description and returns the item type, colours and up to five identifying features as strict JSON. Every fact must appear in the student's own words; anything else is dropped, and if the model fails a rule-based parser gives the same kind of result.

## Where my tests are

Every test I own sits in a folder named after me, in each part of the system.

| Part | Folder | Tool | Tests |
| --- | --- | --- | ---: |
| API (ASP.NET Core) | `api/tests/FoundU.Tests/Member2_Ranasinghe/` | xUnit, WebApplicationFactory | 35 |
| AI service (agent) | `ai/tests/member2_ranasinghe/` | pytest, FastAPI TestClient | 55 |
| Web app (React) | `web/tests/member2_ranasinghe/` | Vitest, React Testing Library | 34 |
| Mobile app (Flutter) | `mobile/test/member2_ranasinghe/` | flutter_test | 22 |

The API tests also carry `[Trait("Member", "Member2-Ranasinghe")]`, so
`dotnet test --filter "Member=Member2-Ranasinghe"` runs exactly these. Mobile counts are the
number of `test`/`testWidgets` cases in the folder.

## Run my tests

```bash
testing/member2_ranasinghe/run_tests.sh            # API, AI, web and mobile
testing/member2_ranasinghe/run_tests.sh ai web     # only some parts
```

The script prints every test with its result and writes
`testing/member2_ranasinghe/results/SUMMARY.md`: a totals table and every test case marked Pass or Fail,
plus the raw reports (`api.trx`, `ai.xml`, `web.xml`, `mobile.json`).

Needs: .NET 8 SDK, the AI service's virtual environment (`cd ai && python -m venv .venv &&
.venv/bin/pip install -e ".[dev]"`), `npm ci` in `web/`, and Flutter for the mobile part. A part
whose tool is missing is reported as skipped. Set `TEST_DATABASE_URL` to a test-only PostgreSQL
database (its name must contain "test") to include the PostgreSQL integration tests.

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
