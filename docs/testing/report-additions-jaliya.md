# Additions for the Software Testing Report (Jaliya's part)

Paste each section into the report where its heading says. Screenshot file names refer to
`testing/reports/manual-evidence/`.

---

## Paste into section 3, "Responsibilities": replace the M4 row, or add a row

| Member | Features owned | Testing responsibility | Tools they demonstrate |
| --- | --- | --- | --- |
| Jaliya Hettiarachchi | Admin user management, analytics, support tickets and the support AI assistant, UI/UX, deployment, email (Resend), Google sign-in | Admin user-management service tests (ADM), API security collection (Newman, SEC), exploratory API testing (Swagger), accessibility and page quality (axe, Lighthouse), deployed-site scan (ZAP baseline), support-agent evaluation cases | xUnit, Postman/Newman, Swagger UI, Playwright + axe, Lighthouse, OWASP ZAP, pytest |

---

## New section: Individual testing contribution: Jaliya Hettiarachchi

### What I tested and why these tools

| Area | Tool | Why this tool |
| --- | --- | --- |
| Admin user management (service logic) | xUnit + EF Core InMemory | Tests the C# rules directly, fast and isolated, with a fresh database per test |
| API security of my endpoints | Postman (runner) and Newman | Sends real HTTP requests with real JWTs and asserts on every response; the same collection runs in the app and from the command line |
| Exploring and checking endpoints by hand | Swagger UI | Shows every endpoint and its policy; used to understand the API and confirm results before automating them. Supplementary, not a replacement for the automated tools |
| UI accessibility (my UI/UX work) | axe-core via Playwright, Lighthouse | WCAG 2.1 AA rules engine, plus page-quality scores on the deployed site |
| Deployed site (my deployment) | OWASP ZAP baseline | Passive scan, so it's safe to run against production |
| Support AI assistant | pytest | Deterministic agent cases and the evaluation categories |

### ADM: admin user-management test cases (xUnit), `api/tests/FoundU.Tests/AdminUserServiceTests.cs`

Preconditions for all: a fresh in-memory database holding one Admin and one Student.

| ID | Type | Steps / input | Expected | Actual | Status |
| --- | --- | --- | --- | --- | --- |
| ADM-01 | Normal | Admin suspends the student with "Posting fake found items" | Suspended; reason, admin id and time saved | As expected | Pass |
| ADM-02 | Invalid | Admin suspends their own account | `ValidationAppException` (400) | As expected | Pass |
| ADM-03 | Invalid / security | Admin suspends another Admin | `ForbiddenAppException` (403) | As expected | Pass |
| ADM-04 | Failure | Suspend the same student twice | Second call: `ConflictAppException` (409) | As expected | Pass |
| ADM-05 | Failure | Suspend an id that does not exist | `NotFoundAppException` (404) | As expected | Pass |
| ADM-06 | Security | Student holds a refresh token; admin suspends them | The token is revoked (`RevokedAt` set) | As expected | Pass |
| ADM-07 | Normal | Reinstate a suspended student | Not suspended; reason and admin cleared | As expected | Pass |
| ADM-08 | Failure | Reinstate a student who is not suspended | 409 | As expected | Pass |
| ADM-09 | Normal | Change the student's role to Staff | Role is Staff | As expected | Pass |
| ADM-10 | Invalid | Admin changes their own role | 400 | As expected | Pass |
| ADM-11 | Invalid | Role "Superuser", "9", "" (3 runs) | 400 for each | As expected | Pass |
| ADM-12 | Failure | Give the student the role "Student" | 409 | As expected | Pass |
| ADM-13 | Boundary | Suspension reason of 0, 9, 10, 500, 501 characters (5 runs) | Invalid, invalid, valid, valid, invalid | As expected | Pass |

**Result: 19 runs, 19 passed, 0 failed** (`dotnet test --filter "FullyQualifiedName~AdminUserService"`).
Evidence: terminal screenshot (to add) and commit (to add).

### EXP: exploratory API checks in Swagger (local API)

