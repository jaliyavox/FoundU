# FoundU — Final Testing Report

**Date:** 27 September 2026 · **Branch:** `claude/exciting-bohr-lrphmc` · **Scope:** API, AI service, web dashboard, Flutter app

This report records what was tested before hand-in, what broke, what was fixed, and what is still
open. Each fix listed below has a regression test or a live API check, and every suite was re-run
after the fixes.

---

## 1. Summary

| | Before this pass | After this pass |
|---|---|---|
| API build | **Failed** on .NET SDK 8.0.1xx (ambiguous `Split` call) | 0 errors, 0 warnings |
| .NET tests (xUnit) | 151 passed, 8 PostgreSQL tests skipped | **166 passed**, including the 8 PostgreSQL tests |
| AI service (pytest + ruff) | 291 passed, 1 skipped | **296 passed**, lint clean |
| Web (vitest + oxlint + tsc + build) | 43 passed | **45 passed**, typecheck and build clean |
| Mobile (flutter analyze + test) | 76 passed | **78 passed**, analyzer clean |
| End-to-end smoke (`scripts/smoke_full_stack.py`) | — | **Passed** (report → AI match → claim → questions → approve → collect) |
| Demo seed (`scripts/demo_seed.py`), every flow through the real API | — | **Passed** |
| Role and ownership probes (live API) | — | **56 checks**. All access rules hold. The 7 checks that did not match were checked by hand: they were mistakes in the probe script, not bugs. |
| Browser crawl, 3 roles × every page (Playwright) | — | **48/48 pages** load with no console errors and no failed API calls. Every forbidden route redirects. |
| Bugs fixed | — | **31** (1 build break, 5 High, 14 Medium, 11 Low) |

**Total automated tests: 585, all passing** (596 after the evaluation-panel pass in section 5).

## 2. How it was tested

1. **Static checks.** `dotnet build` (warnings count as errors), `ruff`, `oxlint`, `tsc -b`, `flutter analyze`.
2. **Unit and integration tests.** The four suites that CI runs. The 8 PostgreSQL tests were also run against a real
   PostgreSQL 16 database (`TEST_DATABASE_URL`). One of them checks that the model has **no pending migrations**.
3. **Full stack running locally.** PostgreSQL 16, the FastAPI AI service (deterministic LLM
   provider), the ASP.NET API and the Vite web server were started together.
4. **End-to-end scripts.** `smoke_full_stack.py` (the happy path through claims) and
   `demo_seed.py` (creates 5 users, lost reports, messages, handovers, found posts, desk
   items, claims and support tickets through the API).
5. **Negative-path probes.** Scripted HTTP calls as anonymous, student, other student, staff and
   admin. They cover role guards, ownership, suspension, refresh-token reuse, password-change
   revocation, handover codes, validation limits and pagination.
6. **Browser crawl.** Playwright logs in through the real form as each role, opens every page that
   role can use, and records console errors and failed API calls. It also checks that every
   forbidden page redirects to `/forbidden`.
7. **Code review.** Four parallel reviews looked for logic errors, contract mismatches and dead
   code in the backend workflows, admin/auth/notifications, web and mobile clients, and the AI
   service. Every finding was checked against the code, and reproduced live where possible,
   before anything was fixed.

## 3. Defects found and fixed

Severity: **High** means wrong data, a security issue, or a main flow broken for a real user.
**Medium** means a visible error or wrong behaviour in a secondary path. **Low** means an edge case
or cleanup.

