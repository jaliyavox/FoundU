# Member 1: Jaliya H. A. W (IT24101976)

**Business component:** Administration and user management (with mailing and Google sign-in), and support tickets
**Agent:** AI support chatbot (the Support agent)

Two business components: **administration and user management** (the Users page and API: search, roles, suspend and reinstate, delete; reference data; analytics; confirmation and reset email; Google sign-in) and **support tickets** (students open tickets, staff work the Support queue). The agent is the **AI support chatbot**: a student describes a problem, and it answers from FoundU's own help guide, asks once for more detail, or drafts a ticket for the Support queue. The language model only picks which guide topic fits, so it cannot invent a policy, and the chatbot can never open a ticket, approve anything or reveal a code.

## Where my tests are

Every test I own sits in a folder named after me, in each part of the system.

| Part | Folder | Tool | Tests |
| --- | --- | --- | ---: |
| API (ASP.NET Core) | `api/tests/FoundU.Tests/Member1_Jaliya/` | xUnit, WebApplicationFactory | 125 |
| AI service (agent) | `ai/tests/member1_jaliya/` | pytest, FastAPI TestClient | 23 |
| Web app (React) | `web/tests/member1_jaliya/` | Vitest, React Testing Library | 19 |
| Mobile app (Flutter) | `mobile/test/member1_jaliya/` | flutter_test | 50 |

The API tests also carry `[Trait("Member", "Member1-Jaliya")]`, so
`dotnet test --filter "Member=Member1-Jaliya"` runs exactly these. Mobile counts are the
number of `test`/`testWidgets` cases in the folder.

## Run my tests

```bash
testing/member1_jaliya/run_tests.sh            # API, AI, web and mobile
testing/member1_jaliya/run_tests.sh ai web     # only some parts
```

The script prints every test with its result and writes
`testing/member1_jaliya/results/SUMMARY.md`: a totals table and every test case marked Pass or Fail,
plus the raw reports (`api.trx`, `ai.xml`, `web.xml`, `mobile.json`).

Needs: .NET 8 SDK, the AI service's virtual environment (`cd ai && python -m venv .venv &&
.venv/bin/pip install -e ".[dev]"`), `npm ci` in `web/`, and Flutter for the mobile part. A part
whose tool is missing is reported as skipped. Set `TEST_DATABASE_URL` to a test-only PostgreSQL
database (its name must contain "test") to include the PostgreSQL integration tests.

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
