import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/reports/presentation/report_progress.dart';

void main() {
  final states = [
    ('newly reported', 'Active', 'Reported', 0, 0, 0, null),
    ('finder contacted owner', 'Active', 'Reported', 1, 0, 0, null),
    ('staff received item without a claim', 'Matched', 'Reported', 1, 0, 0, null),
    ('suggestion created', 'Active', 'PossibleMatch', 0, 0, 1, null),
    ('claim submitted', 'Matched', 'ClaimSubmitted', 0, 0, 2, null),
    ('verification questions', 'Matched', 'VerificationQuestions', 0, 0, 2, 'Verification questions available'),
    ('follow-up requested', 'Matched', 'RevisionRequested', 0, 0, 2, 'More information requested for your claim'),
    ('claim under review', 'Matched', 'ClaimUnderReview', 0, 0, 2, 'Claim under staff review'),
    ('claim approved but still at desk', 'Matched', 'ClaimApproved', 0, 0, 2, 'Claim approved; item awaiting collection'),
    ('approved and returned', 'Resolved', 'Resolved', 0, 0, 3, null),
    ('rejected claim', 'Active', 'Reported', 0, 0, 0, null),
    ('withdrawn report', 'Withdrawn', 'Reported', 0, 0, 0, 'Withdrawn; no longer being matched'),
  ];

  for (final (name, status, progressStage, foundClaimCount, messageCount, stage, detail) in states) {
    test(name, () {
      expect(reportProgressIndex(status, progressStage), stage);
      expect(finderContacted(foundClaimCount, messageCount), foundClaimCount > 0 || messageCount > 0);
      expect(reportProgressDetail(status, progressStage), detail);
    });
  }

  test('a finder message alone is contact, not a suggestion', () {
    expect(finderContacted(0, 1), isTrue);
    expect(reportProgressIndex('Active', 'Reported'), 0);
  });

  test('legacy Matched does not imply custody or a claim', () {
    expect(reportProgressIndex('Matched', 'Reported'), 0);
    expect(reportProgressLabels, ['Reported', 'Possible Match', 'Claim Submitted', 'Back with Owner']);
  });
}
