# FoundU found-item workflow verification

Implementation date: 4 October 2026. Changes are local; no commit, push or deployment was performed.

## First-round evidence (preserved historical results)

The following sections describe the earlier local fixes and their test run. The second-round results and migration below supersede their final-state claims.

## 1. Root causes and workflow trace

The backend already used one canonical `FoundReport` for a student post and its security intake. `FoundPostService.ConfirmAsync` changes Posted to Unclaimed, records the staff member/storage/verification evidence, and consumes the hand-in code. Creating a duplicate found record was not the cause in that path.

The feed response already calculated `IsMine` from the authenticated caller and returned the finder code only to that finder. Staff-only routes protect found-report evidence and code lookup/intake. The observed cross-account finder controls were consistent with mobile state reuse: both feed notifiers lacked an authentication-session dependency. Several detail providers/repositories also outlived the identity that loaded them. A slow response or refresh could complete across logout. Router branch stacks and selected post snapshots were retained.

Intake did not notify existing suggestion owners. Mobile report/match/detail providers did not regularly refetch remote staff changes. Student-safe item summaries lacked a custody desk name. Automatic matches also stored a pre-custody note, which could remain misleading after intake.

The tracker expected a `Claimed` lost-report status. The domain has Active, Matched, Resolved and Withdrawn; Matched means a claim was submitted. Suggestions leave a report Active. The mobile tracker therefore displayed Reported for a possible match and could never reach its Claimed step using the real status contract.

The staff picker did not consult existing item suggestions, so it offered duplicate pairs. The API rejected both manual and AI duplicates, but the web AI error mapper replaced the conflict with contradictory advice to retry manually.

The existing direct staff logging path is Found Items -> Log Item -> POST /api/found-reports. Its policy already permits Staff/Admin and creates Unclaimed items with storage and private evidence. AI/manual suggestion actions already use ASP.NET endpoints. The Python AI service stays internal.

The lifecycle is: student lost report -> student found post -> MatchSuggestion (possible candidate) -> staff confirms the same FoundReport -> existing suggestion reads that item's custody -> authenticated owner creates Claim -> staff generates/writes questions -> claimant submits answers -> staff decides -> authorized collection consumes the owner's collection code and resolves the report. Direct staff logging joins this lifecycle at the in-custody FoundReport stage.

## 2. Changes and resulting behavior

- Every mobile data repository depends on the session epoch. Feed notifiers reset at identity changes and discard old request completions. Detail families refresh through their recreated repositories. Logout/login reset navigation, filters, and controller state. Token refresh and authenticated response handling reject results belonging to an earlier session. Logout clears local tokens before remote revocation. Push listeners are stopped on logout.
- API responses carry no-store caching policy for identity-dependent payloads. Existing ownership/role checks remain the security boundary; client controls do not grant authorization.
- Intake retains item/suggestion IDs, records security receipt time, consumes the finder code, and queues custody notifications for active candidates. Its existing history records staff and receipt; storage identifies the desk. Known obsolete system notes are replaced while staff-authored notes remain intact.
- Safe item summaries/feed responses include storageLocationName. Directly logged Unclaimed items are discoverable through the safe found feed. canMessageFinder prevents offering a finder conversation on a staff-only item.
- Report DTOs derive progressStage from real claims and eligible suggestions. Flutter uses Reported -> Possible Match -> Claim Submitted -> Resolved. Confidence alone never means ownership. Approval remains at Claim Submitted until collection. Rejection/cancellation uses the remaining backend search stage.
- Mounted match/report/claim providers and feeds refresh every 30 seconds. Claim and recognition mutations invalidate affected local data immediately. The student web suggestion panel also polls. This does not require Firebase delivery to update custody.
- The existing match becomes claimable after intake with its desk name and Claim this item action. Existing claims open View claim. Before custody, the claimant sees: The finder still has this item. You can claim it after it is handed to security.
- Staff picker disables existing pairs with Already suggested, permits another eligible report, and explains searches where all shown reports are already suggested. Both duplicate APIs return: This item has already been suggested for that lost report. The existing unique index race is mapped to the same conflict text.
- Staff can view scores, notes and matchReason. Reasons derive only from public item-type/colour comparisons and state that ownership still needs verification. Closed reports and reports with an approved claim cannot receive new suggestions.
- Existing verification evidence privacy, staff decisions, rejection/appeal support, collection ID checks, honor awards, messaging, and claim question generation remain available.

## 3. Database changes

No migration was required for the first-round projections. The second round now requires the migration described below. Added response fields are projections/capabilities or derived progress, not new database columns. Existing hand-in code and suggestion uniqueness constraints and row concurrency remain in use. Existing suggestions are updated in place at intake.

