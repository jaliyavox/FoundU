import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/claims/data/claim_models.dart';
import 'package:foundu/features/claims/data/claim_repository.dart';

void main() {
  test('Claims repository uses the paged mine endpoint', () async {
    final dio = Dio()
      ..httpClientAdapter = _CallbackAdapter((options) {
        expect(options.method, 'GET');
        expect(options.path, '/api/claims/mine');
        expect(options.queryParameters, {'page': 2, 'pageSize': 20});
        return _jsonResponse(200, {
          'items': [],
          'page': 2,
          'totalPages': 3,
          'totalCount': 41,
        });
      });

    final result = await ClaimRepository(dio).getMyClaims(page: 2);
    expect(result.totalPages, 3);
  });

  test('Claims repository serializes create, answer, and cancel requests',
      () async {
    final calls = <RequestOptions>[];
    final dio = Dio()
      ..httpClientAdapter = _CallbackAdapter((options) {
        calls.add(options);
        return _jsonResponse(200, _detailJson());
      });
    final repository = ClaimRepository(dio);

    await repository.createClaim(
      const CreateClaimRequest(
          lostReportId: 'lost-1', foundReportId: 'found-1'),
    );
    await repository.submitAnswers('claim-1', const [
      ClaimAnswerInput(questionId: 'question-1', answerText: 'blue tag'),
    ]);
    await repository.cancelClaim('claim-1', 'No longer needed');

    expect(calls[0].path, '/api/claims');
    expect(
        calls[0].data, {'lostReportId': 'lost-1', 'foundReportId': 'found-1'});
    expect(calls[1].path, '/api/claims/claim-1/answers');
    expect(calls[1].data, {
      'answers': [
        {'questionId': 'question-1', 'answerText': 'blue tag'}
      ]
    });
    expect(calls[2].path, '/api/claims/claim-1/cancel');
    expect(calls[2].data, {'reason': 'No longer needed'});
  });
}

Map<String, dynamic> _detailJson() => {
      'id': 'claim-1',
      'status': 'Pending',
      'lostReportId': 'lost-1',
      'lostReportDescription': 'Lost backpack',
      'foundItem': {
        'id': 'found-1',
        'categoryName': 'Bags',
        'itemTypeName': 'Backpack',
        'foundLocationName': 'Library',
        'generalDescription': 'Blue backpack',
        'foundAt': '2026-01-01T00:00:00Z',
        'status': 'Unclaimed',
      },
      'questions': [],
      'createdAt': '2026-01-01T00:00:00Z',
      'updatedAt': '2026-01-01T00:00:00Z',
    };

ResponseBody _jsonResponse(int statusCode, Object body) =>
    ResponseBody.fromString(
      jsonEncode(body),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

class _CallbackAdapter implements HttpClientAdapter {
  _CallbackAdapter(this.callback);

  final ResponseBody Function(RequestOptions options) callback;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async =>
      callback(options);

  @override
  void close({bool force = false}) {}
}
