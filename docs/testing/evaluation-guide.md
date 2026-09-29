# FoundU — Evaluation Guide (4 members)

This guide divides FoundU into four areas of about the same size, one per member. For each member
it lists the features they own, the code to be able to walk through, a demo script with expected
results, the tests to run, likely viva questions, and the known limitations they should raise
themselves.

The original split was: M1 lost items, listings and profile · M2 found items, found-item AI match
and staff code features · M3 AI model, lost-item matching and push · M4 admin/staff panel,
monitoring, users and support. Features that turned up in the build but were not in that split
have been assigned to balance the load. They are marked **(added)**.

Stories are numbered as in [user-stories.md](user-stories.md). Test results and known issues are
in [test-report.md](test-report.md).

---

## 0. Before the evaluation (whole team, about 15 minutes)

1. Start the stack (README → "Full-stack PowerShell demo startup"): PostgreSQL, the AI service,
   the API, the web app, and the Flutter emulator.
2. Reset the demo data: `python3 scripts/demo_seed.py` (with `FOUNDU_ADMIN_PASSWORD` set).
   **Write down the two codes it prints.** M2 needs them.
3. Demo accounts (password `Demo!Pass2026`):

| Account | Role | Seeded state |
|---|---|---|
| `amara@foundu.test` | Student (owner) | Open report, a conversation, asking about found keys |
| `dev@foundu.test` | Student (owner) | Wallet on its way to the desk, laptop waiting at the desk, an open ticket |
| `nadia@foundu.test` | Student (owner) | One item collected, one withdrawn, one resolved ticket |
| `kasun@foundu.test` | Student (finder) | Carrying a wallet, handed two in, has honor points |
| `priya@foundu.test` | Staff | The desk, the support queue, handover codes |
| `admin@foundu.com` | Admin | Password is your `DEV_ADMIN_PASSWORD` |

4. Run all four test suites once so the output is ready to show (section 6).
5. Everyone reads section 4 of the test report ("Known issues"). If an examiner finds one of
   those, say it is known and explain the fix. Don't be surprised by it.

## 1. Marking rubric (the same for every member, out of 100)

| Criterion | Weight | What earns full marks |
|---|---|---|
| Working demonstration | 30 | Every demo step in their section runs live on web **and** mobile where both exist, including the negative cases |
| Design and code understanding | 20 | Can open their files and explain the flow from UI → controller → service → database without notes. Can say why each layer exists |
| Testing | 20 | Can run their tests, explain what one test proves, and show one bug found and fixed in their area (test report §3) |
| Viva answers | 20 | Correct, specific answers to the questions in their section, including the known limitations |
| Integration and teamwork | 10 | Can explain where their area connects to the others (the "hand-off" row) and show a commit history |

Suggested grade bands: 85+ excellent · 70–84 good · 55–69 satisfactory · below 55 needs work.

## 2. Workload balance

| | M1 | M2 | M3 | M4 |
|---|---|---|---|---|
| User stories | 18 | 13 | 12 | 16 |
| API controllers | 3 | 5 | 4 | 7 |
| Main backend services | Auth, Profile, LostReport, PhotoStorage | FoundPost, FoundReport, Handover, Honor, HelpToFind | Whole AI service (Python), Matching/Verification/Parser/Intake/Workflow clients, Notification push | Claim, AdminUser, AdminOverview, Analytics, ReferenceAdmin, Support |
| Fixes from testing (report §3 and §5) | 6, 7, 8, 16, 17, 22, 26, 36, 43 | 2, 3, 4, 12, 20, 24, 25, 32, 37, 38 | 1, 5, 11, 14, 15, 18, 21, 27, 41 | 9, 10, 13, 19, 23, 33, 34, 35, 39, 40, 42 |
| Platforms | Web + mobile | Web + mobile | Python + web + mobile | Web (+ mobile claims and support) |

---

## Member 1 — Lost items, listings, profile and settings