## 4. Automated coverage

Backend HTTP integration now executes the full secure verification/approval/collection lifecycle for both a student-posted item and a directly logged item. It creates the lost report through the API; tests 96% candidate discovery, caller-specific finder code/ownership, forbidden removal/code lookup/intake, claim rejection before custody, staff receipt without a duplicate item, code reuse rejection, same suggestion ID after intake, desk location, legacy note correction, candidate notification, consistent duplicate conflicts, claim ownership, hidden-answer privacy, staff questions, claimant answers, staff approval and collection/progress. The direct variant covers staff logging authorization, no finder code, AI and additional manual suggestions, safe feed visibility and the same claim process.

Backend unit tests add closed-report and already-approved-report eligibility coverage for both manual and AI suggestions, ensuring the AI is not called for those reports. Existing backend tests also cover finder withdrawal, handovers, claim rejection, unauthorized edits and private DTOs.

Flutter tests add real AuthController/HTTP-interceptor account switching against controlled responses on one provider container, finder controls/code before logout, no finder data after the next account logs in, secure token clearing, router replacement, old feed completion rejection, old authenticated response rejection, and refresh completion after logout. A widget test advances time to verify that the existing match gains the security desk and Claim action automatically. Progress tests cover candidate, claim, collection, rejection/cancellation and legacy statuses.

Web component tests cover disabled duplicate candidates, manual suggestion to another report, direct-item AI action and the all-candidates-already-suggested explanation. Existing API tests enforce ASP.NET-only AI calls.

## 5. First-round command results

Commands below use the repository root unless another working directory is stated. All final commands returned exit code 0.

| Command | Result |
| --- | --- |
| `dotnet build api/FoundU.sln --artifacts-path C:/Users/brave/FoundU/.workflow-artifacts --no-restore` | Build succeeded; 0 errors, 5 NU1900 warnings about unavailable NuGet vulnerability metadata. |
| `dotnet test api/FoundU.sln --artifacts-path C:/Users/brave/FoundU/.workflow-artifacts --no-build --no-restore --logger 'trx;LogFileName=workflow.trx'` | 221 passed, 9 skipped, 0 failed; 230 total. |
| `flutter analyze --no-pub` (mobile/) | No issues found. |
| `flutter test --no-pub --reporter expanded` (mobile/) | 98 passed, 4 skipped, 0 failed. |
| `npm test -- --reporter=dot` (web/) | 15 files / 57 tests passed. |
| `npm run build` (web/) | TypeScript and Vite production build passed. |
| `git diff --check` | Passed. |

Python source was not modified, so Python tests were not run. Backend AI client tests ran in dotnet test.

The initial default dotnet build encountered locked DLLs from the already-running API. A separate artifacts directory avoided interrupting that process. Initial sandboxed account tests could not write ASP.NET data-protection keys; the final run with approved access passed. Flutter checks likewise needed approved access to the SDK cache outside the workspace.

Backend TRX: api/tests/FoundU.Tests/TestResults/workflow.trx.

## 6. Remaining verification limits

The 9 PostgreSQL tests require TEST_DATABASE_URL naming a clearly test-only database; it was not configured. The 4 live mobile/API tests require FOUNDU_API_URL; they were not run against a live deployment. HTTP tests use the real API/authorization/services with EF's in-memory database and controlled matching/verification agents. Flutter tests use controlled API responses. This establishes regression coverage, but does not constitute an actual emulator + staff browser + PostgreSQL + deployed Python end-to-end retest.

The running API was not restarted and may still serve the earlier build. No production data was changed. Rebuild/restart the API and run the updated mobile/web clients before live retesting.

Remote custody refresh is polling-based, normally within 30 seconds while the relevant screen/provider is active and the API is reachable. Notifications are also queued. The existing direct found logging UI has no implemented found-photo upload endpoint; this change uses its supported public fields and hidden text evidence rather than introducing a new photo subsystem.

## 7. Manual end-to-end retest

