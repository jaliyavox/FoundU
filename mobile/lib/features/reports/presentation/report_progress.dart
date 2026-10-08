/// How far a lost report has got. A finder's hand-in counts too: the item can reach the desk
/// with no suggestion or claim, and the tracker used to sit on "Reported" while it waited there.
const reportProgressLabels = ['Reported', 'Possible Match', 'Claim Submitted', 'Back with Owner'];

/// `Matched` is ambiguous: a finder handover can set it without an ownership claim.
int reportProgressIndex(String status, String progressStage) {
  if (status == 'Resolved') return 3;
  if (status == 'Withdrawn') return 0;
  if (const {
    'ClaimSubmitted', 'VerificationQuestions', 'RevisionRequested',
    'ClaimUnderReview', 'ClaimApproved', 'AtDesk',
  }.contains(progressStage)) {
    return 2;
  }
  if (const {'PossibleMatch', 'FinderFound', 'FinderOnTheWay'}
      .contains(progressStage)) {
    return 1;
  }
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
    'FinderFound' => 'A finder says they have it - check your messages',
    'FinderOnTheWay' => 'A finder is taking it to the security desk',
    'AtDesk' =>
      'At the security desk - collect it with your collection code and student ID',
    _ => null,
  };
}
