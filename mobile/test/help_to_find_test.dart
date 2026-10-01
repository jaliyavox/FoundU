import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/theme/app_theme.dart';
import 'package:foundu/features/help/data/help_models.dart';
import 'package:foundu/features/help/data/help_repository.dart';
import 'package:foundu/features/help/presentation/help_to_find_page.dart';

Map<String, dynamic> activity({
  String kind = 'found-claim',
  String? code = '483921',
  String status = 'Active',
  int points = 0,
}) =>
    {
      'kind': kind,
      'reportId': '11111111-1111-1111-1111-111111111111',
      'itemTypeName': 'Backpack',
      'locationName': 'Library',
      'status': status,
      'handInCode': code,
      'ownerName': 'Ama',
      'pointsEarned': points,
      'createdAt': '2026-09-26T12:00:00Z',
    };

class FakeHelp extends HelpRepository {
  FakeHelp(this.payload) : super(Dio());

  final Map<String, dynamic> payload;
  bool fail = false;

  @override
  Future<HelpToFind> getHelpToFind() async {
    if (fail) throw StateError('private transport details');
    return HelpToFind.fromJson(payload);
  }
}

Future<void> mount(WidgetTester tester, FakeHelp repository) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [helpRepositoryProvider.overrideWithValue(repository)],
    child: MaterialApp(theme: buildFoundUTheme(), home: const HelpToFindPage()),
  ));
  await tester.pumpAndSettle();
}

void main() {
  test('an offer still to walk to a desk keeps its code, a closed one does not', () {
    final open = HelpActivity.fromJson(activity());
    final done = HelpActivity.fromJson(activity(code: null, status: 'Resolved', points: 25));

    expect(open.handInCode, '483921');
    expect(open.isPost, isFalse);
    expect(done.handInCode, isNull);
    expect(done.pointsEarned, 25);
  });

  testWidgets('shows the points earned and the code to quote', (tester) async {
    await mount(
      tester,
      FakeHelp({
        'honorPoints': 25,
        'itemsReturned': 1,
        'handIns': 0,
        'openHelpOffers': 1,
        'activity': [activity(points: 25)],
      }),
    );

    expect(find.text('25'), findsWidgets);
    expect(find.text('honor points'), findsOneWidget);
    // Grouped for reading aloud at a desk.
    expect(find.text('483 921'), findsOneWidget);
    expect(find.text('+25'), findsOneWidget);
    expect(find.textContaining("Ama's report"), findsOneWidget);
  });

  testWidgets('an empty ledger explains how points are earned', (tester) async {
    await mount(
      tester,
      FakeHelp({
        'honorPoints': 0,
        'itemsReturned': 0,
        'handIns': 0,
        'openHelpOffers': 0,
        'activity': <Map<String, dynamic>>[],
      }),
    );

    expect(find.text('Nothing yet'), findsOneWidget);
    expect(find.textContaining('I found this'), findsOneWidget);
  });

  testWidgets('a failure offers a retry without showing transport details', (tester) async {
    final repository = FakeHelp(const {})..fail = true;
    await mount(tester, repository);

    expect(find.text('Could not load your finding activity'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(find.textContaining('private transport details'), findsNothing);
  });
}
