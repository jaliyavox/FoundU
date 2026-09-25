import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';
import 'claim_models.dart';

final claimRepositoryProvider = Provider<ClaimRepository>(
    (ref) => ClaimRepository(ref.watch(apiClientProvider)));

class ClaimRepository {
  ClaimRepository(this._dio);
  final Dio _dio;

  Future<PagedClaims> getMyClaims({int page = 1, int pageSize = 20}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/claims/mine',
          queryParameters: {'page': page, 'pageSize': pageSize});
      return PagedClaims.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<ClaimDetail> getClaim(String id) async {
    try {
      return ClaimDetail.fromJson(
          (await _dio.get<Map<String, dynamic>>('/api/claims/$id')).data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<ClaimDetail> createClaim(CreateClaimRequest request) async {
    try {
      return ClaimDetail.fromJson((await _dio.post<Map<String, dynamic>>(
              '/api/claims',
              data: request.toJson()))
          .data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<ClaimDetail> submitAnswers(
      String claimId, List<ClaimAnswerInput> answers) async {
    try {
      return ClaimDetail.fromJson((await _dio.post<Map<String, dynamic>>(
              '/api/claims/$claimId/answers',
              data: {
            'answers': answers.map((answer) => answer.toJson()).toList()
          }))
          .data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<ClaimDetail> cancelClaim(String claimId, String? reason) async {
    try {
      return ClaimDetail.fromJson((await _dio.post<Map<String, dynamic>>(
              '/api/claims/$claimId/cancel',
              data: {'reason': reason}))
          .data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }
}
