import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/api/api_exception.dart';

DioException _response(int status, Map<String, dynamic> body) {
  final options = RequestOptions(path: '/api/lost-reports');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response(requestOptions: options, statusCode: status, data: body),
  );
}

void main() {
  // Found by the Assignment 2 API-integration run (MOB-INT-03): the API's validation 400 also
  // carries a generic "detail", which used to win, so the form never got its field messages.
  test('a validation 400 keeps its field errors, not the generic detail', () {
    final error = ApiException.fromDio(_response(400, {
      'title': 'Validation failed',
      'status': 400,
      'detail': 'One or more validation errors occurred.',
      'errors': {
        'Description': [
          'Describe the item in at least 10 characters so it can be matched.'
        ],
        'CategoryId': ["'Category Id' must not be empty."],
      },
    }));

    expect(error.statusCode, 400);
    expect(error.errorsFor('Description'),
        ['Describe the item in at least 10 characters so it can be matched.']);
    expect(error.fieldErrors.keys, containsAll(['Description', 'CategoryId']));
    expect(error.message, contains('at least 10 characters'));
    expect(error.message, isNot(contains('One or more validation errors')));
  });

  test('a problem with a detail and no field errors still shows the detail',
      () {
    final error = ApiException.fromDio(_response(409, {
      'title': 'Conflict',
      'status': 409,
      'detail': 'Someone else changed this claim. Reload and try again.',
    }));

    expect(error.message,
        'Someone else changed this claim. Reload and try again.');
    expect(error.fieldErrors, isEmpty);
  });
}
