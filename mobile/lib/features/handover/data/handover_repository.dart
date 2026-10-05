import '../../../core/auth/auth_session.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';

/// Mirrors FoundU.Application.Handovers.Dtos.HandoverDto.
///
/// [code] is only ever filled for the finder who started the handover and for the report's
/// owner - the API leaves it out for everyone else.
class Handover {
  const Handover({
    required this.reportId,
    required this.status,
    required this.code,
    required this.expiresAt,
    required this.storageLocationName,
    required this.finderName,
  });

  final String reportId;

  /// Declared, AwaitingHandIn, InCustody, Collected, Expired or Cancelled.
  final String status;
  final String? code;
  final DateTime? expiresAt;
  final String? storageLocationName;
  final String? finderName;

  bool get isWalking => status == 'AwaitingHandIn';
  bool get isAtDesk => status == 'InCustody';
  bool get isLive => isWalking || isAtDesk;

  factory Handover.fromJson(Map<String, dynamic> json) => Handover(
        reportId: json['reportId'] as String,
        status: json['status'] as String,
        code: json['code'] as String?,
        expiresAt: json['expiresAt'] == null
            ? null
            : DateTime.parse(json['expiresAt'] as String),
        storageLocationName: json['storageLocationName'] as String?,
        finderName: json['finderName'] as String?,
      );
}

class HandoverRepository {
  HandoverRepository(this._dio);

  final Dio _dio;

  /// "I will take it to security." Pressing it twice returns the same handover.
  Future<Handover> start(String reportId) async {
    try {
      final response = await _dio
          .post<Map<String, dynamic>>('/api/lost-reports/$reportId/handover');
      return Handover.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<Handover> cancel(String reportId) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
          '/api/lost-reports/$reportId/handover/cancel');
      return Handover.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  /// Null when nothing is in flight for this reader - the common case. The API answers with
  /// a bare `null` body then, which Dio hands back as null data.
  Future<Handover?> get(String reportId) async {
    try {
      final response =
          await _dio.get<dynamic>('/api/lost-reports/$reportId/handover');
      final data = response.data;
      return data is Map<String, dynamic> ? Handover.fromJson(data) : null;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }
}

final handoverRepositoryProvider = Provider<HandoverRepository>(
    (ref) { ref.watch(authSessionEpochProvider); return HandoverRepository(ref.watch(apiClientProvider)); });

/// One report's live handover, for whichever of the two people is looking.
final handoverProvider = FutureProvider.autoDispose.family<Handover?, String>(
  (ref, reportId) => ref.watch(handoverRepositoryProvider).get(reportId),
);
