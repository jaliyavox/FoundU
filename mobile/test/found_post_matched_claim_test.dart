import 'dart:async';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/auth/auth_controller.dart';
import 'package:foundu/core/auth/auth_session.dart';
import 'package:foundu/features/auth/data/auth_models.dart';
import 'package:foundu/features/claims/data/claim_models.dart';
import 'package:foundu/features/claims/presentation/providers/claim_providers.dart';
import 'package:foundu/features/feed/data/feed_models.dart';
import 'package:foundu/features/feed/presentation/found_feed_controller.dart';
import 'package:foundu/features/feed/presentation/found_post_sheet.dart';
import 'package:foundu/features/reports/data/report_models.dart';
import 'package:foundu/features/reports/data/report_repository.dart';
import 'package:foundu/features/reports/presentation/providers/report_providers.dart';

void main() {
  testWidgets('feed sheet Yes binds all three IDs, blocks retries and shows claim success', (tester) async {
    final reports = _Matches([_match()]);
    final claims = _Claims()..pending = Completer<ClaimDetail>();
    await _open(tester, reports, claims);
    expect(find.text('Is this your item?'), findsOneWidget);
    expect(find.text('Open your matches to submit a claim'), findsNothing);
    expect(find.byType(DropdownButtonFormField<String>), findsNothing);
    await tester.tap(find.text('Yes, this is mine'));
    await tester.pump();
    expect(claims.calls, 1);
    expect(claims.request!.lostReportId, 'lost-bottle');
    expect(claims.request!.foundReportId, 'bottle');
    expect(claims.request!.matchSuggestionId, 'match-bottle');
    expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Yes, this is mine')).onPressed, isNull);
    expect(tester.widget<OutlinedButton>(find.widgetWithText(OutlinedButton, 'No, this is not mine')).onPressed, isNull);
    claims.pending!.complete(_claim());
    await tester.pumpAndSettle();
    expect(find.text('Claim submitted — waiting for verification questions.'), findsOneWidget);
    expect(find.text('Yes, this is mine'), findsNothing);
    expect(find.text('View claim'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('feed sheet No dismisses the exact match and preserves the report', (tester) async {
    final reports = _Matches([_match()]);
    final claims = _Claims();
    await _open(tester, reports, claims);
    await tester.ensureVisible(find.text('No, this is not mine'));
    await tester.tap(find.text('No, this is not mine'));
    await tester.pumpAndSettle();
    expect(reports.dismissed, 'match-bottle');
    expect(reports.withdrawals, 0);
    expect(claims.calls, 0);
    expect(find.textContaining('Your lost report remains active.'), findsOneWidget);
    expect(find.text('Yes, this is mine'), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });

  for (final score in [0.74, 0.75, 0.85]) {
    testWidgets('sheet uses the 75 percent candidate cutoff: $score', (tester) async {
      await _open(tester, _Matches([_match(score: score)]), _Claims());
      expect(find.text('Is this your item?'), score >= .75 ? findsOneWidget : findsNothing);
      if (score < .75) expect(find.textContaining('no eligible match'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
    });
  }

  testWidgets('manual staff suggestion needs no invented AI score', (tester) async {
    await _open(tester, _Matches([_match(manual: true)]), _Claims());
    expect(find.text('Yes, this is mine'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('multiple genuine matches name each report rather than silently choosing', (tester) async {
    await _open(tester, _Matches([_match(), _match(id: 'second', report: 'second-lost', description: 'Bottle with dent')]), _Claims());
    expect(find.text('Is this your item?'), findsNWidgets(2));
    expect(find.text('Your lost report: Red water bottle'), findsOneWidget);
    expect(find.text('Your lost report: Bottle with dent'), findsOneWidget);
    expect(find.byType(DropdownButtonFormField<String>), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('already claimed suggestion opens claim without another confirmation', (tester) async {
    await _open(tester, _Matches([_match(claimId: 'claim')]), _Claims());
    expect(find.text('View claim'), findsOneWidget);
    expect(find.text('Yes, this is mine'), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('matched feed item cannot be claimed before security intake', (tester) async {
    final claims = _Claims();
    await _open(tester, _Matches([_match()]), claims, status: 'Posted');
    expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Yes, this is mine')).onPressed, isNull);
    expect(find.text('You can submit a claim after this item is handed to security.'), findsOneWidget);
    expect(claims.calls, 0);
    await tester.pumpWidget(const SizedBox());
  });

  test('old item-match response cannot replace a new account session', () async {
    final reports = _SessionMatches();
    final container = ProviderContainer(overrides: [reportRepositoryProvider.overrideWithValue(reports)]);
    addTearDown(container.dispose);
    final provider = foundItemMatchesProvider('bottle');
    final subscription = container.listen(provider, (_, next) {});
    addTearDown(subscription.close);
    await Future<void>.delayed(Duration.zero);
    container.read(authSessionEpochProvider.notifier).advance();
    final current = await container.read(provider.future);
    expect(current.single.id, 'new-account-match');
    reports.old.complete([_match(id: 'old-account-match')]);
    await Future<void>.delayed(Duration.zero);
    expect(container.read(provider).requireValue.single.id, 'new-account-match');
  });

  test('repository filters canonical item and fetches every page of own suggestions', () async {
    final requests = <RequestOptions>[];
    final dio = Dio()..interceptors.add(InterceptorsWrapper(onRequest: (request, handler) {
      requests.add(request);
      handler.resolve(Response(requestOptions: request, data: {'items': [], 'totalCount': 0,
        'page': request.queryParameters['page'], 'pageSize': 100, 'totalPages': 2,
        'hasNextPage': request.queryParameters['page'] == 1, 'hasPreviousPage': false}));
    }));
    expect(await LostReportRepository(dio: dio).getMatchesForFoundItem('bottle'), isEmpty);
    expect(requests.length, 2);
    for (final request in requests) {
      expect(request.path, '/api/match-suggestions/mine');
      expect(request.queryParameters['foundReportId'], 'bottle');
    }
  });
}

Future<void> _open(WidgetTester tester, _Matches reports, _Claims claims, {String status = 'Unclaimed'}) async {
  final post = FoundPost(id: 'bottle', postedByName: 'Student B', isMine: false,
    categoryName: 'Other', itemTypeName: 'Water Bottle', foundLocationName: 'Library',
    description: 'Red water bottle', primaryColor: 'Red', foundAt: DateTime.utc(2026), status: status, canMessageFinder: false,
    storageLocationName: 'Library Front Desk', handInCode: null, createdAt: DateTime.utc(2026));
  await tester.pumpWidget(ProviderScope(overrides: [
    authControllerProvider.overrideWith(_Student.new),
    foundFeedControllerProvider.overrideWith(_EmptyFeed.new),
    reportRepositoryProvider.overrideWithValue(reports),
    claimControllerProvider.overrideWith(() => claims),
  ], child: MaterialApp(home: Scaffold(body: Builder(builder: (context) => TextButton(
    onPressed: () => showFoundPostDetail(context, post), child: const Text('Open')))))));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Open'));
  await tester.pumpAndSettle();
  await tester.scrollUntilVisible(find.byKey(const ValueKey('found-item-match-actions')), 250,
    scrollable: find.byType(Scrollable).last);
  await tester.pumpAndSettle();
}

MatchSuggestionModel _match({double score = .85, bool manual = false, String id = 'match-bottle',
  String report = 'lost-bottle', String description = 'Red water bottle', String? claimId}) => MatchSuggestionModel(
  id: id, lostReportId: report, lostReportDescription: description,
  foundItem: FoundReportSummaryModel(id: 'bottle', categoryName: 'Other', itemTypeName: 'Water Bottle',
    foundLocationName: 'Library', generalDescription: 'Red water bottle', foundAt: DateTime.utc(2026), status: 'Unclaimed'),
  status: claimId == null ? 'Suggested' : 'Confirmed', isAgentGenerated: !manual,
  matchScore: manual ? null : score, claimId: claimId, createdAt: DateTime.utc(2026));

ClaimDetail _claim() => ClaimDetail(id: 'claim', status: 'Pending', lostReportId: 'lost-bottle',
  lostReportDescription: 'Red water bottle', questions: const [], createdAt: DateTime.utc(2026), updatedAt: DateTime.utc(2026),
  foundItem: FoundItemSummary(id: 'bottle', categoryName: 'Other', itemTypeName: 'Water Bottle',
    foundLocationName: 'Library', generalDescription: 'Red bottle', foundAt: DateTime.utc(2026), status: 'Unclaimed'));

class _Student extends AuthController {
  @override
  Future<AuthUser?> build() async => const AuthUser(id: 'A', name: 'Student A', email: 'a@test', role: 'Student');
}
class _EmptyFeed extends FoundFeedController {
  @override
  FoundFeedState build() => const FoundFeedState();
}
class _Matches extends LostReportRepository {
  _Matches(this.items) : super(dio: Dio());
  final List<MatchSuggestionModel> items;
  String? dismissed;
  int withdrawals = 0;
  @override
  Future<List<MatchSuggestionModel>> getMatchesForFoundItem(String foundItemId) async => items.where((m) => m.id != dismissed).toList();
  @override
  Future<void> dismissMatch(String matchId) async { dismissed = matchId; }
  @override
  Future<LostReportDetailModel> withdrawReport(String id, String? reason) async {
    withdrawals++; throw StateError('Must not withdraw a report');
  }
}
class _Claims extends ClaimController {
  int calls = 0;
  CreateClaimRequest? request;
  Completer<ClaimDetail>? pending;
  @override
  Future<void> build() async {}
  @override
  Future<ClaimDetail> create(CreateClaimRequest input) async {
    calls++; request = input;
    return pending == null ? _claim() : await pending!.future;
  }
}

class _SessionMatches extends LostReportRepository {
  _SessionMatches() : super(dio: Dio());
  final old = Completer<List<MatchSuggestionModel>>();
  int calls = 0;
  @override
  Future<List<MatchSuggestionModel>> getMatchesForFoundItem(String foundItemId) async {
    return ++calls == 1 ? await old.future : [_match(id: 'new-account-match')];
  }
}
