import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/theme/app_theme.dart';
import 'package:foundu/features/handover/data/handover_repository.dart';
import 'package:foundu/features/reports/data/report_models.dart';
import 'package:foundu/features/reports/data/report_repository.dart';
import 'package:foundu/features/reports/presentation/my_reports_page.dart';

LostReportListItemModel report(String status) => LostReportListItemModel(
      id: 'r-$status',
      categoryName: 'Bags & Wallets',
      itemTypeName: 'Backpack',
      lastSeenLocationName: 'Library',
      description: 'Black backpack with two zips and maths notes inside.',
      primaryColor: 'Black',
      estimatedLostFromAt: DateTime.now().subtract(const Duration(hours: 5)),
      estimatedLostToAt: DateTime.now().subtract(const Duration(hours: 3)),
      status: status,
      photoUrls: const [],
      messageCount: 2,
      foundClaimCount: 1,
      lastFoundClaimAt: DateTime.now().subtract(const Duration(hours: 1)),
      isFlagged: false,
      createdAt: DateTime.now().subtract(const Duration(hours: 5)),
    );

class Reports extends LostReportRepository {
  Reports() : super(dio: Dio());

  @override
  Future<PagedResult<LostReportListItemModel>> getMyReports({String? status, int page = 1, int pageSize = 20}) async =>
      PagedResult(
        items: [report('Active'), report('Matched'), report('Resolved'), report('Withdrawn')],
        totalCount: 4,
        page: 1,
        pageSize: 20,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      );
}

class NoHandovers extends HandoverRepository {
  NoHandovers() : super(Dio());

  @override
  Future<Handover?> get(String reportId) async => null;
}

/// The report card puts Details, Messages, I found this, Edit and Withdraw on one line. On
/// the smallest common phone width that has to lay out without failing or running off.
void main() {
  for (final width in [360.0, 412.0]) {
    testWidgets('report cards lay out at ${width.toInt()}dp in every status', (tester) async {
      tester.view.physicalSize = Size(width * 3, 2400 * 3 / 2.6 * 2.6);
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.reset);

      await tester.pumpWidget(ProviderScope(
        overrides: [
          reportRepositoryProvider.overrideWithValue(Reports()),
          handoverRepositoryProvider.overrideWithValue(NoHandovers()),
        ],
        child: MaterialApp(theme: buildFoundUTheme(), home: const MyReportsPage()),
      ));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.text('Backpack'), findsWidgets);
    });
  }
}
