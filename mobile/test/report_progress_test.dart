import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/reports/presentation/report_progress.dart';

void main() {
  test('candidate confidence never promotes an active report to a submitted claim', () {
    expect(reportProgressIndex('Active', 'Reported'), 0);
    expect(reportProgressIndex('Active', 'PossibleMatch'), 1);
    expect(reportProgressLabels[1], 'Possible Match');
  });

  test('claim submission and approval remain at claim stage until collection', () {
    expect(reportProgressIndex('Matched', 'ClaimSubmitted'), 2);
    expect(reportProgressIndex('Resolved', 'Resolved'), 3);
  });

  test('rejection or cancellation returns to the backend search stage', () {
    expect(reportProgressIndex('Active', 'PossibleMatch'), 1);
    expect(reportProgressIndex('Active', 'Reported'), 0);
    expect(reportProgressIndex('Withdrawn', 'PossibleMatch'), 0);
  });

  test('legacy Matched means a claim was submitted, and Resolved means collected', () {
    expect(reportProgressIndex('Matched', 'Reported'), 2);
    expect(reportProgressIndex('Resolved', 'Reported'), 3);
  });
}
