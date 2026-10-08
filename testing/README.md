# FoundU testing (SE3090 Assignment 2)

Each suite below is a tool or framework run against the real FoundU system. The evidence each
one produces lands in [`reports/`](reports/). The test plan, test cases, defect report and
execution summary are in the Software Testing Report.

| Area | Tool | Where | Evidence |
|---|---|---|---|
| API unit, service, controller, auth | xUnit, WebApplicationFactory, coverlet | `api/tests/FoundU.Tests` | `reports/api/api-tests.trx`, `reports/api/coverage/Summary.txt` |
| Database (real PostgreSQL 16) | xUnit + EF Core + Npgsql | `PostgresPersistenceIntegrationTests.cs` | included in the TRX (9 tests, 0 skipped) |
| React web | Vitest, React Testing Library | `web/tests`, `web/src/**/*.test.ts` | console output |
| Flutter mobile | flutter_test (unit, widget, API integration) | `mobile/test`, `mobile/test/integration` | `reports/mobile/lcov.info`, `reports/mobile/api-integration.txt` |
| End-to-end and cross-component | Playwright | `testing/e2e/tests` | `reports/e2e/html/index.html`, `reports/e2e/junit.xml` |
| Accessibility (WCAG 2.1 AA) | axe-core via Playwright, Lighthouse | `testing/e2e/tests/accessibility.spec.ts` | `reports/accessibility/` (before and after fixes), `reports/lighthouse/` |
| Performance: load, stress, spike | k6 | `testing/performance` | `reports/performance/*-summary.json` |
| Security: API | Newman (Postman collection) | `testing/security/build_collection.py` | `reports/security/{before,after}-fix/newman-report.html` |
| Security: scans | OWASP ZAP (baseline + authenticated API scan) | Docker `zaproxy/zap-stable` | `reports/security/{before,after}-fix/zap-*.html` |
| Security: web headers and CSP | Playwright against the build with the real headers | `testing/e2e/tests/csp.spec.ts` | E2E report (CSP-01 to 04) |
| Agentic AI evaluation | pytest (deterministic and live Groq) | `ai/tests/evaluation` | `reports/ai-eval/*-results.json` |

## Running everything

```bash
# 0. Once: PostgreSQL in Docker, AI venv, web and e2e packages (see the root README)
testing/start-stack.sh                  # PostgreSQL, AI service (fake model), API, web - waits until healthy

# Unit and integration suites (what CI runs), with coverage
TEST_DATABASE_URL="Host=localhost;Port=5434;Database=foundu_test;Username=foundu;Password=foundu" \
  dotnet test api/FoundU.sln --collect:"XPlat Code Coverage" --logger "trx;LogFileName=api-tests.trx" --results-directory testing/reports/api
(cd ai && .venv/bin/pytest --cov=app)
(cd web && npm test)
(cd mobile && flutter test --coverage)

# Mobile <-> API integration (needs the stack)
(cd mobile && flutter test test/integration --dart-define=FOUNDU_API_URL=http://localhost:5292)

# End-to-end, accessibility (and CSP, see below)
(cd testing/e2e && npm install && npx playwright test)

# Performance
(cd testing/performance && k6 run load.js && k6 run stress.js && k6 run login-spike.js)

# Security: API collection, then ZAP
testing/security/run-newman.sh after-fix
docker run --rm -v "$PWD/testing/reports/security":/zap/wrk:rw zaproxy/zap-stable \
  zap-baseline.py -t https://foundu-web.onrender.com -r zap-baseline-web.html -I

# AI evaluation: deterministic, then against the hosted model
(cd ai && .venv/bin/pytest tests/evaluation)
(cd ai && LLM_PROVIDER=groq LLM_MODEL=openai/gpt-oss-20b LLM_API_KEY=... .venv/bin/pytest tests/evaluation)
```

### Checking the CSP before it ships

`render.yaml` sets the web app's security headers on Render. To check them locally, serve a
production build with exactly those headers, then run the CSP specs. They fail on any CSP
violation:

```bash
(cd web && VITE_API_BASE_URL=http://localhost:5292 npx vite build --outDir /tmp/foundu-dist)
node testing/security/csp/serve-with-render-headers.mjs /tmp/foundu-dist 4173 &
(cd testing/e2e && CSP_URL=http://127.0.0.1:4173 npx playwright test csp.spec.ts)
```

## Test data

The suites create their own throwaway accounts (`e2e-*`, `sec-*`, `mobile-*` at `@foundu.test`)
and use the demo accounts from `scripts/demo_seed.py` (`priya@foundu.test` for staff,
`amara@foundu.test` for a student). Run them against a local or test database only. The
performance and ZAP active scans in particular are never pointed at production.
