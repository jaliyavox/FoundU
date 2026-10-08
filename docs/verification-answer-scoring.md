# Question-specific answer scoring

## Confirmed cause and path

Flutter submits `{ answers: [{ questionId, answerText }] }` to the ASP.NET claims API.
`ClaimService.SubmitAnswersAsync` validates ownership, outstanding questions and input,
then stores `ClaimAnswer.AnswerText`. It reconstructs each saved question's agent ID and
wording from its generation audit and retrieves the original hidden observation by the
audit's evidence keys. `VerificationAgentClient` sends those questions, answers and the
hidden evidence mapping through the authenticated internal `/agents/run` contract.

Previously Python `_build_challenges` bound the entire observation as the expected value.
`evaluate_answers` validated question grounding but passed that complete paragraph to
`_evaluate_answer`. ASP.NET fallback likewise matched answers to whole sorted observations
by array position. Neither selected the attribute actually requested by the question.
Reproducing the exact supplied paragraph/question/Black request locally before the scoring
fix returned `no_match`, score `0.07142857142857142` (7.14%). The reported live 71% was not
reproduced; ASP.NET also accepted inconsistent result/score pairs, such as `no_match` at
0.71. That validation gap is now closed. No deployment or live-user session was used to
claim that the exact live percentage had been reproduced.

Python scoring is deterministic and does not call an LLM. The fake provider is only a
question-drafting provider; it cannot override answer evaluation. ASP.NET's HTTP client
turns Python's per-question scores into percentages and safe message categories.
`ClaimService` calculates the question-weighted arithmetic mean across generation and
follow-up groups, applies the unchanged 75% staff review threshold, and persists the staff
assessment in the evaluation audit. The React staff page displays that assessment as-is;
it does not compute a different score. Student claim responses exclude the assessment.

## Fix

The existing shared grounding catalogue now includes internal extraction patterns and
answer kinds. Both services resolve the saved question and its source evidence to a
unique expected attribute: cap colour, brand, scratch location, sticker colour/location,
cap type, or presence. Descriptive questions select the relevant feature span or one
original clause rather than unrelated paragraph facts. Ambiguous or unsupported bindings
return insufficient information and require staff assessment.

Deterministic typed checks run before trusting provider scores. Obvious exact and harmless
sentence variants receive 1.0, including Black, The cap is black, It has a black cap and
Black colour. Normalisation handles case/punctuation, colour/color and grey/gray. Known
different colours or incompatible locations are contradictions at 0. Unknown or off-topic
answers are insufficient at 0; partial information is 0.4. Uncertainty and missing answers
are insufficient; API input validation still rejects empty submitted answers. Negation,
multiple alternative colours, unrelated subject wording and answer stuffing cannot turn
one overlapping word into a strong match. Broader descriptions retain advisory lexical
comparison against their own feature observation; unrestricted semantic inference is not
introduced.

The internal Python response already had per-question evaluations. ASP.NET now retains
them in its internal result model and persists safe question GUID/result/percentage rows
inside the staff-only assessment. Result/score consistency and IDs are validated. Typed
answers are checked deterministically in ASP.NET too, so stale, unavailable, malformed or
inconsistent service/provider outputs cannot reintroduce paragraph scoring. Local fallback
receives the actual saved questions and associates answers by question ID.

No EF schema change is required: saved question text, generation audit evidence keys and
the staff-only source observation already identify the target. Newly generated question
audits additionally store source evidence fingerprints in existing JSON. If that source
is edited before scoring, the system marks the evaluation insufficient instead of silently
changing the expected answer. Legacy audits without fingerprints continue to resolve
against their stored evidence reference. No expected answer is put in audits, public
question DTOs, model prompts or student responses.

## Validation and limitations