1. Restart the updated ASP.NET API with its normal database and internal AI configuration. Run the updated Flutter app and staff web app against that API. Keep Python inaccessible to clients. Use two Students A/B and a Staff or Admin account.
2. As A, create an active blue-backpack lost report. Record its ID. As B, post a matching blue backpack and record the found item ID and hand-in code. Confirm B sees Your post/Found by you/code/removal controls. With the matcher available, A should receive a possible candidate; an exact 96% is controlled in automated tests, not guaranteed by the live matcher.
3. On the same emulator, log B out and A in. Open the board and detail; check that B's code and finder-only controls are absent. Use back navigation and switch tabs to verify old private pages do not return. Verify the authorized A feed response has isMine=false and handInCode=null.
4. As A, open the possible match. Confirm the pre-custody message and absence of Claim. Attempt POST /api/claims with A's report/item IDs: expect 409. Attempts to withdraw B's post or use staff code/intake endpoints must fail authorization.
5. As staff, resolve B's code and receive the physical item. Enter its storage/security desk and hidden verification details. Confirm the resulting found item ID is unchanged, status Unclaimed, desk recorded, and finder code consumed. Reusing the code must fail; a repeated confirmation must return conflict.
6. Leave A's match screen open for up to 30 seconds, or refresh. The same suggestion must show the desk and Claim this item. It must not retain the old Not at a desk yet text. Confirm A receives the custody notification and still cannot see hidden evidence or B's code.
7. On staff's item detail, open suggestions. The A/report pair must say Already suggested and be disabled. Another eligible active report should still be selectable. Test both manual and AI duplicate endpoints: expect the same 409 message. Withdrawn/resolved/approved-collection reports must not be suggestible.
8. A creates the claim. Confirm the tracker advances to Claim Submitted. Staff opens the claim and generates or writes verification questions. A answers; staff can review those answers and the hidden item evidence. A's responses must never contain hidden answers. Another student cannot claim against A's report.
9. Staff approves. The item is reserved but the lost report remains Matched/Claim Submitted until collection. A receives the collection code. Staff checks the owner's ID and collects using that code; the item becomes Returned, report Resolved, tracker Resolved, and the collection code is consumed.
10. Log a separate found item directly as staff with category/type, colour, public description, found location/time, storage desk and hidden evidence. It should immediately be Unclaimed with no student hand-in code. Check staff search and public-safe feed. Generate an AI suggestion or manually suggest it to a different active report. Repeat claim/questions/answers/approval/collection.
11. Repeat with rejection/cancellation: affected reports, matches and claims must refresh to the backend search state. Confirm an unauthorized student cannot approve, reject, collect, remove another person's post or read staff-only found details.

## Second round: remaining workflow fixes

### Root causes traced

The Flutter and React generic found sheets still offered active lost-report selectors and finder controls independent of custody. The matched Flutter action navigated into this generic flow without binding the existing suggestion. A claim request could therefore lose its canonical match context. Compatibility and idempotency needed enforcement at the API boundary as well as in clients. The local claim service also contained unfinished follow-up references and copied claim-creation validation in unrelated methods; these were repaired without discarding the previous changes.

The verification fallback selected a template from the generic evidence field name instead of its actual content. Token-only answer comparison missed a correctly recalled identifier/location expressed in different words. Staff question-generation and decision controls did not consistently enforce the answer/evaluation lifecycle. Revisions allowed old answers to be rewritten, and there was no implemented, evidence-bound follow-up with separately recorded physical observations. Queue/detail views did not poll for cross-client updates. Staff matching offered incompatible or already-suggested reports and displayed its action even when a student-created item had no remaining eligible candidates.

### Resulting behavior and privacy boundary

