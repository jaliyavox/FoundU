import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/reports/data/report_models.dart';
import 'package:foundu/features/reports/data/report_repository.dart';
import 'package:foundu/features/reports/presentation/possible_matches_page.dart';
import 'package:foundu/features/reports/presentation/providers/report_providers.dart';
import 'package:go_router/go_router.dart';
import 'package:foundu/features/claims/data/claim_models.dart';
import 'package:foundu/features/claims/presentation/providers/claim_providers.dart';

void main() {
  testWidgets('matched item uses Yes No with exact IDs and no unrelated report selector', (tester) async {
    final reports = _CustodyRepository()..inCustody = true;
    final controller = _CapturingClaims();
    final router = GoRouter(routes: [
      GoRoute(path: '/', builder: (context, state) => const PossibleMatchesPage(reportId: 'lost')),
      GoRoute(path: '/claims/:id', builder: (context, state) => const Scaffold(body: Text('Claim submitted — waiting for verification questions.'))),
    ]);
    await tester.pumpWidget(ProviderScope(overrides: [
      reportRepositoryProvider.overrideWithValue(reports),
      claimControllerProvider.overrideWith(() => controller),
    ], child: MaterialApp.router(routerConfig: router)));
    await tester.pumpAndSettle();
    expect(find.text('Is this your item?'), findsOneWidget);
    expect(find.byType(DropdownButtonFormField<String>), findsNothing);
    expect(find.text('No, this is not mine'), findsOneWidget);
    await tester.tap(find.text('Yes, submit a claim'));
    await tester.pumpAndSettle();
    expect(controller.request!.lostReportId, 'lost');
    expect(controller.request!.foundReportId, 'same-item');
    expect(controller.request!.matchSuggestionId, 'same-match');
    expect(find.text('Claim submitted — waiting for verification questions.'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
    router.dispose();
  });

  testWidgets('No dismisses the exact suggestion and removes it without withdrawing report', (tester) async {
    final reports = _CustodyRepository()..inCustody = true;
    await tester.pumpWidget(ProviderScope(overrides: [reportRepositoryProvider.overrideWithValue(reports)],
      child: const MaterialApp(home: PossibleMatchesPage(reportId: 'lost'))));
    await tester.pumpAndSettle();
    await tester.tap(find.text('No, this is not mine'));
    await tester.pumpAndSettle();
    expect(reports.dismissed, 'same-match');
    expect(find.text('Yes, submit a claim'), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });
  testWidgets('existing possible match refreshes to claimable after security intake', (tester) async {
    final repository = _CustodyRepository();
    await tester.pumpWidget(ProviderScope(
      overrides: [reportRepositoryProvider.overrideWithValue(repository)],
      child: const MaterialApp(home: PossibleMatchesPage(reportId: 'lost')),
    ));
    await tester.pumpAndSettle();
    expect(find.text('Yes, submit a claim'), findsNothing);
    expect(find.text('The finder still has this item. You can claim it after it is handed to security.'), findsOneWidget);
    repository.inCustody = true;
    await tester.pump(const Duration(seconds: 31));
    await tester.pumpAndSettle();
    expect(find.text('Yes, submit a claim'), findsOneWidget);
    expect(find.text('At security — Claim now: Main Security Desk'), findsOneWidget);
    expect(repository.calls, 2);
    expect(find.textContaining('finder still has'), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });
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
      expect(find.text('Yes, submit a claim'), status == 'Posted' ? findsNothing : findsOneWidget);
    });
  }
}

class _CustodyRepository extends LostReportRepository {
  _CustodyRepository() : super(dio: Dio());
  bool inCustody = false;
  int calls = 0;
  String? dismissed;
  @override
  Future<void> dismissMatch(String matchId) async { dismissed = matchId; }
  @override
  Future<List<MatchSuggestionModel>> getPossibleMatches(String reportId) async {
    calls++;
    if (dismissed != null) return [];
    return [MatchSuggestionModel(
      id: 'same-match', lostReportId: 'lost', lostReportDescription: 'Blue backpack',
      foundItem: FoundReportSummaryModel(id: 'same-item', categoryName: 'Bags', itemTypeName: 'Backpack',
        foundLocationName: 'Library', generalDescription: 'Blue backpack', foundAt: DateTime.utc(2026),
        status: inCustody ? 'Unclaimed' : 'Posted', storageLocationName: inCustody ? 'Main Security Desk' : null),
      status: 'Suggested', isAgentGenerated: true, matchScore: 0.96, createdAt: DateTime.utc(2026),
    )];
  }
}

class _CapturingClaims extends ClaimController {
  CreateClaimRequest? request;
  @override
  Future<void> build() async {}
  @override
  Future<ClaimDetail> create(CreateClaimRequest input) async {
    request = input;
    return ClaimDetail(id: 'claim', status: 'Pending', lostReportId: input.lostReportId,
      lostReportDescription: 'Water bottle', questions: const [], createdAt: DateTime.utc(2026), updatedAt: DateTime.utc(2026),
      foundItem: FoundItemSummary(id: input.foundReportId, categoryName: 'Other', itemTypeName: 'Water Bottle',
        foundLocationName: 'Library', generalDescription: 'Red bottle', foundAt: DateTime.utc(2026), status: 'Unclaimed'));
  }
}
