import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/theme/app_theme.dart';
import 'package:foundu/features/intake/data/intake_repository.dart';
import 'package:foundu/features/reference/data/reference_models.dart';
import 'package:foundu/features/reports/presentation/providers/report_providers.dart';
import 'package:foundu/features/reports/presentation/report_form_page.dart';

const categories = [
  CategoryModel(
    id: 'c-bags',
    name: 'Bags & Wallets',
    description: null,
    isHighlighted: false,
    itemTypes: [
      ItemTypeModel(id: 't-backpack', categoryId: 'c-bags', name: 'Backpack'),
      ItemTypeModel(id: 't-wallet', categoryId: 'c-bags', name: 'Wallet'),
    ],
  ),
];

const locations = [
  CampusLocationModel(id: 'l-library', name: 'Library', building: null, description: null),
];

Future<void> mount(WidgetTester tester, {IntakeDraft? draft}) async {
  // A phone-sized screen, so layout problems that only show on a real device show here too.
  tester.view.physicalSize = const Size(1080, 2400);
  tester.view.devicePixelRatio = 2.6;
  addTearDown(tester.view.reset);

  await tester.pumpWidget(ProviderScope(
    overrides: [
      categoriesProvider.overrideWith((ref) async => categories),
      campusLocationsProvider.overrideWith((ref) async => locations),
    ],
    child: MaterialApp(theme: buildFoundUTheme(), home: ReportFormPage(draft: draft)),
  ));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('the blank form draws its sections', (tester) async {
    await mount(tester);

    expect(tester.takeException(), isNull);
    expect(find.text('1. What did you lose?'), findsOneWidget);
  });

  testWidgets('a draft from Ask FoundU opens prefilled', (tester) async {
    await mount(
      tester,
      draft: const IntakeDraft(
        categoryId: 'c-bags',
        itemTypeId: 't-backpack',
        locationId: 'l-library',
        description: 'I lost my black backpack. Last seen at Library.',
        primaryColor: 'black',
      ),
    );

    expect(tester.takeException(), isNull);
    expect(find.text('I lost my black backpack. Last seen at Library.'), findsOneWidget);
  });
}
