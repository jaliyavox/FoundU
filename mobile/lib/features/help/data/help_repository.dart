import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';
import 'help_models.dart';

class HelpRepository {
  HelpRepository(this._dio);

  final Dio _dio;

  Future<HelpToFind> getHelpToFind() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/help-to-find');
      return HelpToFind.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }
}

final helpRepositoryProvider = Provider<HelpRepository>((ref) => HelpRepository(ref.watch(apiClientProvider)));

/// Refetched whenever the screen is opened - points change when other people act.
final helpToFindProvider = FutureProvider.autoDispose<HelpToFind>(
  (ref) => ref.watch(helpRepositoryProvider).getHelpToFind(),
);