**Scope:** Epic 1 (accounts and profile, US-01 to 08) and Epic 2 (lost items and listings, US-10 to 19).
**Added:** registration and login with JWT refresh, Google sign-in, landing page, theme and
accessibility, flagging a report, "I got it back".

**Code to know**

| Layer | Files |
|---|---|
| API | `Controllers/AuthController.cs`, `ProfileController.cs`, `LostReportsController.cs` (create, edit, mine, feed, photos, withdraw, resolve, flag, possible-matches) |
| Services | `Infrastructure/Identity/AuthService.cs`, `ProfileService.cs`, `JwtTokenService.cs`, `GoogleTokenVerifier.cs` · `Reporting/LostReportService.cs` · `Storage/LocalPhotoStorage.cs` · `Application/Common/PhotoRules.cs` |
| Validation | `Application/Auth/Validators/*`, `Application/LostReports/Validators/*` |
| Domain | `LostReport`, `LostItemPhoto`, `LostReportStatusHistory`, `RefreshToken`, `AppUser`, `LostReportStatus` |
| Web | `features/auth/*`, `features/account/*`, `features/reports/*` (report-lost wizard, my-reports, edit, withdraw, got-it-back), `features/feed/feed-page.tsx`, `feed-card.tsx`, `feed-detail-panel.tsx`, `pages/landing-page.tsx`, `lib/api/client.ts` (single shared refresh) |
| Mobile | `features/auth/*`, `features/account/*`, `features/reports/*`, `features/feed/presentation/feed_page.dart`, `core/api/auth_interceptor.dart`, `core/router/app_router.dart` |
| Tests | `AuthServiceTests.cs`, `ProfileServiceTests.cs`, `HonorPointsTests.cs` (the resolve path) · web `report-stage.test.ts`, `feed-api.test.ts`, `client.test.ts` · mobile `auth_repository_test`, `report_form_test`, `my_reports_layout_test`, `report_detail_layout_test`, `router_redirect_test` |

**Demo script**

1. Register a new student on web → lands on My reports. Register again with the same email → "already exists" (409).
2. Report a lost item: pick a category, then show that the item-type list is limited to that category. Type a description and show the parser filling in type and colour. Set a time window and attach 1 photo → the report appears as *Active*.
3. Try a lost window **in the future** → validation error. Try the end before the start → validation error.
4. Open `/feed` in a private window (signed out) → the report is public, but private details are not shown.
5. Edit the report, then withdraw it → it leaves the feed.
6. As `nadia` show a *Resolved* report with its status history. As `amara` open a report where someone pressed "I found this" and mark **I got it back** → the finder is thanked.
7. Account page: change the name (shown straight away), change the password → other sessions are signed out.
8. Mobile: sign in as `amara`, report an item with a photo, and see it in the Reports tab. Sign in as `priya` → "use the web dashboard" (the app is for students only).

**Hand-offs:** a new report feeds **M3**'s matching and Ask FoundU · finders act on it through **M2**'s "I found this" and handover · claims on it go through **M4**.

**Viva questions (answer pointers)**

- *Why a time window instead of a single "last seen" time?* Found items are compared against a range (`EstimatedLostFromAt`/`ToAt`); people rarely know the exact moment.
- *How do refresh tokens work, and what happens if one is stolen and reused?* They rotate on every use. Reusing a rotated token revokes all of that user's sessions (`AuthService.RefreshAsync`). Tokens are stored hashed.
- *Why is "suspended" checked after the password?* So an email alone doesn't reveal that the account exists and is suspended (fix 22).
- *Where does a 1200-character description get rejected, and why there?* In the FluentValidation validator, with a 1000 limit that matches the database column. Before the fix it reached the database and returned a 500 (fix 8).
- *How do you stop one student editing another's report?* The service compares `report.StudentId` with the id from the token and returns 403. The live probes check this.
- *Web tokens are in localStorage — is that safe?* This is a documented trade-off in `tokens.ts`. The API returns tokens in the response body, so an httpOnly cookie isn't available. The 15-minute access token limits the damage.