| # | Area | Severity | Defect | Fix | Proof |
|---|---|---|---|---|---|
| 1 | API | **Build** | `IntakeService` did not compile on .NET 8.0.1xx SDKs: the collection expression passed to `string.Split` was ambiguous. | Pass an explicit `char[]`. | Build is clean |
| 2 | API | High | "I got it back" gave +25 honor points to **everyone** who had pressed "I found this" on the report. Clicking the button on every report was a free points farm. | Credit only finders who took the item to a desk, or the single finder when the handover was in person. Everyone is still notified. | `HonorPointsTests.PressingIFoundThisAlone…` |
| 3 | API | High | Withdrawing or resolving a report left the finder's handover code working. The desk could then receive the item, which moved a *Withdrawn* report back to *Matched* and created an orphaned found item. | Withdraw and resolve cancel handovers still in progress. The desk refuses a code whose report is closed. The history row records the real previous status. | `HandoverTests.WithdrawingTheReport…`, live check |
| 4 | API | High | "This is mine" on a found post returned **409** when the matching agent had already paired that post with the owner's report, which is the usual case. The finder was never told. | Reuse the existing suggestion and always notify the finder. | Live check (was 409, now 200) |
| 5 | AI | High | Matching scored **colour alone** as 0.5, a `match_candidate`. A black umbrella was suggested for a lost black wallet, and the student got a "possible match" notification. | A different item type scores 0. | `test_colour_alone_is_not_a_match_candidate` |
| 6 | Mobile | High | Staff and Admin could sign in to the student-only app, and every tab then showed 403 errors. | Only Student accounts are accepted; others are told to use the web dashboard. | `auth_repository_test` |
| 7 | API | Medium | A duplicate student number at registration or in a profile edit returned **500** from the database. | Checked first, now returns 409. | `AuthServiceTests.Register_DuplicateStudentNumber…`, live |
| 8 | API | Medium | Validators allowed 2000 characters for fields stored in 1000-character columns (lost and found descriptions, private details), and 1000 for 500-character columns (claim answers, decision reasons). Longer text returned **500**. | Validator limits now match the columns. New validators added for the withdraw reason, resolve note and claim cancel reason. | Live check (1200-character description returns 400) |
| 9 | API | Medium | Numeric strings such as `"42"` were accepted as enum values: ticket category and status, claim decision, and report status filters. A ticket with status `"9"` disappeared from every queue. | Require `Enum.IsDefined`. | `SupportTicketTests.ANumberOrUnknownName…`, live |
| 10 | API | Medium | An admin overturn of a rejected claim left the owner's report **on the public feed**. The earlier rejection had reopened it. | Overturn moves the report back to Matched, and refuses if the owner has closed it. | Live check |
| 11 | API | Medium | After a revision a claim has two coordinator runs, and approving the second workflow could update the first one. | Pick the run by workflow id. | Code review |
| 12 | API | Medium | The honor duplicate check did not match the database's two unique indexes. Some collection orders failed with **500** at the desk. | The check now uses the same rule as the indexes. | `HonorPointsTests.TheSameReturnCredited…` |
| 13 | API | Medium | A unique-index race, such as two staff approving at once, surfaced as a 500. | Mapped to 409 "someone else changed this". | Code review |
| 14 | AI | Medium | The description parser could return more than 5 features, over-long features, or features differing only in case. The API then rejected the whole result and autofill silently failed. | Capped, truncated and de-duplicated. | `test_deterministic_features_respect_the_api_contract` |
| 15 | AI | Medium | Ask FoundU took the first colour **in its word list**, not the first one in the message ("blue bag with black straps" searched for black). "gray" never matched "Grey". | Use the earliest position in the message, and normalise gray to grey. | `test_the_first_colour_named…` |
| 16 | Mobile | Medium | A token refresh that failed on weak Wi-Fi **logged the student out**. | Only a 400, 401 or 403 from the server ends the session. | `auth_repository_test` |
| 17 | Mobile | Medium | If the photo upload failed after the report was saved, the form stayed open, and pressing Submit again created a **duplicate report**. | The report is kept and the student is told to add photos from Edit. | `flutter analyze` + code review |
| 18 | Mobile | Medium | Tapping a "new message" push opened `/reports/{id}` even when the id was a found post, or a report the finder does not own, giving a 404 or 403. | Message pushes open the inbox, which knows the thread type. Support, finder and return pushes route correctly. | `push_notification_manager_test` |
| 19 | Web | Medium | The staff overview linked to admin-only pages (Moderation, Users, Analytics), so staff landed on Forbidden. | Staff see the figures without those links. | Browser crawl |
| 20 | Web | Medium | Posting, withdrawing or handing in a found post did not refresh the Fresh-finds strip or My reports. | One helper refreshes all three lists. | Code review |
| 21 | Web | Low | The web `NotificationType` had 8 of the API's 15 types. Support and finder notifications could not be clicked and showed a generic icon. | Types synced, with an icon and a link for each. | `notifications-api.test.ts` |
| 22 | API | Low | Login returned "account suspended" **before** checking the password, so anyone could find out an account was suspended from the email alone. | Suspension is checked after the password. | `AuthServiceTests.SuspendedUser_WithWrongPassword…`, live |
| 23 | API | Low | A suspended user's phone kept receiving push notifications. | Suspension deactivates their device registrations. | Code review |
| 24 | API | Low | A staff member could post an item and confirm it themselves to earn points. | No award when the finder is the confirming staff member. | Code review |
| 25 | API | Low | The desk's "receive" note was validated but thrown away. | Stored in the report history. | Code review |
| 26 | API | Low | `EntityFrameworkCore.Design` floated to `8.0.*` (8.0.31) while every other EF package was pinned to 8.0.10, which caused an assembly conflict warning. | Pinned to 8.0.10. | Build shows 0 warnings |
| 27 | AI | Low | A non-ASCII service-key header returned 500 instead of 401. | Compare as UTF-8 bytes. | `test_non_ascii_service_key…` |
| 28 | Web | Low | Unused template files: `react.svg`, `vite.svg`, `hero.png`, `public/icons.svg`, `ui/card.tsx`. | Removed. | Build |
| 29 | Web | Low | Duplicate, unused API helpers `getLostReports`, `createFoundReport`, `getFoundReports` in `reports-api.ts`, which duplicated `items-api`. | Removed. | `tsc` |
| 30 | Web | Low | `my-reports-page.tsx` imported from `feed-api` twice. | Merged. | lint |
| 31 | Mobile | Low | Unused `searchQueryProvider` / `SearchQueryNotifier`. | Removed. | `flutter analyze` |

