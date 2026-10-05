/// Suggestion and claim progress only. Finder contact and desk custody have separate notices.
const reportProgressLabels = ['Reported', 'Possible Match', 'Claim Submitted', 'Back with Owner'];

/// `Matched` is ambiguous: a finder handover can set it without an ownership claim.
int reportProgressIndex(String status, String progressStage) {
  if (status == 'Resolved') return 3;
  if (status == 'Withdrawn') return 0;
  if (const {
    'ClaimSubmitted', 'VerificationQuestions', 'RevisionRequested',
    'ClaimUnderReview', 'ClaimApproved',
  }.contains(progressStage)) {
    return 2;
  }
  if (progressStage == 'PossibleMatch') return 1;
  return 0;
}

bool finderContacted(int foundClaimCount, int messageCount) =>
    foundClaimCount > 0 || messageCount > 0;

String? reportProgressDetail(String status, String progressStage) {
  if (status == 'Withdrawn') return 'Withdrawn; no longer being matched';
  if (status == 'Resolved') return null;
  return switch (progressStage) {
    'VerificationQuestions' => 'Verification questions available',
    'RevisionRequested' => 'More information requested for your claim',
    'ClaimUnderReview' => 'Claim under staff review',
    'ClaimApproved' => 'Claim approved; item awaiting collection',
    _ => null,
  };
}
