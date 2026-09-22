import 'package:dio/dio.dart';

class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode, this.fieldErrors = const {}});

  final String message;
  final int? statusCode;

  /// Validation messages keyed by the C# property name (`Email`, `Password`) - see
  /// /docs/api/conventions.md "Validation". Empty for anything other than a 400.
  final Map<String, List<String>> fieldErrors;

  List<String> errorsFor(String field) => fieldErrors[field] ?? const [];

  factory ApiException.fromDio(DioException error) {
    final data = error.response?.data;
    if (data is Map) {
      final detail = data['detail'];
      if (detail is String && detail.trim().isNotEmpty) {
        return ApiException(detail, statusCode: error.response?.statusCode);
      }

      final errors = data['errors'];
      if (errors is Map) {
        final messages = errors.values
            .expand((value) => value is List ? value : [value])
            .whereType<String>()
            .where((value) => value.trim().isNotEmpty)
            .toList();
        if (messages.isNotEmpty) {
          final byField = <String, List<String>>{
            for (final entry in errors.entries)
              entry.key.toString(): [
                for (final m in (entry.value is List ? entry.value as List : [entry.value]))
                  if (m is String && m.trim().isNotEmpty) m,
              ],
          };
          return ApiException(
            messages.join('\n'),
            statusCode: error.response?.statusCode,
            fieldErrors: byField,
          );
        }
      }

      final title = data['title'];
      if (title is String && title.trim().isNotEmpty) {
        return ApiException(title, statusCode: error.response?.statusCode);
      }
    }

    if (error.type == DioExceptionType.connectionError ||
        error.type == DioExceptionType.connectionTimeout ||
        error.type == DioExceptionType.receiveTimeout ||
        error.type == DioExceptionType.sendTimeout) {
      return const ApiException(
        'Could not connect to FoundU. Check the API and your network.',
      );
    }

    return ApiException(
      'Something went wrong. Please try again.',
      statusCode: error.response?.statusCode,
    );
  }

  @override
  String toString() => message;
}