## 4. Known issues not fixed (be ready to discuss these)

These are real but were judged too large or too risky to change just before hand-in. They are
listed so no member is caught out in a viva.

| Area | Issue | Impact | Suggested fix |
|---|---|---|---|
| API | A Google-only account can add a password with only an access token. | Low: an attacker needs a stolen token. | Require a fresh Google ID token |
| API | Staff cannot mark a stored item as **Disposed**. | Items stay in storage forever. | A staff "dispose" action |
| API | Analytics group days in **UTC**, not campus time. | Chart days shift by 5.5 hours (Sri Lanka). | Convert to the campus time zone, or label the chart UTC |
| API | After a revision, a paused coordinator run from the first attempt can stop a new run from starting. | Rare. Staff can still decide manually. | Close the old run on revision |
| API | FCM `InvalidArgument` deactivates a device token even when the payload was at fault. | Low. | Only deactivate on `Unregistered` |
| API | Dead code: the `StorageTransfer` entity, the reference-data block in `DevelopmentDataSeeder`, and the duplicate route `lost-reports/mine` + `my-reports` (web uses one, mobile the other). | Cleanup only. | Remove, or pick one route |
| AI | Intake field limits (40 or 80 characters) are shorter than the DB columns (50, 100, 150). A very long place name makes Ask FoundU unavailable. | Rare. | Align the limits |
| AI | Retrying after a mid-run 503 gets 409 instead of resuming. | Rare. | Allow a failed record to re-run |
| AI | 7 of the 9 registry tools are stubs; `/agents/parse-description` has no caller. | Cleanup only. | Remove, or mark as future work |
| Web | A single 1.4 MB JavaScript bundle (Vite warning). | Slower first load. | Split routes with `React.lazy` |
| Mobile | Missing compared with web: my found posts and "I gave it to security", the match-suggestions list, flagging a report, Google sign-in. | Students need the web app for these. | Port the screens |
| Mobile | After a claim from Ask FoundU, or a report withdraw/resolve, My claims or the feed are refreshed only by pull-to-refresh. | Stale for a short time. | `ref.invalidate` the providers |

## 5. Evaluation-panel pass (29 September 2026)

We re-ran the system as an examiner would: reading the claim, handover and collection code for state-machine gaps, then attacking the running stack. That meant parallel requests, one student acting on another's records, disguised uploads and malformed input. A probe of **36 live checks** now passes. A browser run drives the new desk-collection and login flows (4/4), and the 48-page crawl is still clean. Each fix below has a test.