- Before intake, finder messaging remains available under the existing ownership rules and explains that messaging creates no claim. After intake, both clients show the custody desk and direct owners to claims through their own report matches. Composers are absent for either participant. The API rejects new post-intake messages; historical threads remain readable through the existing authorized endpoint.
- Matched items show **Is this your item?**, **Yes, submit a claim**, and **No, this is not mine**, with no report selector. Yes submits the exact report/item/suggestion IDs; the API verifies authenticated ownership, canonical pairing, compatible category/type and security custody. Sequential duplicate submissions return the existing active claim. A database partial unique index guards concurrent duplicates. No dismisses only that suggestion, leaving report/item intact. Generic feeds direct students to matching instead of offering unrelated reports.
- Claims persist with claimant, canonical lost/found/suggestion and custody location, history and initial Pending status. The mobile tracker becomes Claim Submitted and displays the waiting-for-questions notice. Approval does not resolve the report; authorized collection does. Claim mutations refresh reports/matches/claims/notifications. Session epochs continue to discard old-user requests.
- Staff queue rows include claimant/item, linked report/item IDs, candidate score, desk, claim/verification status and submission time. Queue/detail poll every five seconds; Flutter mounted claim/report/match providers poll every thirty seconds. Notifications are queued through the existing notification model. These are bounded polling updates, not a new realtime transport.
- Initial generation is allowed only for an in-custody, valid initial claim with no questions. It generates and sends one safe item-specific question from one private observation, recording the evidence key in the agent audit. The existing strict agent schemas remain in use. External agent failure uses a deterministic safe fallback. Repeated initial generation/manual sending is blocked. Sent questions and prior answers remain visible; the original generation control never returns.
- Claimants answer outstanding questions in open text. Empty/whitespace, short, duplicate or rewritten answers are rejected. Follow-up screens retain earlier answers read-only. Submission evaluates the complete answer set, records the agent outcome, and returns the claim for staff review.
- Staff-only assessment contains a 0–100 score, matched/missing/conflicting evidence, safe rationale and recommendation. `AiService:VerificationReviewThreshold` is the single configurable classification setting, default 75. Scores at/above it say **Likely valid — staff review.**; lower scores say **More information required — manual review.** Python and API fallback handle exact, partial, wrong and injection answers deterministically. No agent approves, rejects, collects or releases an item.
- Approve and Ask for more detail are disabled until all outstanding answers have a completed evaluation. They are disabled again while an additional answer is outstanding. Reject retains existing availability and requires a safe reason. A staff follow-up requires a confirmed distinct safe question. AI may draft it only from unused original evidence or a new physical observation recorded by authorized staff. Exhausted evidence cannot generate another paraphrase of the same challenge. New observations are stored separately with staff identity/timestamp and history; the original is preserved. Drafting alone does not send a question or persist a new observation. Sending moves the existing internal status to RevisionRequested (displayed as More Information Requested), notifies the owner, and later reevaluates all answers together.
- Student-created items retain existing suggestions through custody. Already-suggested/closed/incompatible reports cannot be selected. The action is hidden when no further eligible report exists and says Suggest to another report when appropriate. Direct staff logging still permits both Staff and Admin, begins in custody without a finder code, and retains AI/manual matching and the same claim lifecycle.
- Mobile and web still call only ASP.NET. Python remains internal. Student projections exclude hidden original/additional evidence, per-question correctness and staff assessment/audit. Question and decision-reason guards prevent expected identifiers/private observations being copied into student-visible text. Existing account isolation, handover-code, removal, ownership, role and collection checks are preserved.

### Database migration (not applied)

`20261004070217_CompleteClaimVerificationWorkflow` adds nullable Claim.MatchSuggestionId and Claim.CustodyLocationId with foreign keys and indexes, backfills these links for existing canonical pairs, adds the active student/report/item partial unique index, and creates FoundVerificationEvidence with separate staff attribution/timestamps and restrictive foreign keys. No original detail or previous claim is deleted.

The model snapshot was checked with EF and reports no pending model changes. The migration has **not** been applied to a database. Review any existing duplicate active claim pairs before applying the unique index: conflicting legacy rows require an explicit manual data decision; migration code does not silently discard them. PostgreSQL execution/concurrency coverage remains skipped until a test-only database is supplied.

### API contract changes

- `POST /api/claims` accepts optional `matchSuggestionId` alongside the existing required lostReportId/foundReportId. A supplied suggestion must belong to the authenticated claimant and match both IDs; omitting it does not bypass ownership, compatibility or custody checks. Repeat active submissions return the existing claim.
- Claim detail adds matchSuggestionId and staff-only verificationForStaff/additionalEvidenceForStaff/canUseUnusedEvidenceForFollowUp. Students receive no assessment or private evidence. Staff reads include the score, evidence lists, rationale and recommendation; action responses use the existing safe projection and clients refetch staff detail.
- Queue rows add lostReportId, foundReportId, storageLocationName, matchScore and verificationStatus.
- Staff/Admin `POST /api/claims/{id}/follow-up/draft` accepts `{question, additionalHiddenDetail}` and returns `{question}`. `POST /api/claims/{id}/follow-up` requires the confirmed question and optionally the new physical observation, sending one evidence-bound follow-up. Both enforce custody/review prerequisites. The original decision endpoint no longer accepts RevisionRequested without a confirmed follow-up; callers must use the dedicated endpoint.
- Lost-report search accepts `excludeSuggestedForFoundReportId` with the existing item category/type filters, excluding existing suggestions. Existing dismiss, question, answer, collection and authorization routes are reused with stronger lifecycle validation.
- The internal AI request/result schemas are retained. No student-facing Python endpoint or new AI approval capability was introduced.

### Focused test coverage added/updated

API tests cover exact match binding and substituted/foreign/incompatible reports, duplicate submission, persisted staff queue/custody linkage, pre-custody rejection, suggestion-only dismissal, finder-message custody gating, item-specific safe generation, blocked repeated generation, open-text answer validation, staff-only scoring, threshold boundary 75/74, fallback/injection, final human decisions, unused-evidence and separately recorded follow-ups, draft nonmutation, original preservation, combined reevaluation and direct staff logging. A new PostgreSQL test checks the active-pair uniqueness constraint across independent contexts when enabled. Previous account/privacy/collection tests are retained.

