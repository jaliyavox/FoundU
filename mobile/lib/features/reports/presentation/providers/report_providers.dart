import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../../../core/auth/auth_session.dart';
import '../../../reference/data/reference_models.dart';
import '../../../reference/data/reference_repository.dart';
import '../../data/report_models.dart';
import '../../data/report_repository.dart';

final categoriesProvider = FutureProvider<List<CategoryModel>>((ref) async {
  final repo = ref.watch(referenceRepositoryProvider);
  return repo.getCategories();
});

final campusLocationsProvider = FutureProvider<List<CampusLocationModel>>((ref) async {
  final repo = ref.watch(referenceRepositoryProvider);
  return repo.getLocations();
});

class StatusFilterNotifier extends Notifier<String> {
  @override
  String build() {
    ref.watch(authSessionEpochProvider);
    return 'All';
  }

  void setStatus(String status) => state = status;
}

final selectedStatusFilterProvider = NotifierProvider<StatusFilterNotifier, String>(
  StatusFilterNotifier.new,
);

final myReportsProvider = FutureProvider.autoDispose<PagedResult<LostReportListItemModel>>((ref) async {
  ref.watch(authSessionEpochProvider);
  final timer = Timer(const Duration(seconds: 30), ref.invalidateSelf);
  ref.onDispose(timer.cancel);
  final status = ref.watch(selectedStatusFilterProvider);
  final repo = ref.watch(reportRepositoryProvider);
  return repo.getMyReports(status: status);
});

final reportDetailProvider = FutureProvider.autoDispose.family<LostReportDetailModel, String>((ref, id) async {
  final timer = Timer(const Duration(seconds: 30), ref.invalidateSelf);
  ref.onDispose(timer.cancel);
  final repo = ref.watch(reportRepositoryProvider);
  return repo.getReportById(id);
});

final possibleMatchesProvider = FutureProvider.autoDispose.family<List<MatchSuggestionModel>, String>((ref, reportId) async {
  final timer = Timer(const Duration(seconds: 30), ref.invalidateSelf);
  ref.onDispose(timer.cancel);
  final repo = ref.watch(reportRepositoryProvider);
  return repo.getPossibleMatches(reportId);
});

final foundItemMatchesProvider = FutureProvider.autoDispose.family<List<MatchSuggestionModel>, String>((ref, foundItemId) async {
  ref.watch(authSessionEpochProvider);
  final timer = Timer(const Duration(seconds: 30), ref.invalidateSelf);
  ref.onDispose(timer.cancel);
  return ref.watch(reportRepositoryProvider).getMatchesForFoundItem(foundItemId);
});

final reportControllerProvider = AsyncNotifierProvider<ReportControllerNotifier, void>(
  ReportControllerNotifier.new,
);

class ReportControllerNotifier extends AsyncNotifier<void> {
  @override
  Future<void> build() async { ref.watch(authSessionEpochProvider); }

  /// Set when the last [createReport] saved the report but not its photos. The report exists,
  /// so the form must not offer to submit it again - that is how duplicates were made.
  bool photoUploadFailed = false;

  Future<LostReportDetailModel?> createReport({
    required CreateLostReportRequest request,
    List<XFile> images = const [],
  }) async {
    state = const AsyncValue.loading();
    photoUploadFailed = false;
    try {
      final repo = ref.read(reportRepositoryProvider);
      final created = await repo.createReport(request);
      if (images.isNotEmpty) {
        try {
          await repo.uploadPhotos(created.id, images);
        } on Object {
          photoUploadFailed = true;
        }
      }
      ref.invalidate(myReportsProvider);
      state = const AsyncValue.data(null);
      return created;
    } catch (e, st) {
      state = AsyncValue.error(e, st);
      rethrow;
    }
  }

  Future<LostReportDetailModel?> updateReport({
    required String reportId,
    required UpdateLostReportRequest request,
    List<XFile> newImages = const [],
  }) async {
    state = const AsyncValue.loading();
    try {
      final repo = ref.read(reportRepositoryProvider);
      final updated = await repo.updateReport(reportId, request);
      if (newImages.isNotEmpty) {
        await repo.uploadPhotos(reportId, newImages);
      }
      ref.invalidate(myReportsProvider);
      ref.invalidate(reportDetailProvider(reportId));
      state = const AsyncValue.data(null);
      return updated;
    } catch (e, st) {
      state = AsyncValue.error(e, st);
      rethrow;
    }
  }

  /// "I found this": the owner has it back, so the report closes and everyone who offered
  /// to help is thanked and credited.
  Future<void> resolveReport({
    required String reportId,
    String? note,
  }) async {
    state = const AsyncValue.loading();
    try {
      final repo = ref.read(reportRepositoryProvider);
      await repo.resolveReport(reportId, note);
      ref.invalidate(myReportsProvider);
      ref.invalidate(reportDetailProvider(reportId));
      state = const AsyncValue.data(null);
    } catch (e, st) {
      state = AsyncValue.error(e, st);
      rethrow;
    }
  }

  Future<void> withdrawReport({
    required String reportId,
    String? reason,
  }) async {
    state = const AsyncValue.loading();
    try {
      final repo = ref.read(reportRepositoryProvider);
      await repo.withdrawReport(reportId, reason);
      ref.invalidate(myReportsProvider);
      ref.invalidate(reportDetailProvider(reportId));
      state = const AsyncValue.data(null);
    } catch (e, st) {
      state = AsyncValue.error(e, st);
      rethrow;
    }
  }
}