Python and ASP.NET regressions cover all requested direct/equivalent/wrong/uncertain and
cross-fact answers, multiple independent questions, new follow-up observations, edited
source observations, fake-provider isolation, unavailable/malformed/inconsistent HTTP
responses, and persistence/privacy through the student/staff API boundary. Existing
grounding tests remain in place. Flutter and React were inspected but not changed; their
student response contract remains unchanged. Ownership always needs a staff decision.

Automated validation does not constitute a new live claim test. Existing stored scores
are not rewritten. Ambiguous observations and unrecognised semantic equivalents still
require staff review. Restart both changed services before retesting.

Final checks: Ruff passed; focused Python verification/scoring tests passed (106);
full pytest passed (479 passed, 1 skipped); focused ASP.NET claim/verification/fallback
tests passed (180 passed, 6 skipped); full dotnet test passed (341 passed, 10 skipped). Full ASP.NET tests
needed normal Data Protection key access outside the filesystem sandbox. `dotnet build`
passed with zero errors and five NU1900 warnings because the NuGet vulnerability feed
was unreachable. Isolated build outputs avoided changing the running API's assemblies.
No Flutter or web source files changed. `git diff --check` passed. Test-generated timing
changes in the unrelated evaluation report were removed.

Final audit also tightened the service-side guard for descriptive questions: a provider
result without per-question evaluations, or one whose matched/missing/conflicting summary
contradicts its evaluations, uses the deterministic fallback.

## Manual retest

1. Stop the running ASP.NET and Python instances, preserving their existing environment
   settings and service key. Rebuild/start ASP.NET with
   `dotnet run --project api/src/FoundU.Api --launch-profile http` from the repository root.
   From `ai`, restart Python with `.venv\Scripts\python.exe -m uvicorn app.main:app --reload`
   (or its existing configured port if it differs from the default 8000).
2. As staff, log a fresh item in security custody with the exact hidden observation:
   `Nike stainless-steel water bottle with a black screw cap. It has a small scratch near the bottom and a white sticker on one side.`
3. Create an eligible match and a completely fresh student claim. Generate its question
   as staff; confirm it asks `What color is the bottle cap?`.
4. As that student, submit `Black`. Confirm their response contains their question,
   submitted answer and `UnderReview` status, without hidden evidence or staff scoring.
5. As staff, reopen the claim. Expect 100%, a matched-evidence message, no conflicting
   information or `evidence did not match` warning, and no automatic ownership approval.
6. Repeat with fresh claims for equivalent correct wording, White/Blue, Nike and I don't
   know. Wrong colours should score 0 with a contradiction; off-topic/uncertain answers
   should score 0 with insufficient information. Check other-account access remains denied.

## Files changed for this scoring fix

- `ai/app/agents/verification.py`
- `ai/app/agents/verification_grounding.py`
- `ai/app/agents/verification_grounding.json`
- `ai/app/agents/verification_scoring.py`
- `ai/tests/test_verification_scoring.py`
- `api/src/FoundU.Application/Claims/Dtos/ClaimDtos.cs`
- `api/src/FoundU.Application/Claims/Dtos/VerificationAgentDtos.cs`
- `api/src/FoundU.Infrastructure/Claims/ClaimService.cs`
- `api/src/FoundU.Infrastructure/Claims/SafeVerificationFallback.cs`
- `api/src/FoundU.Infrastructure/Claims/VerificationGrounding.cs`
- `api/src/FoundU.Infrastructure/Claims/VerificationAnswerScoring.cs`
- `api/src/FoundU.Infrastructure/Verification/VerificationAgentClient.cs`
- `api/tests/FoundU.Tests/ClaimVerificationIntegrationTests.cs`
- `api/tests/FoundU.Tests/ClaimsEndpointIntegrationTests.cs`
- `api/tests/FoundU.Tests/VerificationAgentClientTests.cs`
- `api/tests/FoundU.Tests/VerificationAnswerScoringTests.cs`
- `docs/verification-answer-scoring.md`

Earlier uncommitted question-grounding changes are preserved alongside these edits.