**Known limitations to raise:** the mobile app has no Google sign-in and no flag button · the web keeps tokens in localStorage (see the viva answer above).

---

## Member 2 — Found items, found-item AI match, staff code features

**Scope:** Epic 4 (finders, US-30 to 36) and Epic 5 (desk work with codes, US-40 to 45).
**Added:** finder ↔ owner message threads, the security hand-in tracker ("I gave it to security"),
the 48-hour handover with self-expiry, honor points and the Help-to-find page, and the "This is
mine" recognition.

**Code to know**

| Layer | Files |
|---|---|
| API | `FoundPostsController.cs`, `FoundReportsController.cs`, `HandoversController.cs`, `HelpToFindController.cs`, `ClaimsController.Collect`, `LostReportsController` (found-claims, messages, handover start/cancel) |
| Services | `Reporting/FoundPostService.cs` (post, recognise, withdraw, hand-in, confirm), `Reporting/FoundReportService.cs`, `Handovers/HandoverService.cs` (start, expire, receive, release), `Honor/HonorService.cs`, `Honor/HelpToFindService.cs`, `Domain/Common/HandoverCodes.cs` |
| Domain | `FoundReport`, `FoundItemPhoto`, `FoundReportMessage`, `LostReportFoundClaim`, `HonorAward`, `HandoverStatus`, `FoundReportStatus` |
| Web | `features/feed/post-found-page.tsx`, `found-board-page.tsx`, `found-feed.tsx`, `found-strip.tsx`, `hand-in-steps.tsx`, `handover-choice.tsx`, `message-thread.tsx`, `found-confirm-panel.tsx` · `features/items/*` (items list, log item, item detail with **AI match**, handover desk) · `features/help/*` |
| Mobile | `features/feed/presentation/found_board_page.dart`, `post_found_page.dart`, `found_post_*.dart`, `message_thread.dart` · `features/handover/*` · `features/help/*` |
| Tests | `HandoverTests.cs`, `HonorPointsTests.cs`, `IntakeFinderTests.cs` · web `ai-match-feedback.test.ts` · mobile `handover_test`, `help_to_find_test`, `post_found_entry_test` |

**Demo script**

1. As `kasun`, post a found item from the Found board (web) and from Fresh finds (mobile) → it appears on the board **and** the strip straight away.
2. As `amara`, open a found post and press **This is mine**, choosing her report → it succeeds even though the AI already paired them. Kasun gets a notification (fix 4).
3. As `kasun`, open one of Amara's lost reports → **I found this** → send a message → Amara sees it. Then choose **give it to security** → a 6-digit code is shown to both of them only.
4. As `priya` (staff), open **Handover code**, enter the "in flight" code from the seed → **Receive** into a storage location with a note → Dev is told where to collect.
5. Enter the "waiting at the desk" code → **Release** without ticking the ID check → refused. Tick it → released. Enter the same code again → "No handover has that code".
6. Staff **Log an item** at the desk (with private verification details) → open the item → **AI match** against a lost report → a suggestion is created.
7. Show Kasun's **Help to find** page: +10 for each desk hand-in, +25 when the item got home.
8. Edge case: start a handover, have the owner **withdraw** the report, then try the code at the desk → refused, and the report stays Withdrawn (fix 3).

**Hand-offs:** "AI match" calls **M3**'s matching agent · an item in storage is claimed through **M4**'s claim queue · the owner's report belongs to **M1**.

**Viva questions**

- *Why can't pressing "I found this" earn points?* Only outcomes confirmed by a second party count (`HonorAwardReason`). Before the fix, "I got it back" credited every button-presser (fix 2).
- *What stops one outcome paying twice?* A duplicate check in `HonorService`, backed by two filtered unique indexes. The check now matches the indexes exactly (fix 12).
- *Why is the handover code public to two people only, while the report's hand-in code is on the public feed?* The hand-in code only routes an item to a report. The handover code proves who collects, so the desk also checks ID.
- *What happens if the finder never turns up?* After 48 hours the handover expires, the code is cleared and the report goes back on the feed (`ExpireLapsedAsync`).
- *Why does receiving an item create a `FoundReport`?* So storage, audits and analytics treat it like any other item in custody.

