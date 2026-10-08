import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/theme/app_theme.dart';
import 'package:foundu/features/feed/data/feed_models.dart';
import 'package:foundu/features/feed/data/feed_repository.dart';
import 'package:foundu/features/handover/data/handover_repository.dart';
import 'package:foundu/features/reports/data/report_models.dart';
import 'package:foundu/features/reports/presentation/providers/report_providers.dart';
import 'package:foundu/features/reports/presentation/report_detail_page.dart';

LostReportDetailModel detail(String status) => LostReportDetailModel(
      id: 'r1',
      categoryId: 'c1',
      categoryName: 'Electronics',
      itemTypeId: 't1',
      itemTypeName: 'Headphones',
      lastSeenLocationId: 'l1',
      lastSeenLocationName: 'Main Auditorium',
      description: 'Black over-ear headphones in a hard case.',
      primaryColor: 'Black',
      estimatedLostFromAt: DateTime.now().subtract(const Duration(hours: 14)),
      estimatedLostToAt: DateTime.now().subtract(const Duration(hours: 12)),
      status: status,
      studentId: 'u1',
      studentName: 'Amara Perera',
      photos: const [],
      isFlagged: false,
      createdAt: DateTime.now().subtract(const Duration(hours: 12)),
      updatedAt: DateTime.now().subtract(const Duration(hours: 12)),
    );

class NoHandovers extends HandoverRepository {
  NoHandovers() : super(Dio());
  @override
  Future<Handover?> get(String reportId) async => null;
}

class NoMessages extends FeedRepository {
  NoMessages() : super(Dio());
  @override
  Future<List<ReportMessage>> getMessages(String reportId) async => const [];
}

/// The action bar at the foot of a report: Edit, Withdraw and Possible matches. It has to
/// fit a small phone without a label wrapping or running off its button.
void main() {
  for (final width in [360.0, 412.0]) {
    testWidgets('the action bar fits at ${width.toInt()}dp', (tester) async {
      tester.view.physicalSize = Size(width * 3, 2400);
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.reset);

      await tester.pumpWidget(ProviderScope(
        overrides: [
          reportDetailProvider('r1').overrideWith((ref) async => detail('Active')),
          handoverRepositoryProvider.overrideWithValue(NoHandovers()),
          feedRepositoryProvider.overrideWithValue(NoMessages()),
        ],
        child: MaterialApp(theme: buildFoundUTheme(), home: const LostReportDetailPage(reportId: 'r1')),
      ));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.text('Edit'), findsOneWidget);
      expect(find.text('Withdraw'), findsOneWidget);
      expect(find.text('Possible matches'), findsOneWidget);
    });
  }
}
