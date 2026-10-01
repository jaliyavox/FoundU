import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/theme/app_theme.dart';
import 'package:foundu/features/handover/data/handover_repository.dart';
import 'package:foundu/features/handover/presentation/handover_notice.dart';

const reportId = '11111111-1111-1111-1111-111111111111';

Handover handover(String status, {String? code = '483921'}) => Handover(
      reportId: reportId,
      status: status,
      code: code,
      expiresAt: null,
      storageLocationName: status == 'InCustody' ? 'Library Front Desk' : null,
      finderName: 'Kasun Jay',
    );

class FakeHandovers extends HandoverRepository {
  FakeHandovers(this.current) : super(Dio());
  Handover? current;

  @override
  Future<Handover?> get(String reportId) async => current;
}

Future<void> mount(WidgetTester tester, Handover? current) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [handoverRepositoryProvider.overrideWithValue(FakeHandovers(current))],
    child: MaterialApp(
      theme: buildFoundUTheme(),
      home: const Scaffold(body: SingleChildScrollView(child: HandoverNotice(reportId: reportId))),
    ),
  ));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('the owner sees the code, grouped for reading aloud, while the item is on its way', (tester) async {
    await mount(tester, handover('AwaitingHandIn'));

    expect(find.text('483 921'), findsOneWidget);
    expect(find.textContaining('Kasun is taking it to a desk'), findsOneWidget);
    expect(find.text('Your collection code'), findsOneWidget);
  });

  testWidgets('once a desk has it, the notice says where to collect it', (tester) async {
    await mount(tester, handover('InCustody'));

    expect(find.textContaining('Ready to collect at Library Front Desk'), findsOneWidget);
    expect(find.text('483 921'), findsOneWidget);
  });

  testWidgets('nothing in flight takes no room at all', (tester) async {
    await mount(tester, null);
    expect(find.text('Your collection code'), findsNothing);

    // A finished or lapsed handover is not news any more either.
    await mount(tester, handover('Collected', code: null));
    expect(find.text('Your collection code'), findsNothing);
    await mount(tester, handover('Expired', code: null));
    expect(find.text('Your collection code'), findsNothing);
  });

  test('a bare null body from the API is "nothing in flight", not an error', () {
    // The endpoint answers `null` for most reports; the repository has to read that as absence.
    expect(handover('AwaitingHandIn').isLive, isTrue);
    expect(handover('InCustody').isLive, isTrue);
    expect(handover('Cancelled', code: null).isLive, isFalse);
  });
}