Flutter widget/provider tests cover matched Yes/No IDs without a dropdown, dismissal, successful claim navigation/progress, custody composer removal/location, initial questions and follow-up-only submission with old answers retained. Previous account-switch/late-request regression tests remain. Web component tests cover queue polling, generation/waiting/evaluation/decision controls, follow-up sending, conditional suggestion action, compatible picker and direct-item AI/manual matching. Python tests cover detail-aware bottle/card questions, non-disclosure, strict structured scoring, partial/wrong/injection answers and deterministic fallback.

### Exact final commands and results

All final checks below exited 0. Commands run from the repository root except where indicated. Isolated .NET artifacts avoid DLL locks from the existing local API process; no process was restarted.

| Command | Result |
| --- | --- |
| `dotnet build api/FoundU.sln --artifacts-path C:/Users/brave/FoundU/.workflow-artifacts --no-restore` | Passed: 0 errors, 5 NU1900 warnings (NuGet vulnerability metadata unavailable). |
| `dotnet test api/FoundU.sln --artifacts-path C:/Users/brave/FoundU/.workflow-artifacts --no-build --no-restore --logger 'trx;LogFileName=workflow-round2-final-all.trx'` | 236 passed, 10 skipped, 0 failed; 246 total. |
| `flutter analyze --no-pub` (mobile/) | Passed: no issues. |
| `flutter test --no-pub --reporter expanded` (mobile/) | 103 passed, 4 skipped, 0 failed. |
| `npm test -- --run` (web/) | 18 files / 65 tests passed. |
| `npm run build` (web/) | TypeScript/Vite production build passed. |
| `.venv/Scripts/python.exe -m pytest -q -o cache_dir=.pytest_cache_round2` (ai/) | 417 passed, 1 skipped, 1 warning. |
| `.venv/Scripts/python.exe -m ruff check .` (ai/) | All checks passed. |
| `dotnet ef migrations has-pending-model-changes --project api/src/FoundU.Infrastructure --startup-project api/src/FoundU.Api --configuration Workflow --no-build` | No changes since last migration; existing global-query-filter relationship warnings. |
| `git diff --check` | Passed (Windows LF/CRLF conversion notices only). |

Backend evidence: `api/tests/FoundU.Tests/TestResults/workflow-round2-final-all.trx`; the first-round `workflow.trx` and intermediate round-two reports remain. The deterministic Python evaluation report is refreshed at `testing/reports/ai-eval/deterministic-results.json` (68 cases passed). Its previously tracked version, which was clean at session start, is preserved separately as `testing/reports/ai-eval/deterministic-results-before-workflow-round2.json`.

The 10 PostgreSQL tests require TEST_DATABASE_URL. Four live Flutter/API tests require FOUNDU_API_URL. One Python PostgreSQL checkpoint test requires TEST_WORKFLOW_DATABASE_URL. None was configured. Python emitted one Starlette/AnyIO BlockingPortal deprecation warning. API tests use real HTTP/authorization/services with in-memory EF and controlled agents; widget tests use controlled responses. They do not validate real PostgreSQL indexes, a restarted deployment, Firebase delivery, or live external-model quality. Earlier sandbox/SDK and locked-output attempts are superseded by the successful final commands. No secrets were printed.

### Required live retest and remaining limitations

**No live end-to-end retest was performed.** The existing local API process was left running and may serve earlier code. Apply the migration after reviewing legacy duplicates, restart the updated ASP.NET/internal Python services, and run updated Flutter/web clients before claiming the live workflow is fixed.

1. Create A's water-bottle and unrelated backpack lost reports; B posts the matching bottle. Check A/B account switching, code/removal isolation, exact suggestion and pre-custody messaging. Claim creation must fail before intake. Try another student's report and the backpack ID through HTTP; authorization/eligibility must reject them.
2. Staff receives B's code into Security Desk – Building A with a private physical mark. Confirm unchanged found/suggestion IDs, consumed code, desk location and claimant notification. After refresh/polling, both composers must be absent and the same suggestion claimable. Staff must see Already suggested and no suggestion action unless another compatible candidate exists.
3. From the exact matched item, check there is no dropdown. No dismisses only that match; use a separate/recreated test suggestion for Yes. Yes creates one Pending claim with exact three IDs, changes progress to Claim Submitted and appears in the open staff queue within its polling interval. Repeat Yes/network retry and verify one active database pair. Item must remain unresolved.
4. Staff initial AI generation sends an item-specific question without its expected identifier. The generation/send controls disappear; waiting text appears and Approve/Ask for more detail are disabled. A receives/refetches the question, cannot send whitespace, and submits an open-text answer. Confirm success, staff-only percentage/rationale, threshold classification and enabled human decision buttons. Inspect A's API responses, screens, notifications and logs for absence of hidden detail and assessment feedback. Test wrong, partial and injection answers in separate claims, including AI service unavailable.
5. Request more detail only after evaluation. Confirm a distinct unused-evidence AI draft, explicit staff confirmation, new notification, More Information Requested, prior answer read-only, and disabled approval while waiting. If the only original observation was used, drafting must require a new observed private detail. Confirm original unchanged, separately attributed new detail, no repeated identifier hint, follow-up answer and combined reevaluation. The initial generation button must never return.
6. Staff rejects with a reason or approves after review. Approval only reserves the item; collection requires owner-ID confirmation and the collection code, then resolves the report/item. Confirm consumed codes cannot be reused. Verify the restricted admin override remains attributable under existing rules.
7. Repeat steps 3–6 with Found Items → Log Item directly as Staff, then Admin. No finder handover code should be required. AI/manual matching must remain available and exclude incompatible/closed/already-suggested reports. Repeat unauthorized staff-route/private-evidence checks and logout/late-request checks.

