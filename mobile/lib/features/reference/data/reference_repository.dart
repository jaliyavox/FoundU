import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';
import 'reference_models.dart';

final referenceRepositoryProvider = Provider<ReferenceRepository>((ref) {
  return ReferenceRepository(dio: ref.watch(apiClientProvider));
});

class ReferenceRepository {
  final Dio _dio;

  ReferenceRepository({required Dio dio}) : _dio = dio;

  Future<List<CategoryModel>> getCategories() async {
    try {
      final response = await _dio.get<List<dynamic>>('/api/reference/categories');
      final list = response.data ?? [];
      return list
          .map((e) => CategoryModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  Future<List<CampusLocationModel>> getLocations() async {
    try {
      final response = await _dio.get<List<dynamic>>('/api/reference/locations');
      final list = response.data ?? [];
      return list
          .map((e) => CampusLocationModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }
}
