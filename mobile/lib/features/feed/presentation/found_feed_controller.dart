import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/feed_models.dart';
import '../data/feed_repository.dart';

/// The found board's state - the same shape as the lost feed's, over the other endpoint.
class FoundFeedState {
  const FoundFeedState({
    this.items = const [],
    this.page = 0,
    this.hasNextPage = true,
    this.isLoading = false,
    this.error,
    this.search = '',
    this.categoryId,
  });

  final List<FoundPost> items;
  final int page;
  final bool hasNextPage;
  final bool isLoading;
  final String? error;
  final String search;
  final String? categoryId;

  bool get isEmpty => !isLoading && error == null && items.isEmpty;

  FoundFeedState copyWith({
    List<FoundPost>? items,
    int? page,
    bool? hasNextPage,
    bool? isLoading,
    String? error,
    bool clearError = false,
    String? search,
    String? categoryId,
    bool clearCategory = false,
  }) =>
      FoundFeedState(
        items: items ?? this.items,
        page: page ?? this.page,
        hasNextPage: hasNextPage ?? this.hasNextPage,
        isLoading: isLoading ?? this.isLoading,
        error: clearError ? null : (error ?? this.error),
        search: search ?? this.search,
        categoryId: clearCategory ? null : (categoryId ?? this.categoryId),
      );
}

class FoundFeedController extends Notifier<FoundFeedState> {
  static const _pageSize = 12;

  @override
  FoundFeedState build() {
    Future.microtask(refresh);
    return const FoundFeedState();
  }

  Future<void> refresh() async {
    state = state.copyWith(items: const [], page: 0, hasNextPage: true, isLoading: true, clearError: true);
    await _load(1);
  }

  Future<void> loadMore() async {
    if (state.isLoading || !state.hasNextPage) return;
    state = state.copyWith(isLoading: true);
    await _load(state.page + 1);
  }

  Future<void> setSearch(String search) async {
    if (search == state.search) return;
    state = state.copyWith(search: search);
    await refresh();
  }

  Future<void> setCategory(String? categoryId) async {
    if (categoryId == state.categoryId) return;
    state = categoryId == null ? state.copyWith(clearCategory: true) : state.copyWith(categoryId: categoryId);
    await refresh();
  }

  Future<void> _load(int page) async {
    try {
      final result = await ref.read(feedRepositoryProvider).getFoundFeed(
            page: page,
            pageSize: _pageSize,
            search: state.search,
            categoryId: state.categoryId,
          );
      state = state.copyWith(
        items: page == 1 ? result.items : [...state.items, ...result.items],
        page: result.page,
        hasNextPage: result.hasNextPage,
        isLoading: false,
        clearError: true,
      );
    } catch (error) {
      state = state.copyWith(isLoading: false, error: error.toString());
    }
  }
}

final foundFeedControllerProvider = NotifierProvider<FoundFeedController, FoundFeedState>(FoundFeedController.new);