Remaining constraints: polling is not instantaneous push; legacy manual questions without reliable evidence bindings conservatively require a new observation for follow-up; meaningful free-text fallback is advisory and requires staff review; migration/index/concurrency behavior and real external-model generation need live PostgreSQL/internal-service validation. Existing direct logging photo-upload support was not expanded. No commit, push, merge, pull request, database update or deployment was performed.

## Follow-up: direct confirmation from the mobile found-item sheet

The screenshot exposed a remaining navigation gap: the Fresh finds detail sheet had no authenticated lookup for an existing match, so it always instructed the owner to open My Reports. This follow-up supersedes the second-round generic-sheet navigation for items with eligible existing matches.

The sheet now keeps the custody desk and displays **Is this your item?**, **Yes, this is mine**, and **No, this is not mine** directly. Agent-created suggestions qualify at a score of 0.75 or higher (one mobile cutoff constant); explicit manual staff suggestions remain usable without inventing a score. This candidate cutoff is separate from the staff-only answer-verification threshold. A low-scoring or missing suggestion does not receive direct confirmation controls; the user may view their reports. An existing claim gets View claim instead of another confirmation.

Yes calls the existing ASP.NET claim endpoint with the exact lostReportId, foundReportId and matchSuggestionId, blocks repeated taps while pending, shows **Claim submitted — waiting for verification questions.**, and uses the existing claim mutation to refresh progress, matches, claims and notifications. The claim is persisted and visible to the existing polling Staff/Admin queue. No dismisses only the exact suggestion and refreshes the matching/report state. Multiple eligible reports are presented as individually labelled matched report cards with their own actions; no report dropdown or silent first-report selection is used. Before custody, Yes is disabled. Finder messaging remains disabled after custody. Account/session changes recreate the controls and discard old match responses.

The additive API contract is `GET /api/match-suggestions/mine?foundReportId={id}&page=1&pageSize=100`. The optional item filter is applied before pagination and always combined with authenticated student ownership and existing closed/dismissed/reservation filters. Unfiltered callers retain their existing behavior. Flutter follows every page and also checks canonical found IDs defensively. No migration or Python/React source change was needed for this follow-up; the earlier migration remains unapplied.

Focused coverage adds eleven Flutter tests for direct Yes/No, exact IDs, duplicate-tap blocking, success, 74/75/85 percent eligibility, manual suggestions, multiple reports, existing claims, pre-custody gating, paged API parameters and old-account response rejection. The backend HTTP regression checks filtered pagination, unrelated newer records, unknown item IDs, another student's access and hidden-detail privacy. Existing claim persistence/staff queue and authorization coverage still passes.

| Latest command | Result |
| --- | --- |
| `dotnet build api/FoundU.sln --artifacts-path C:/Users/brave/FoundU/.workflow-artifacts --no-restore` | Passed; 0 errors, 5 existing NU1900 metadata warnings. |
| `dotnet test api/FoundU.sln --artifacts-path C:/Users/brave/FoundU/.workflow-artifacts --no-build --no-restore --logger 'trx;LogFileName=workflow-direct-sheet.trx'` | 237 passed, 10 skipped, 0 failed. |
| `flutter analyze --no-pub` (mobile/) | No issues found. |
| `flutter test --no-pub --reporter expanded` (mobile/) | 114 passed, 4 skipped, 0 failed. |
| `git diff --check` | Passed; Windows line-ending notices only. |

The same unavailable PostgreSQL/live-API tests remain skipped. Web/Python were unchanged in this follow-up and their prior results above remain historical evidence; they were not rerun. Earlier widget attempts identified fixture scrolling/compile issues, which were corrected before these successful final checks. Backend evidence is retained at `api/tests/FoundU.Tests/TestResults/workflow-direct-sheet.trx`.

