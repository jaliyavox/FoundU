import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/feed_models.dart';
import '../data/feed_repository.dart';

/// The feed as the screen sees it: what has loaded, whether more exists, and the filters
/// that produced it. Changing a filter starts again from page one; scrolling appends.
class FeedState {
  const FeedState({
    this.items = const [],
    this.page = 0,
    this.hasNextPage = true,
    this.totalCount = 0,
    this.isLoading = false,
    this.error,
    this.search = '',
    this.categoryId,
  });

  final List<FeedItem> items;
  final int page;
  final bool hasNextPage;
  final int totalCount;
  final bool isLoading;
  final String? error;
  final String search;
  final String? categoryId;

  bool get isEmpty => !isLoading && error == null && items.isEmpty;

  FeedState copyWith({
    List<FeedItem>? items,
    int? page,
    bool? hasNextPage,
    int? totalCount,
    bool? isLoading,
    String? error,
    bool clearError = false,
    String? search,
    String? categoryId,
    bool clearCategory = false,
  }) =>
      FeedState(
        items: items ?? this.items,
        page: page ?? this.page,
        hasNextPage: hasNextPage ?? this.hasNextPage,
        totalCount: totalCount ?? this.totalCount,
        isLoading: isLoading ?? this.isLoading,
        error: clearError ? null : (error ?? this.error),
        search: search ?? this.search,
        categoryId: clearCategory ? null : (categoryId ?? this.categoryId),
      );
}

class FeedController extends Notifier<FeedState> {
  static const _pageSize = 12;

  @override
  FeedState build() {
    Future.microtask(refresh);
    return const FeedState();
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
      final result = await ref.read(feedRepositoryProvider).getFeed(
            page: page,
            pageSize: _pageSize,
            search: state.search,
            categoryId: state.categoryId,
          );
      state = state.copyWith(
        items: page == 1 ? result.items : [...state.items, ...result.items],
        page: result.page,
        hasNextPage: result.hasNextPage,
        totalCount: result.totalCount,
        isLoading: false,
        clearError: true,
      );
    } catch (error) {
      state = state.copyWith(isLoading: false, error: error.toString());
    }
  }
}

final feedControllerProvider = NotifierProvider<FeedController, FeedState>(FeedController.new);

/// "3 hours ago" - the same wording as the web feed.
String timeAgo(DateTime when, {DateTime? now}) {
  final diff = (now ?? DateTime.now()).difference(when);
  if (diff.inSeconds < 60) return 'just now';
  if (diff.inMinutes < 60) return '${diff.inMinutes} min ago';
  if (diff.inHours < 24) return '${diff.inHours} hour${diff.inHours == 1 ? '' : 's'} ago';
  if (diff.inDays < 7) return diff.inDays == 1 ? 'yesterday' : '${diff.inDays} days ago';
  final weeks = (diff.inDays / 7).floor();
  if (weeks < 5) return weeks == 1 ? 'last week' : '$weeks weeks ago';
  final months = (diff.inDays / 30).floor();
  return months <= 1 ? 'last month' : '$months months ago';
}
