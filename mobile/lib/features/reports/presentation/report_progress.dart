const reportProgressLabels = ['Reported', 'Possible Match', 'Claim Submitted', 'Resolved'];

/// Matched is the legacy API condition for a submitted claim, never a confidence score.
int reportProgressIndex(String status, String progressStage) {
  if (status == 'Resolved') return 3;
  if (status == 'Withdrawn') return 0;
  if (progressStage == 'ClaimSubmitted' || status == 'Matched') return 2;
  if (progressStage == 'PossibleMatch') return 1;
  return 0;
}