**Known limitations to raise:** staff cannot mark an item Disposed · mobile can't show "my found posts" or "I gave it to security".

**Also be ready for:** *what if two desks scan the same code at once?* Claims, found items and handovers carry PostgreSQL's `xmin` row version, so the second save fails with a 409 instead of logging the item twice (test report fix 32). *Why does collection need an ID tick?* The code says which item, not who is at the counter. The desk looks the code up, sees the owner's name, then confirms the ID check (fix 37).

---

## Member 3 — AI service, lost-item matching, push notifications

**Scope:** Epic 6 (AI agents, US-50 to 57) and Epic 7 (notifications and push, US-60 to 63).
**Added:** the Ask FoundU chat assistant, the description parser that fills in the report form,
verification-question generation and answer grading, the coordinator workflow with a human
approval step, and the service-key authentication.

> **Important: be accurate about "model training".** The repository contains **no model
> training**: no dataset, no embeddings, no fine-tuning, no training script. The AI is:
> 1. **Deterministic, rule-based agents** (regex item-type patterns, a colour vocabulary, type
>    and colour scoring, fixed safe question templates) that always run.
> 2. An **optional LLM** (a local Ollama model) called with a JSON schema at temperature 0. Its
>    output is **validated against the source text** before use.
> 3. **LangGraph** routing requests to 5 agents, with durable workflow state in PostgreSQL and a
>    human-approval pause.
> 4. **Evaluation** with a golden test set (`ai/tests/data/description_parser_golden.json`) and
>    about 296 tests.
>
> Present it as **AI agent engineering and evaluation**. If the module requires training,
> actually do some before the viva, for example tune the matching weights against a labelled
> set of lost/found pairs and report precision and recall. Examiners will open `requirements.txt`
> and see there is no ML library, so don't claim training that isn't in the repo.

**Code to know**

| Layer | Files |
|---|---|
| AI service | `ai/app/main.py` (routes, idempotent workflow store), `service_auth.py`, `agents/graph.py`, `agents/matching.py`, `verification.py`, `description_parser.py`, `intake.py`, `coordinator.py`, `plans.py`, `checkpoint.py`, `llm/ollama.py`, `llm/fake.py`, `tools/registry.py` |
| API clients | `Infrastructure/Matching/MatchingAgentClient.cs`, `MatchSuggestionService.cs`, `Verification/VerificationAgentClient.cs`, `AiRequestRetry.cs`, `Reporting/DescriptionParserAgentClient.cs`, `Intake/*`, `Workflow/*` |
| Notifications | `Notifications/NotificationService.cs`, `NotificationPushSaveChangesInterceptor.cs` (sends only after commit), `NotificationPushDispatcher.cs`, `FirebasePushNotificationService.cs`, `DeviceRegistrationService.cs` · `NotificationsController`, `DeviceRegistrationsController`, `MatchSuggestionsController`, `IntakeController` |
| Web | `features/intake/*` (Ask FoundU page and bubble), `features/notifications/*` (bell, routing), `features/claims/suggestions-panel.tsx`, `agent-runs-panel.tsx` |
| Mobile | `features/intake/*`, `features/notifications/*` (push manager, inbox), `features/reports/presentation/possible_matches_page.dart` |
| Tests | all of `ai/tests/*` · `MatchingAgentClientTests`, `MatchingAgentIntegrationTests`, `VerificationAgentClientTests`, `ClaimVerificationIntegrationTests`, `DescriptionParserAgentClientTests`, `LostReportDescriptionParserIntegrationTests`, `PushNotificationTests` · web `intake-api.test.ts`, `notifications-api.test.ts`, `verification-question-generation-feedback.test.ts` · mobile `ask_foundu_test`, `notifications_test`, `notification_navigation_test`, `push_notification_manager_test` |

