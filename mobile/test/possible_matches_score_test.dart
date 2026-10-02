import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/reports/data/report_models.dart';
import 'package:foundu/features/reports/presentation/possible_matches_page.dart';
import 'package:foundu/features/reports/presentation/providers/report_providers.dart';

void main() {
  for (final status in ['Posted', 'Unclaimed']) {
    testWidgets('match score preserves $status claim behavior', (tester) async {
      final match = MatchSuggestionModel(
        id: 'match', lostReportId: 'lost', lostReportDescription: 'Blue card',
        foundItem: FoundReportSummaryModel(
          id: 'found', categoryName: 'Cards', itemTypeName: 'Bank Card',
          foundLocationName: 'Library', generalDescription: 'Blue BOC card',
          foundAt: DateTime.utc(2026), status: status,
        ),
        status: 'Suggested', isAgentGenerated: true, matchScore: 0.956,
        createdAt: DateTime.utc(2026),
      );
      await tester.pumpWidget(ProviderScope(
        overrides: [possibleMatchesProvider('lost').overrideWith((ref) async => [match])],
        child: const MaterialApp(home: PossibleMatchesPage(reportId: 'lost')),
      ));
      await tester.pumpAndSettle();
      expect(find.text('Match score 96%'), findsOneWidget);
      expect(find.textContaining('confidence'), findsNothing);
      expect(find.text('Start claim'), status == 'Posted' ? findsNothing : findsOneWidget);
    });
  }
}