Live retest remains outstanding: rebuild/restart the API and Flutter app (and apply the earlier migration if still pending). As the report owner, open the matched water bottle directly from Fresh finds. Confirm the desk and Yes/No actions without visiting My Reports. Yes must show success, Claim Submitted progress and the same claim in the already-open staff queue. Repeat submission to verify one active claim. Test No on a separate suggestion, multiple genuine matching reports, a 74% candidate, manual staff suggestion, pre-custody state and account switching. Verify no hidden detail or staff evaluation is returned to the student. No running service was restarted and no live workflow success is claimed. No commit, push, merge or PR was performed.

Files edited in this follow-up:

- `api/src/FoundU.Api/Controllers/MatchSuggestionsController.cs`
- `api/src/FoundU.Application/Abstractions/IMatchSuggestionService.cs`
- `api/src/FoundU.Infrastructure/Matching/MatchSuggestionService.cs`
- `api/tests/FoundU.Tests/ClaimsEndpointIntegrationTests.cs`
- `mobile/lib/features/feed/data/feed_models.dart`
- `mobile/lib/features/feed/presentation/found_post_sheet.dart`
- `mobile/lib/features/claims/presentation/providers/claim_providers.dart`
- `mobile/lib/features/reports/data/report_repository.dart`
- `mobile/lib/features/reports/presentation/providers/report_providers.dart`
- `mobile/lib/features/reports/presentation/possible_matches_page.dart`
- `mobile/test/found_post_custody_test.dart`
- `mobile/test/found_post_matched_claim_test.dart` (new)
- `docs/found-item-workflow-verification.md`

## Mobile verification-question layout

The question was rendered as the answer field's floating label, making a long question small and cramped against the border. Editable initial and follow-up questions now appear as standalone, wrapping question text above the input. The separate multiline input is labelled **Your answer** and has room for three to five lines. Previously submitted answers remain read-only. No API, database, question-generation or verification behavior changed.

Updated the existing claim-detail widget coverage to verify that the question is positioned above the input and the input label is Your answer. `flutter analyze --no-pub` passed with no issues; `flutter test --no-pub test/claim_detail_page_test.dart --reporter expanded` passed all 6 tests; `git diff --check` passed. Only `mobile/lib/features/claims/presentation/claim_detail_page.dart`, `mobile/test/claim_detail_page_test.dart` and this document changed in this follow-up. Earlier checks remain historical; no live device retest was performed. Rebuild/relaunch Flutter, open a claim with an outstanding initial/follow-up question, and confirm the full question wraps above the answer box on a narrow screen and submission still works. No commit or push was performed.

### Complete current changed-file manifest

This lists every modified/untracked nonignored file on the current branch, including preserved first-round changes and generated platform files already present at session start. Generated build/cache/TRX files remain ignored and are referenced above as evidence.

