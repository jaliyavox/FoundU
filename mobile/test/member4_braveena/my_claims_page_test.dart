import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/claims/data/claim_models.dart';
import 'package:foundu/features/claims/data/claim_repository.dart';
import 'package:foundu/features/claims/presentation/my_claims_page.dart';

void main() {
  testWidgets('shows loaded claims and appends the next page', (tester) async {
    final repository = _ListRepository({
      1: _page(['claim-1'], page: 1, totalPages: 2),
      2: _page(['claim-2'], page: 2, totalPages: 2),
    });

    await tester.pumpWidget(ProviderScope(
      overrides: [claimRepositoryProvider.overrideWithValue(repository)],
      child: const MaterialApp(home: MyClaimsPage()),
    ));
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    await tester.pumpAndSettle();

    expect(find.text('Backpack claim-1'), findsOneWidget);
    expect(find.text('Load more'), findsOneWidget);
    await tester.tap(find.text('Load more'));
    await tester.pumpAndSettle();
    expect(find.text('Backpack claim-1'), findsOneWidget);
    expect(find.text('Backpack claim-2'), findsOneWidget);
    expect(find.text('Load more'), findsNothing);
  });

  testWidgets('shows empty and safe initial error states', (tester) async {
    await tester.pumpWidget(ProviderScope(
      key: const ValueKey('empty'),
      overrides: [
        claimRepositoryProvider.overrideWithValue(
          _ListRepository({1: _page([], page: 1)}),
        ),
      ],
      child: const MaterialApp(home: MyClaimsPage()),
    ));
    await tester.pumpAndSettle();
    expect(find.text('No claims yet'), findsOneWidget);

    await tester.pumpWidget(ProviderScope(
      key: const ValueKey('error'),
      overrides: [
        claimRepositoryProvider.overrideWithValue(
          _ListRepository({}, fail: true),
        ),
      ],
      child: const MaterialApp(home: MyClaimsPage()),
    ));
    await tester.pumpAndSettle();
    expect(find.text('Could not load claims'), findsOneWidget);
  });
}

PagedClaims _page(List<String> ids, {required int page, int totalPages = 1}) =>
    PagedClaims(
      items: ids
          .map(
            (id) => ClaimListItem(
              id: id,
              status: 'Pending',
              categoryName: 'Bags',
              itemTypeName: 'Backpack $id',
              unansweredQuestionCount: 0,
              createdAt: DateTime.utc(2026),
              updatedAt: DateTime.utc(2026),
            ),
          )
          .toList(),
      page: page,
      totalPages: totalPages,
      totalCount: ids.length,
    );

class _ListRepository extends ClaimRepository {
  _ListRepository(this.pages, {this.fail = false}) : super(Dio());

  final Map<int, PagedClaims> pages;
  final bool fail;

  @override
  Future<PagedClaims> getMyClaims({int page = 1, int pageSize = 20}) async {
    if (fail) throw StateError('network unavailable');
    return pages[page]!;
  }
}