**Demo script**

1. `curl -X POST localhost:8000/agents/run` without the header → **401**. Show `service_auth.py`.
2. Show matching scores: a black wallet vs a black wallet → 1.0 · a black wallet vs a brown wallet → 0.5 · a black wallet vs a black **umbrella** → 0 / `no_match` (fix 5, `pytest -k colour_alone`).
3. Web: Ask FoundU as a student: "I lost a blue water bottle with black stickers near the library yesterday" → picks **blue**, searches, and offers a claim or a report draft. As a finder: "I found a wallet" → points to the owner.
4. Report form: type a description → type, colour and features fill in. **Stop the AI service** → the form still works (graceful fallback). Restart it.
5. Staff claim detail → **Generate questions** → show that none of the questions contain the private details. Show the agent-runs panel and the coordinator approval pause.
6. Notifications: the bell's unread count → click a row → the right page. Mark all read.
7. Push (if Firebase is configured): sign in on the emulator, trigger a match, and show the push. Otherwise show `PushNotificationTests` and explain the "send only after commit" interceptor.
8. `pytest -q` → 296 passed. Open the golden set and explain it.

**Hand-offs:** gets reports from **M1** and found items from **M2**, and gives claims to **M4** (questions, recommendation, coordinator).

**Viva questions**

- *What happens when Ollama is down?* Every agent has a deterministic path. The API retries 503 errors (`AiRequestRetry`), then falls back. Staff can always create a manual suggestion.
- *How do you stop the LLM leaking the private verification details?* Questions come from fixed templates chosen by key name. Drafted wording is checked against the private values and rejected if it leaks. Scores are never sent to the claimant.
- *Why is the push sent from a SaveChanges interceptor?* So a push goes out only when the notification row has committed. A rolled-back transaction never sends a push.
- *What's in a push payload?* Only the title, body, type, notification id and entity id. Never evidence, answers or contact details. The app fetches authorised details after a tap.
- *Why could matching produce false positives before, and how did you prove the fix?* Colour alone scored 0.5. There is a new unit test, and the black umbrella vs black wallet case now gives no_match.
- *Is this machine learning?* Be honest. See the note above.

**Known limitations to raise:** matching compares one pair on type and colour, with no date or place weighting · 7 of the 9 registry tools are stubs · the in-memory LangGraph checkpointer is never cleared.

**Also be ready for:** *how is an answer graded?* Filler words are ignored. At least 80% of the hidden detail's meaningful words is a match, 40% is partial. An answer much longer than the detail is capped at partial, so listing every plausible word does not work (fix 41).

---

## Member 4 — Admin and staff panel, monitoring, users, support, claims review

**Scope:** Epic 8 (staff and admin panel, US-70 to 77), Epic 9 (support tickets, US-80 to 82) and
Epic 3 (the student side of claims, US-20 to 23), so M4 owns the whole claim lifecycle.
**Added:** the claim queue and decisions, admin overturn with an audit trail, the coordinator
approval panel, moderation of flags, the places/categories/storage editor, analytics, role-based
route guards, and the global error handling.

**Code to know**

| Layer | Files |
|---|---|
| API | `AdminOverviewController`, `AdminAnalyticsController`, `AdminUsersController`, `AdminReferenceController`, `AdminSupportController`, `SupportController`, `ClaimsController` (queue, decide, overturn, questions, workflow approval) |
| Services | `Administration/AdminOverviewService.cs`, `AdminAnalyticsService.cs`, `AdminUserService.cs`, `ReferenceAdminService.cs` · `Support/SupportService.cs` · `Claims/ClaimService.cs` · `Workflow/ClaimWorkflowService.cs` |
| Security | `Infrastructure/DependencyInjection.cs` (Student, Staff and Admin policies; Staff includes Admin), `Api/Middleware/GlobalExceptionHandler.cs`, `Api/Filters/ValidationFilter.cs` |
| Web | `features/admin/*` (overview, users, suspend dialog, analytics, moderation, reference), `features/support/*`, `features/claims/*` (queue, detail, my-claims, workflow-approval-panel), `routes/*` (protected route, route-access, role-home) |
| Mobile | `features/claims/*`, `features/support/*` |
| Tests | `ReferenceAdminTests.cs`, `SupportTicketTests.cs`, `ClaimsEndpointIntegrationTests.cs`, `ClaimVerificationIntegrationTests.cs`, `PostgresPersistenceIntegrationTests.cs` · web `claims-api.test.ts`, `workflow-approval-panel.test.ts`, `protected-route.test.ts` · mobile `claim_*_test`, `my_claims_page_test`, `claims_pagination_test`, `support_and_account_test` |