- `.gitignore` (pre-existing change, preserved)
- `ai/app/agents/verification.py`
- `ai/tests/test_verification.py`
- `api/src/FoundU.Api/Controllers/ClaimsController.cs`
- `api/src/FoundU.Api/Controllers/MatchSuggestionsController.cs`
- `api/src/FoundU.Api/Middleware/GlobalExceptionHandler.cs`
- `api/src/FoundU.Api/Program.cs`
- `api/src/FoundU.Application/Abstractions/IClaimService.cs`
- `api/src/FoundU.Application/Abstractions/IMatchSuggestionService.cs`
- `api/src/FoundU.Application/Claims/Dtos/ClaimDtos.cs`
- `api/src/FoundU.Application/Claims/Dtos/VerificationAgentDtos.cs`
- `api/src/FoundU.Application/FoundReports/Dtos/FoundPostDtos.cs`
- `api/src/FoundU.Application/FoundReports/Dtos/FoundReportDtos.cs`
- `api/src/FoundU.Application/LostReports/Dtos/LostReportDtos.cs`
- `api/src/FoundU.Application/Matching/Dtos/MatchSuggestionDtos.cs`
- `api/src/FoundU.Domain/Entities/Claim.cs`
- `api/src/FoundU.Domain/Entities/FoundVerificationEvidence.cs`
- `api/src/FoundU.Infrastructure/Claims/ClaimService.cs`
- `api/src/FoundU.Infrastructure/Claims/SafeVerificationFallback.cs`
- `api/src/FoundU.Infrastructure/Matching/MatchSuggestionService.cs`
- `api/src/FoundU.Infrastructure/Migrations/20261004070217_CompleteClaimVerificationWorkflow.Designer.cs`
- `api/src/FoundU.Infrastructure/Migrations/20261004070217_CompleteClaimVerificationWorkflow.cs`
- `api/src/FoundU.Infrastructure/Migrations/FoundUDbContextModelSnapshot.cs`
- `api/src/FoundU.Infrastructure/Persistence/Configurations/ClaimConfiguration.cs`
- `api/src/FoundU.Infrastructure/Persistence/Configurations/FoundVerificationEvidenceConfiguration.cs`
- `api/src/FoundU.Infrastructure/Persistence/FoundUDbContext.cs`
- `api/src/FoundU.Infrastructure/Reporting/FoundPostService.cs`
- `api/src/FoundU.Infrastructure/Reporting/LostReportService.cs`
- `api/src/FoundU.Infrastructure/Verification/AiServiceOptions.cs`
- `api/src/FoundU.Infrastructure/Verification/VerificationAgentClient.cs`
- `api/tests/FoundU.Tests/ClaimLifecycleTests.cs`
- `api/tests/FoundU.Tests/ClaimVerificationIntegrationTests.cs`
- `api/tests/FoundU.Tests/ClaimsEndpointIntegrationTests.cs`
- `api/tests/FoundU.Tests/FoundPostBoardTests.cs`
- `api/tests/FoundU.Tests/MatchingAgentIntegrationTests.cs`
- `api/tests/FoundU.Tests/PostgresPersistenceIntegrationTests.cs`
- `docs/found-item-workflow-verification.md`
- `mobile/lib/core/api/api_client.dart`
- `mobile/lib/core/api/auth_interceptor.dart`
- `mobile/lib/core/auth/auth_controller.dart`
- `mobile/lib/core/router/app_router.dart`
- `mobile/lib/features/account/data/account_repository.dart`
- `mobile/lib/features/auth/data/auth_repository.dart`
- `mobile/lib/features/claims/data/claim_models.dart`
- `mobile/lib/features/claims/data/claim_repository.dart`
- `mobile/lib/features/claims/presentation/claim_detail_page.dart`
- `mobile/lib/features/claims/presentation/providers/claim_providers.dart`
- `mobile/lib/features/feed/data/feed_models.dart`
- `mobile/lib/features/feed/data/feed_repository.dart`
- `mobile/lib/features/feed/presentation/feed_controller.dart`
- `mobile/lib/features/feed/presentation/found_feed_controller.dart`
- `mobile/lib/features/feed/presentation/found_post_sheet.dart`
- `mobile/lib/features/handover/data/handover_repository.dart`
- `mobile/lib/features/help/data/help_repository.dart`
- `mobile/lib/features/intake/data/intake_repository.dart`
- `mobile/lib/features/notifications/data/notification_repository.dart`
- `mobile/lib/features/notifications/data/push_notification_manager.dart`
- `mobile/lib/features/reference/data/reference_repository.dart`
- `mobile/lib/features/reports/data/report_models.dart`
- `mobile/lib/features/reports/data/report_repository.dart`
- `mobile/lib/features/reports/presentation/my_reports_page.dart`
- `mobile/lib/features/reports/presentation/possible_matches_page.dart`
- `mobile/lib/features/reports/presentation/providers/report_providers.dart`
- `mobile/lib/features/reports/presentation/report_detail_page.dart`
- `mobile/lib/features/reports/presentation/report_progress.dart`
- `mobile/lib/features/support/data/support_repository.dart`
- `mobile/test/account_switch_workflow_test.dart`
- `mobile/test/claim_detail_page_test.dart`
- `mobile/test/found_post_custody_test.dart`
- `mobile/test/found_post_matched_claim_test.dart`
- `mobile/test/possible_matches_score_test.dart`
- `mobile/test/report_progress_test.dart`
- `testing/reports/ai-eval/deterministic-results-before-workflow-round2.json`
- `testing/reports/ai-eval/deterministic-results.json`
- `web/package-lock.json`
- `web/src/features/claims/claim-detail-page.tsx`
- `web/src/features/claims/claim-queue-page.tsx`
- `web/src/features/claims/claims-api.ts`
- `web/src/features/claims/suggestions-panel.tsx`
- `web/src/features/feed/feed-api.ts`
- `web/src/features/feed/found-feed.tsx`
- `web/src/features/items/ai-match-feedback.ts`
- `web/src/features/items/item-detail-page.tsx`
- `web/src/features/items/items-api.ts`
- `web/src/features/items/link-report-dialog.tsx`
- `web/tests/features/claims/claim-lifecycle-controls.test.tsx`
- `web/tests/features/claims/claim-queue-refresh.test.tsx`
- `web/tests/features/items/ai-match-feedback.test.ts`
- `web/tests/features/items/conditional-suggest-action.test.tsx`
- `web/tests/features/items/link-report-dialog.test.tsx`
