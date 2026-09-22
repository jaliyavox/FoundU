import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/claims/presentation/claim_submission_page.dart';

void main() {
  testWidgets('valid claim route creates the submission page', (tester) async {
    await tester.pumpWidget(ProviderScope(
      child: MaterialApp(
        home: claimSubmissionRoutePage(
          Uri.parse('/claims/new?lostReportId=lost-1&foundReportId=found-1'),
        ),
      ),
    ));

    expect(find.byType(ClaimSubmissionPage), findsOneWidget);
  });

  testWidgets('missing claim route IDs show a safe error page', (tester) async {
    await tester.pumpWidget(ProviderScope(
      child: MaterialApp(
        home: claimSubmissionRoutePage(Uri.parse('/claims/new?lostReportId=x')),
      ),
    ));

    expect(find.byType(InvalidClaimSubmissionRoutePage), findsOneWidget);
    expect(find.textContaining('incomplete'), findsOneWidget);
  });
}