| ID | Steps / input | Expected | Actual | Status | Evidence |
| --- | --- | --- | --- | --- | --- |
| EXP-01 | `POST /api/auth/login` as admin@foundu.com | 200 with access and refresh tokens | 200, role Admin | Pass | 01, 02 |
| EXP-02 | `GET /api/admin/users` with the admin token | 200, user list | 200 | Pass | 03 |
| EXP-03 | `POST /api/admin/users/{nadia}/suspend` with reason "too short" (9 chars) | 400, field error on Reason | 400 "Give a little more detail than that." | Pass | 04 |
| EXP-04 | The same call with a token older than 15 minutes | 401, `invalid_token` | 401 "The token expired at …" | Pass | 05 |
| EXP-05 | Sign in as amara (student), `GET /api/admin/users` | 403 | 403, with security headers | Pass | 06, 07 |
| EXP-06 | Suspend Nadia with "Posting fake found items" | 200, isSuspended true | (to run) | | |
| EXP-07 | The same request again | 409 "already suspended" | (to run) | | |
| EXP-08 | `POST /api/admin/users/{nadia}/reinstate` | 200, isSuspended false | (to run) | | |

### SEC: API security collection (Postman runner)

| Run | Requests | Assertions | Passed | Failed | Evidence |
| --- | --- | --- | --- | --- | --- |
| 4 Oct 2026, environment "local" | 36 | 49 | 49 | 0 | 08, 09 |

**Mutation check on SEC-12** (staff → `GET /api/admin/users` must be 403): I changed the assertion to
expect 200 and re-sent it, and the test **failed** with an AssertionError against the real 403.
Restored to 403, it **passed**. This shows the assertion really checks the authorisation rule.
Evidence: 10, 11.

### Defects I found, fixed and retested

| Defect | Found by | Cause | Fix | Retest |
| --- | --- | --- | --- | --- |
| 62/63: missing hardening headers; no rate limit on password-reset emails | Newman SEC-27, SEC-28 | Headers were never added to API responses; the email endpoints had no limiter | Security headers on every response; fixed-window limit of 5 per minute per client, 429 after that (`408b827`) | Newman 49/49; SEC-28 receives a 429 inside a burst of 15 |
| 64: no CSP, anti-clickjacking, HSTS or Permissions-Policy on the deployed web app | ZAP baseline | The Render static site served no security headers | Headers set in `render.yaml` (`107d80c`) | ZAP re-run on Render: those warnings gone (`7f079a0`) |
| 55–58: contrast, unnamed controls, list markup | axe-core | UI built without those checks | Fixed in `8afce39` | axe on 11 pages: 0 violations |
| 67: 1.4 MB first load | Lighthouse | Every page in one bundle | One chunk per page (`fbf1f23`) | Deployed performance 45 → 73 (landing) |
| 61: support escalated a password reset to the desk | AI evaluation AIE-FR-02 | The guide entry predated self-service reset | Guide points to Forgot password? (`f4267c0`) | 68/68, deterministic and live |

### Git evidence

Repository: https://github.com/jaliyavox/FoundU

| Commit | What |
| --- | --- |
| `3afa703` | Assignment 2 suites, scripts and evidence (Playwright, k6, Newman, ZAP) |
| `5a4f3ff` | Agentic AI evaluation suite, nine categories |
| `408b827`, `107d80c`, `8afce39`, `fbf1f23`, `f4267c0` | Fixes for the defects above |
| `2000e79`, `f0ec2ac`, `7f079a0` | After-fix retests (ZAP, AI live run, production retest) |
| (to add) | Admin user-management tests (ADM-01 to ADM-13) |

### AI usage (CLEAR)

Claude Code drafted test scripts (including the first version of `AdminUserServiceTests.cs`) and
helped run tools and draft this section. I reviewed every test against the code it checks, ran
each tool myself on the local system and the deployed one, modified tests to confirm they fail
when they should (SEC-12, ADM-02), and can explain, re-run and change any of them.

---

## Add as an appendix: tool evidence index

| File (`testing/reports/manual-evidence/`) | Shows |
| --- | --- |
| 01-swagger-admin-login-200.png | Admin login: 200 with tokens |
| 02-swagger-authorize-bearer.png | JWT applied in Swagger |
| 03-swagger-admin-users-200.png | Admin user list: 200 |
| 04-swagger-suspend-reason-9-chars-400-boundary.png | Boundary: a 9-character reason gets 400 |
| 05-swagger-expired-token-401.png | Expired token: 401 |
| 06-swagger-student-login-200.png | Student login |
| 07-swagger-student-admin-users-403.png | A student gets 403 on the admin endpoint |
| 08-postman-collection-36-requests.png | Collection imported |
| 09-postman-runner-49-passed.png | Runner: 49/49 passed |
| 10-postman-sec12-modified-to-200.png | Mutation: assertion changed |
| 11-postman-sec12-restored-403.png | Restored: 403 |

Generated reports: `testing/reports/` (e2e HTML, Newman HTML before and after, ZAP before and after,
k6 JSON, Lighthouse, axe, AI evaluation JSON, API coverage).