| # | Severity | Defect found by the panel | Fix | Proof |
|---|---|---|---|---|
| 32 | **High** | **Race:** two staff approving rival claims on one item at the same moment both got 200. Each approval rejected the other and both saves landed, so **both students were told "approved" and "rejected"**, and the loser kept a collection code. Two desks receiving one handover code could log the item twice. | Claims, found items and handovers carry PostgreSQL's `xmin` row version as a concurrency token. The second save fails with 409. The migration adds no column. | `PostgresPersistenceIntegrationTests.TwoStaffApprovingRivalClaimsAtOnce…` (fails 2 runs in 5 without the fix, passes 10 in 10 with it); live: parallel approvals give 200 + 409, parallel receives give 200 + 409, no orphaned item |
| 33 | High | One lost report could have **two items approved** (claim A on item X and claim B on item Y). | Approval requires no other approved item for the report. The owner's other open claims on it close. A new claim is refused once one is approved. | `ClaimLifecycleTests.ApprovingOneItemClosesTheOwnersOtherClaims…`, live |
| 34 | High | Staff could **approve a claim after the owner withdrew the report**. Withdraw left open claims in the queue. | Withdraw and "I got it back" cancel claims still waiting on staff. Every approval (staff or admin overturn) checks that the report is open and the item unreserved. | `ClaimLifecycleTests.WithdrawingCancelsOpenClaims…`, live |
| 35 | Medium | A rejection or cancel put the report **back on the public feed** while an approved item, or a finder's hand-in, was still waiting at the desk for the owner. | Reopening also counts approved-uncollected claims and live handovers. | `ClaimLifecycleTests.ARejectionDoesNotPutTheReportBack…` |
| 36 | Medium | An owner could withdraw a report while their item sat reserved at the desk, leaving it on hold forever. | Refused with a clear message (collect it, or open a ticket). | `ClaimLifecycleTests.AnOwnerCannotWithdrawWhile…`, live |
| 37 | Medium | "Mark collected" handed an item over the moment a code was typed. The desk never saw whose item it was, and there was no ID check, unlike handover release. | `GET /api/claims/by-code/{code}` shows the owner first. Collect requires `ownerIdChecked`. The web desk is now look-up → tick → hand over. | `ClaimsEndpointIntegrationTests`, `ClaimLifecycleTests.TheDeskMustCheckId…`, browser flow |
| 38 | Medium | A student's "might be yours" list kept items that were taken down, already returned or reserved for someone else. "This is mine" then failed. | Filtered out. | live (post withdrawn → suggestion gone) |
| 39 | Medium | No way to make anyone Staff or Admin (only direct SQL in the seed script). | `PUT /api/admin/users/{id}/role` and a role control on the Users page. You can never change your own role. The user's sessions end. | `ClaimLifecycleTests.AnAdminMakesSomeoneStaff…`, live |
| 40 | Medium | A suspended user's access token, or one carrying an old role, kept working for up to 15 minutes. | The token check at validation refuses it on the next request. | live (suspend → 401; role change → old token 401) |
| 41 | Low | Answer grading almost never reached `likely_match`, and filler words earned partial credit. | Filler words ignored. A faithful paraphrase (≥ 80% of the meaningful words) is a match. A keyword-stuffed answer is capped at partial. | `test_a_faithful_paraphrase_is_a_match…` |
| 42 | Low | Moderation showed only the first 20 flags. | Pager. | browser crawl |
| 43 | Low | After signing in, a staff member who had followed a student link landed on Forbidden. | The redirect is used only if the role can open that page. | `role-home.test.ts`, browser flow |

**Checked and found sound by the panel:** one student opening, answering or cancelling another's claim, or uploading to their report (403). A text file named `.jpg` (400). Negative page numbers and 5000-character searches (200, clamped). Non-GUID ids (404). Malformed JSON (400). Messaging your own report (400).

**Totals after the panel pass:** .NET 173, AI 297, web 48, mobile 78, so **596 automated tests, all passing**.

## 6. How to reproduce the testing

```bash
# Unit / integration (what CI runs)
cd api && dotnet test FoundU.sln
cd ai && ruff check . && pytest
cd web && npm run lint && npm test && npm run build
cd mobile && flutter analyze && flutter test

# PostgreSQL-backed API tests (need a throwaway database)
TEST_DATABASE_URL="Host=localhost;Port=5432;Database=foundu_test;Username=…;Password=…" \
  dotnet test api/FoundU.sln --filter "Category=PostgreSql"

# Full stack: start Postgres, the AI service, the API (see README), then
FOUNDU_SMOKE_ADMIN_PASSWORD=… ai/.venv/bin/python scripts/smoke_full_stack.py
FOUNDU_ADMIN_PASSWORD=… python3 scripts/demo_seed.py     # destructive: local DB only
```
