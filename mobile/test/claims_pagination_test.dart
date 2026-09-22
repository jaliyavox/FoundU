import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/claims/data/claim_models.dart';
import 'package:foundu/features/claims/data/claim_repository.dart';
import 'package:foundu/features/claims/presentation/providers/claim_providers.dart';

void main() {
  test('loads page one and exposes more pages', () async {
    final repository = _PagingRepository({
      1: _page(['claim-1'], page: 1, totalPages: 2),
    });
    final container = _container(repository);
    addTearDown(container.dispose);

    expect(container.read(myClaimsProvider).isInitialLoading, isTrue);
    await _settle(container);

    final state = container.read(myClaimsProvider);
    expect(state.items.single.id, 'claim-1');
    expect(state.hasMore, isTrue);
    expect(repository.calls, [1]);
  });

  test('empty and initial failure states are retained safely', () async {
    final empty = _container(_PagingRepository({1: _page([], page: 1)}));
    addTearDown(empty.dispose);
    await _settle(empty);
    expect(empty.read(myClaimsProvider).items, isEmpty);
    expect(empty.read(myClaimsProvider).initialError, isNull);

    final failed = _container(_PagingRepository({}, failPages: {1}));
    addTearDown(failed.dispose);
    await _settle(failed);
    expect(failed.read(myClaimsProvider).items, isEmpty);
    expect(failed.read(myClaimsProvider).initialError, isNotNull);
  });

  test('loading another page preserves existing items and prevents duplicates',
      () async {
    final repository = _PagingRepository({
      1: _page(['claim-1'], page: 1, totalPages: 2),
      2: _page(['claim-2'], page: 2, totalPages: 2),
    });
    final container = _container(repository);
    addTearDown(container.dispose);
    await _settle(container);

    final pager = container.read(myClaimsProvider.notifier);
    await Future.wait([pager.loadMore(), pager.loadMore()]);

    expect(container.read(myClaimsProvider).items.map((item) => item.id),
        ['claim-1', 'claim-2']);
    expect(repository.calls, [1, 2]);
  });

  test('a later page failure keeps page one and refresh reloads page one',
      () async {
    final repository = _PagingRepository(
      {
        1: _page(['claim-1'], page: 1, totalPages: 2)
      },
      failPages: {2},
    );
    final container = _container(repository);
    addTearDown(container.dispose);
    await _settle(container);

    await container.read(myClaimsProvider.notifier).loadMore();
    expect(container.read(myClaimsProvider).items.single.id, 'claim-1');
    expect(container.read(myClaimsProvider).loadMoreError, isNotNull);

    await container.read(myClaimsProvider.notifier).refresh();
    expect(container.read(myClaimsProvider).items.single.id, 'claim-1');
    expect(repository.calls, [1, 2, 1]);
  });

  test('a stale load-more result cannot overwrite a refreshed first page',
      () async {
    final repository = _DeferredPagingRepository();
    final container = ProviderContainer(
      overrides: [claimRepositoryProvider.overrideWithValue(repository)],
    );
    addTearDown(container.dispose);

    container.read(myClaimsProvider);
    await _nextTurn();
    repository.initialPage
        .complete(_page(['old-claim'], page: 1, totalPages: 2));
    await _nextTurn();

    final pager = container.read(myClaimsProvider.notifier);
    final staleLoadMore = pager.loadMore();
    await _nextTurn();
    final refresh = pager.refresh();
    await _nextTurn();
    repository.refreshedPage
        .complete(_page(['fresh-claim'], page: 1, totalPages: 2));
    await refresh;

    repository.staleSecondPage
        .complete(_page(['stale-page-two'], page: 2, totalPages: 2));
    await staleLoadMore;

    expect(container.read(myClaimsProvider).items.map((item) => item.id),
        ['fresh-claim']);

    final freshLoadMore = pager.loadMore();
    await _nextTurn();
    repository.freshSecondPage
        .complete(_page(['fresh-page-two'], page: 2, totalPages: 2));
    await freshLoadMore;

    expect(container.read(myClaimsProvider).items.map((item) => item.id),
        ['fresh-claim', 'fresh-page-two']);
  });
}

ProviderContainer _container(_PagingRepository repository) => ProviderContainer(
      overrides: [claimRepositoryProvider.overrideWithValue(repository)],
    );

Future<void> _settle(ProviderContainer container) async {
  container.read(myClaimsProvider);
  await _nextTurn();
  await _nextTurn();
}

Future<void> _nextTurn() => Future<void>.delayed(Duration.zero);

PagedClaims _page(List<String> ids, {required int page, int totalPages = 1}) =>
    PagedClaims(
      items: ids.map(_claim).toList(),
      page: page,
      totalPages: totalPages,
      totalCount: ids.length,
    );

ClaimListItem _claim(String id) => ClaimListItem(
      id: id,
      status: 'Pending',
      categoryName: 'Bags',
      itemTypeName: 'Backpack',
      unansweredQuestionCount: 0,
      createdAt: DateTime.utc(2026),
      updatedAt: DateTime.utc(2026),
    );

class _PagingRepository extends ClaimRepository {
  _PagingRepository(this.pages, {this.failPages = const {}}) : super(Dio());

  final Map<int, PagedClaims> pages;
  final Set<int> failPages;
  final List<int> calls = [];

  @override
  Future<PagedClaims> getMyClaims({int page = 1, int pageSize = 20}) async {
    calls.add(page);
    if (failPages.contains(page)) throw StateError('network unavailable');
    return pages[page]!;
  }
}

class _DeferredPagingRepository extends ClaimRepository {
  _DeferredPagingRepository() : super(Dio());

  final initialPage = Completer<PagedClaims>();
  final staleSecondPage = Completer<PagedClaims>();
  final refreshedPage = Completer<PagedClaims>();
  final freshSecondPage = Completer<PagedClaims>();
  var _firstPageCalls = 0;
  var _secondPageCalls = 0;

  @override
  Future<PagedClaims> getMyClaims({int page = 1, int pageSize = 20}) {
    if (page == 1) {
      _firstPageCalls++;
      return _firstPageCalls == 1 ? initialPage.future : refreshedPage.future;
    }
    _secondPageCalls++;
    return _secondPageCalls == 1
        ? staleSecondPage.future
        : freshSecondPage.future;
  }
}
