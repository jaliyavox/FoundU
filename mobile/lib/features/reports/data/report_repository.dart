import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';
import 'report_models.dart';

final reportRepositoryProvider = Provider<LostReportRepository>((ref) {
  return LostReportRepository(dio: ref.watch(apiClientProvider));
});

class LostReportRepository {
  final Dio _dio;

  LostReportRepository({required Dio dio}) : _dio = dio;

  Future<PagedResult<LostReportListItemModel>> getMyReports({
    String? status,
    int page = 1,
    int pageSize = 20,
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'page': page,
        'pageSize': pageSize,
      };
      if (status != null && status.isNotEmpty && status != 'All') {
        queryParams['status'] = status;
      }

      final response = await _dio.get<Map<String, dynamic>>(
        '/api/lost-reports/my-reports',
        queryParameters: queryParams,
      );

      return PagedResult.fromJson(
        response.data!,
        (json) => LostReportListItemModel.fromJson(json),
      );
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  Future<LostReportDetailModel> getReportById(String id) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/api/lost-reports/$id',
      );
      return LostReportDetailModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  Future<LostReportDetailModel> createReport(
      CreateLostReportRequest request) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/lost-reports',
        data: request.toJson(),
      );
      return LostReportDetailModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  Future<LostReportDetailModel> updateReport(
    String id,
    UpdateLostReportRequest request,
  ) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(
        '/api/lost-reports/$id',
        data: request.toJson(),
      );
      return LostReportDetailModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  Future<List<LostReportPhotoModel>> uploadPhotos(
    String reportId,
    List<XFile> images,
  ) async {
    if (images.isEmpty) return [];
    try {
      final formData = FormData();
      for (final image in images) {
        final bytes = await image.readAsBytes();
        formData.files.add(
          MapEntry(
            'photos',
            MultipartFile.fromBytes(
              bytes,
              filename: image.name.isNotEmpty ? image.name : 'photo.jpg',
            ),
          ),
        );
      }

      final response = await _dio.post<List<dynamic>>(
        '/api/lost-reports/$reportId/photos',
        data: formData,
      );

      final list = response.data ?? [];
      return list
          .map((e) => LostReportPhotoModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  Future<LostReportDetailModel> withdrawReport(
    String id,
    String? reason,
  ) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/lost-reports/$id/withdraw',
        data: {'reason': reason},
      );
      return LostReportDetailModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  Future<List<MatchSuggestionModel>> getPossibleMatches(String reportId) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        '/api/lost-reports/$reportId/possible-matches',
      );
      final list = response.data ?? [];
      return list
          .map((e) => MatchSuggestionModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }
}
