import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/auth/auth_session.dart';
import '../../data/claim_models.dart';
import '../../data/claim_repository.dart';

const claimsPageSize = 20;

final myClaimsProvider = NotifierProvider<ClaimsPager, ClaimsPageState>(
  ClaimsPager.new,
);

class ClaimsPageState {
  const ClaimsPageState({
    required this.items,
    required this.page,
    required this.totalPages,
    this.isInitialLoading = false,
    this.isLoadingMore = false,
    this.initialError,
    this.loadMoreError,
  });

  const ClaimsPageState.initial()
      : items = const [],
        page = 0,
        totalPages = 0,
        isInitialLoading = true,
        isLoadingMore = false,
        initialError = null,
        loadMoreError = null;

  final List<ClaimListItem> items;
  final int page;
  final int totalPages;
  final bool isInitialLoading;
  final bool isLoadingMore;
  final Object? initialError;
  final Object? loadMoreError;

  bool get hasMore => page < totalPages;
}

class ClaimsPager extends Notifier<ClaimsPageState> {
  int _sessionVersion = 0;
  int? _loadingSessionVersion;
  int _generation = 0;

  @override
  ClaimsPageState build() {
    // A logout/login can otherwise leave this non-auto-disposed pager showing a previous
    // account's 403/error or data. The next session gets a fresh page-one request.
    ref.watch(authSessionEpochProvider);
    _sessionVersion++;
    Future.microtask(_loadFirst);
    return const ClaimsPageState.initial();
  }

  Future<void> refresh() => _loadFirst();

  Future<void> _loadFirst() async {
    final sessionVersion = _sessionVersion;
    if (_loadingSessionVersion == sessionVersion) return;
    _loadingSessionVersion = sessionVersion;
    final requestGeneration = ++_generation;
    state = const ClaimsPageState.initial();
    try {
      final page = await ref
          .read(claimRepositoryProvider)
          .getMyClaims(page: 1, pageSize: claimsPageSize);
      if (!ref.mounted ||
          requestGeneration != _generation ||
          sessionVersion != _sessionVersion) {
        return;
      }
      state = ClaimsPageState(
        items: page.items,
        page: page.page,
        totalPages: page.totalPages,
      );
    } catch (error) {
      if (!ref.mounted ||
          requestGeneration != _generation ||
          sessionVersion != _sessionVersion) {
        return;
      }
      state = ClaimsPageState(
        items: const [],
        page: 0,
        totalPages: 0,
        initialError: error,
      );
    } finally {
      if (_loadingSessionVersion == sessionVersion) {
        _loadingSessionVersion = null;
      }
    }
  }

  Future<void> loadMore() async {
    if (state.isInitialLoading || state.isLoadingMore || !state.hasMore) {
      return;
    }

    final current = state;
    final requestGeneration = _generation;
    state = ClaimsPageState(
      items: current.items,
      page: current.page,
      totalPages: current.totalPages,
      isLoadingMore: true,
    );
    try {
      final page = await ref.read(claimRepositoryProvider).getMyClaims(
            page: current.page + 1,
            pageSize: claimsPageSize,
          );
      if (!ref.mounted || requestGeneration != _generation) return;
      final existingIds = current.items.map((claim) => claim.id).toSet();
      state = ClaimsPageState(
        items: [
          ...current.items,
          ...page.items.where((claim) => existingIds.add(claim.id)),
        ],
        page: page.page,
        totalPages: page.totalPages,
      );
    } catch (error) {
      if (!ref.mounted || requestGeneration != _generation) return;
      state = ClaimsPageState(
        items: current.items,
        page: current.page,
        totalPages: current.totalPages,
        loadMoreError: error,
      );
    }
  }
}

final claimDetailProvider = FutureProvider.family<ClaimDetail, String>(
    (ref, id) => ref.watch(claimRepositoryProvider).getClaim(id));

final claimControllerProvider =
    AsyncNotifierProvider<ClaimController, void>(ClaimController.new);

class ClaimController extends AsyncNotifier<void> {
  @override
  Future<void> build() async {}

  Future<ClaimDetail> create(CreateClaimRequest request) async {
    state = const AsyncLoading();
    try {
      final claim =
          await ref.read(claimRepositoryProvider).createClaim(request);
      ref.invalidate(myClaimsProvider);
      ref.invalidate(claimDetailProvider(claim.id));
      state = const AsyncData(null);
      return claim;
    } catch (error, stack) {
      state = AsyncError(error, stack);
      rethrow;
    }
  }

  Future<ClaimDetail> submitAnswers(
      String claimId, List<ClaimAnswerInput> answers) async {
    state = const AsyncLoading();
    try {
      final claim = await ref
          .read(claimRepositoryProvider)
          .submitAnswers(claimId, answers);
      ref.invalidate(myClaimsProvider);
      ref.invalidate(claimDetailProvider(claimId));
      state = const AsyncData(null);
      return claim;
    } catch (error, stack) {
      state = AsyncError(error, stack);
      rethrow;
    }
  }

  Future<ClaimDetail> cancel(String claimId, String? reason) async {
    state = const AsyncLoading();
    try {
      final claim =
          await ref.read(claimRepositoryProvider).cancelClaim(claimId, reason);
      ref.invalidate(myClaimsProvider);
      ref.invalidate(claimDetailProvider(claimId));
      state = const AsyncData(null);
      return claim;
    } catch (error, stack) {
      state = AsyncError(error, stack);
      rethrow;
    }
  }
}