**Demo script**

1. Sign in as `priya` (staff) → lands on Found items. Open **Overview** → queue figures. Staff see no links to admin pages (fix 19). Try `/admin` → Forbidden.
2. Full claim: a student claims an item → staff **Generate questions** → the student answers → staff see the answers and the AI recommendation → **Reject** with a reason → the report is back on the feed.
3. Sign in as admin → **Overturn** the rejection with a reason → the claim is approved, both decisions are kept, and the report is off the feed (fix 10). The student sees the collection code, staff don't.
4. **Users**: search `nadia` → suspend with a reason → she cannot log in (403, but a wrong password still gives 401), and a token she already had stops working on the next request. Reinstate. Try to suspend yourself → refused. **Make a registered student Staff** with the role control → they are signed out and come back with desk access. Your own role can't be changed.
5. **Places & categories**: add a place, rename it, retire it. Try to delete a category that has reports → 409 "Retire it instead".
6. **Analytics**: returns, storage, the 30-day chart and "Show as a table". **Moderation**: clear a flag.
7. **Support**: as `dev`, open a ticket. As `priya`, assign it, reply, then resolve → Dev is notified. Dev replies → the ticket reopens.
8. Show the server-side guards: `curl` `/api/admin/users` with a student token → 403.

**Hand-offs:** decisions use **M3**'s verification and coordinator agents · collection happens at **M2**'s desk · the reports being claimed are **M1**'s.

**Viva questions**

- *Why is role checking done on the server and not only in React?* A client can be bypassed. Every controller has a policy. The web guards only improve the user experience.
- *What happens if two staff approve two different claims for the same item at the same time?* A filtered unique index allows only one approved claim per found item. The loser now gets a 409 instead of a 500 (fix 13).
- *Why can't an admin delete a category in use?* Records would lose their meaning. Retiring hides it from the pickers and keeps old records readable. Deletes are soft deletes.
- *How is the overturn audited?* The rejection and the override `ApprovalDecision` rows both stay, with `IsOverride` and `OverriddenByUserId`.
- *Why did a ticket with status "9" vanish from the queue?* `Enum.TryParse` accepts numbers. Validation now requires `Enum.IsDefined` (fix 9).

**Known limitations to raise:** analytics days are in UTC (the chart says so).

**Also be ready for:** *one lost item, two found items?* Approval refuses a second item for the same report, and the owner's other claims close (fix 33). *The owner withdraws while staff are reviewing?* The claim is cancelled and approval is refused (fix 34). *How are staff accounts made?* They register, then an admin changes their role (fix 39).

---

## 6. Test commands for the evaluation

```bash
cd api && dotnet test FoundU.sln                     # 166 tests (8 need TEST_DATABASE_URL)
cd ai && ruff check . && pytest -q                   # 296 tests
cd web && npm run lint && npm test && npm run build  # 45 tests
cd mobile && flutter analyze && flutter test         # 78 tests
FOUNDU_SMOKE_ADMIN_PASSWORD=… ai/.venv/bin/python scripts/smoke_full_stack.py  # end-to-end
```

Filter to one member's area, for example `dotnet test --filter "FullyQualifiedName~Handover"`,
`pytest -k matching`, `npx vitest run notifications`, `flutter test test/handover_test.dart`.
