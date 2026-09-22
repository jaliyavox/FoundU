import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_config.dart';
import '../../../core/api/api_exception.dart';
import 'feed_models.dart';

/// The public lost feed, and the two things a signed-in reader can do on it.
///
/// The feed itself works without a token, but sends one when it exists so the API can mark
/// the caller's own posts. The authenticated Dio does that - and if a stale token ever comes
/// back 401 here the interceptor's refresh is harmless, because the endpoint answers anyone.
class FeedRepository {
  FeedRepository(this._dio);
  final Dio _dio;

  Future<FeedPage> getFeed({int page = 1, int pageSize = 20, String? search, String? categoryId}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/api/lost-reports/feed',
        queryParameters: {
          'page': page,
          'pageSize': pageSize,
          if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
          if (categoryId != null) 'categoryId': categoryId,
        },
      );
      return FeedPage.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  /// "I found this". Recorded once per person however often it is pressed.
  Future<FoundClaimResult> registerFoundClaim(String reportId) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>('/api/lost-reports/$reportId/found-claims');
      return FoundClaimResult.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> sendMessage(String reportId, String body) async {
    try {
      await _dio.post<Map<String, dynamic>>('/api/lost-reports/$reportId/messages', data: {'body': body});
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }
}

final feedRepositoryProvider = Provider<FeedRepository>((ref) => FeedRepository(ref.watch(apiClientProvider)));

/// Upload URLs are host-relative ("/uploads/..."), served by the API from its own wwwroot.
String resolvePhotoUrl(String path) => path.startsWith('http') ? path : '${ApiConfig.baseUrl}$path';
