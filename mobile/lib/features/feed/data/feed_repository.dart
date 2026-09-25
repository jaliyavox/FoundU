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

  /* ------------------------------------------------------------ found posts */

  Future<FoundPostPage> getFoundFeed({int page = 1, int pageSize = 20, String? search, String? categoryId}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/api/found-posts/feed',
        queryParameters: {
          'page': page,
          'pageSize': pageSize,
          if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
          if (categoryId != null) 'categoryId': categoryId,
        },
      );
      return FoundPostPage.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<FoundPost> postFound({
    required String categoryId,
    required String itemTypeId,
    required String foundLocationId,
    required String description,
    String? primaryColor,
    required DateTime foundAt,
    String? lostReportHandInCode,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>('/api/found-posts', data: {
        'categoryId': categoryId,
        'itemTypeId': itemTypeId,
        'foundLocationId': foundLocationId,
        'description': description,
        if (primaryColor != null && primaryColor.trim().isNotEmpty) 'primaryColor': primaryColor.trim(),
        'foundAt': foundAt.toUtc().toIso8601String(),
        if (lostReportHandInCode != null && lostReportHandInCode.isNotEmpty) 'lostReportHandInCode': lostReportHandInCode,
      });
      return FoundPost.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  /// "That is mine" - names one of the caller's own reports. The finder is told to hand it in.
  Future<FoundPost> recogniseFoundPost(String postId, String lostReportId) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>('/api/found-posts/$postId/recognise', data: {'lostReportId': lostReportId});
      return FoundPost.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<FoundPost> withdrawFoundPost(String postId) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>('/api/found-posts/$postId/withdraw', data: const {});
      return FoundPost.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  /// A finder leaves [recipientId] empty; the author names the finder they reply to.
  Future<void> sendMessage(String reportId, String body, {String? recipientId}) async {
    try {
      await _dio.post<Map<String, dynamic>>('/api/lost-reports/$reportId/messages', data: {
        'body': body,
        if (recipientId != null) 'recipientId': recipientId,
      });
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  /// The reader's threads on a report. A finder who has not written yet is 403 - an empty
  /// thread, not a failure, so it comes back as an empty list.
  Future<List<ReportMessage>> getMessages(String reportId) async {
    try {
      final response = await _dio.get<List<dynamic>>('/api/lost-reports/$reportId/messages');
      return (response.data ?? const []).map((e) => ReportMessage.fromJson(e as Map<String, dynamic>)).toList();
    } on DioException catch (error) {
      if (error.response?.statusCode == 403) return const [];
      throw ApiException.fromDio(error);
    }
  }
}

final feedRepositoryProvider = Provider<FeedRepository>((ref) => FeedRepository(ref.watch(apiClientProvider)));

/// Upload URLs are host-relative ("/uploads/..."), served by the API from its own wwwroot.
String resolvePhotoUrl(String path) => path.startsWith('http') ? path : '${ApiConfig.baseUrl}$path';
