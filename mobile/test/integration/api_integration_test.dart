// Mobile API-integration and cross-platform workflow tests (SE3090 Assignment 2).
//
// These use the app's real repositories and Dio over real HTTP against a running API, so they
// prove the Flutter client and the ASP.NET API agree on every request and response shape.
// The staff side of the workflow is driven through the API exactly as the web desk drives it.
//
// Skipped unless an API is given:
//   flutter test test/integration --dart-define=FOUNDU_API_URL=http://localhost:5292
import 'dart:math';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/api/api_exception.dart';
import 'package:foundu/core/auth/token_storage.dart';
import 'package:foundu/features/auth/data/auth_repository.dart';
import 'package:foundu/features/claims/data/claim_models.dart';
import 'package:foundu/features/claims/data/claim_repository.dart';
import 'package:foundu/features/reports/data/report_models.dart';
import 'package:foundu/features/reports/data/report_repository.dart';

const api = String.fromEnvironment('FOUNDU_API_URL');
const demoPassword = 'Demo!Pass2026';

class _MemoryTokens implements TokenStorage {
  String? access;
  String? refresh;
  @override
  Future<void> clear() async => access = refresh = null;
  @override
  Future<String?> readAccessToken() async => access;
  @override
  Future<String?> readRefreshToken() async => refresh;
  @override
  Future<void> save(AuthTokens tokens) async {
    access = tokens.accessToken;
    refresh = tokens.refreshToken;
  }
}

/// The app's two Dio clients: one anonymous for auth, one that attaches the stored token.
({AuthRepository auth, Dio dio, _MemoryTokens tokens}) _client() {
  final tokens = _MemoryTokens();
  final authDio = Dio(BaseOptions(baseUrl: api));
  final dio = Dio(BaseOptions(baseUrl: api))
    ..interceptors.add(InterceptorsWrapper(onRequest: (options, handler) async {
      final token = await tokens.readAccessToken();
      if (token != null) options.headers['Authorization'] = 'Bearer $token';
      handler.next(options);
    }));
  return (
    auth: AuthRepository(
        authDio: authDio, authenticatedDio: dio, tokenStorage: tokens),
    dio: dio,
    tokens: tokens
  );
}

/// Staff actions, as the web desk performs them (plain HTTP to the same API).
Future<Dio> _staff() async {
  final dio = Dio(BaseOptions(baseUrl: api));
  final login = await dio.post<Map<String, dynamic>>('/api/auth/login',
      data: {'email': 'priya@foundu.test', 'password': demoPassword});
  dio.options.headers['Authorization'] = 'Bearer ${login.data!['accessToken']}';
  return dio;
}

String _tag() => Random().nextInt(0xFFFFFF).toRadixString(16).padLeft(6, '0');

void main() {
  final skip = api.isEmpty
      ? 'set --dart-define=FOUNDU_API_URL to run against a live API'
      : null;

  group('Mobile <-> API integration', skip: skip, () {
    test(
        'MOB-INT-01 a student registers from the app and is signed in as a Student',
        () async {
      final c = _client();
      final tag = _tag();
      final user = await c.auth.register(
          fullName: 'Mobile Student $tag',
          email: 'mobile-$tag@foundu.test',
          password: 'Mobile!Pass$tag');
      expect(user.role, 'Student');
      expect(c.tokens.access, isNotNull);
      expect(c.tokens.refresh, isNotNull);
    });

    test(
        'MOB-INT-02 a wrong password surfaces as a 401 ApiException, not a crash',
        () async {
      final c = _client();
      await expectLater(
        c.auth.login(email: 'amara@foundu.test', password: 'Wrong-pass1'),
        throwsA(
            isA<ApiException>().having((e) => e.statusCode, 'statusCode', 401)),
      );
      expect(c.tokens.access, isNull);
    });

    test(
        'MOB-INT-03 an invalid report comes back as field errors the form can show',
        () async {
      final c = _client();
      final tag = _tag();
      await c.auth.register(
          fullName: 'Mobile $tag',
          email: 'mobile-v-$tag@foundu.test',
          password: 'Mobile!Pass$tag');
      final reports = LostReportRepository(dio: c.dio);
      await expectLater(
        reports.createReport(CreateLostReportRequest(
          categoryId: '00000000-0000-0000-0000-000000000000',
          itemTypeId: '00000000-0000-0000-0000-000000000000',
          lastSeenLocationId: '00000000-0000-0000-0000-000000000000',
          description: 'short', // under the 10-character minimum
          estimatedLostFromAt:
              DateTime.now().subtract(const Duration(hours: 2)),
          estimatedLostToAt: DateTime.now().subtract(const Duration(hours: 1)),
        )),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 400)
            .having(
                (e) => e.fieldErrors.keys, 'fields', contains('Description'))),
      );
    });

    test(
        'MOB-INT-04 cross-platform: report on mobile, match and question at the desk, claim and answer on mobile',
        () async {
      final c = _client();
      final staff = await _staff();
      final tag = _tag();
      await c.auth.register(
          fullName: 'Mobile Owner $tag',
          email: 'mobile-o-$tag@foundu.test',
          password: 'Mobile!Pass$tag');

      final categories =
          (await staff.get<List<dynamic>>('/api/reference/categories')).data!;
      final category =
          categories.firstWhere((c) => (c['itemTypes'] as List).isNotEmpty);
      final itemTypeId = (category['itemTypes'] as List).first['id'] as String;
      final locationId =
          (await staff.get<List<dynamic>>('/api/reference/locations'))
              .data!
              .first['id'] as String;
      final storageId =
          (await staff.get<List<dynamic>>('/api/reference/storage-locations'))
              .data!
              .first['id'] as String;

      // 1. Mobile: the owner reports the loss.
      final reports = LostReportRepository(dio: c.dio);
      final report = await reports.createReport(CreateLostReportRequest(
        categoryId: category['id'] as String,
        itemTypeId: itemTypeId,
        lastSeenLocationId: locationId,
        description: 'Teal item $tag lost on the bus, has a sticker',
        primaryColor: 'Teal',
        estimatedLostFromAt:
            DateTime.now().toUtc().subtract(const Duration(hours: 3)),
        estimatedLostToAt:
            DateTime.now().toUtc().subtract(const Duration(hours: 2)),
      ));
      expect((await reports.getMyReports()).items.map((r) => r.id),
          contains(report.id));

      // 2. Desk (web): log the item and ask the matching agent.
      final found =
          (await staff.post<Map<String, dynamic>>('/api/found-reports', data: {
        'categoryId': category['id'],
        'itemTypeId': itemTypeId,
        'primaryColor': 'Teal',
        'foundLocationId': locationId,
        'storageLocationId': storageId,
        'generalDescription': 'Teal item $tag handed in from the bus',
        'privateVerificationDetails': 'A sticker of a red kite on the back',
        'foundAt': DateTime.now()
            .toUtc()
            .subtract(const Duration(hours: 1))
            .toIso8601String(),
      }))
              .data!;
      final ai = (await staff.post<Map<String, dynamic>>(
              '/api/match-suggestions/generate-ai',
              data: {'lostReportId': report.id, 'foundReportId': found['id']}))
          .data!;
      expect(ai['recommendation'], 'match_candidate');

      // 3. Mobile: the suggestion arrives; the owner claims it.
      final matches = await reports.getPossibleMatches(report.id);
      expect(matches.map((m) => m.foundItem.id), contains(found['id']));
      final claims = ClaimRepository(c.dio);
      var claim = await claims.createClaim(CreateClaimRequest(
          lostReportId: report.id, foundReportId: found['id'] as String));
      expect(claim.status, 'Pending');

      // 4. Desk: a verification question.
      await staff.post('/api/claims/${claim.id}/questions', data: {
        'questions': ['What is the sticker of?']
      });

      // 5. Mobile: the question arrives, without the hidden detail; the owner answers.
      claim = await claims.getClaim(claim.id);
      expect(claim.questions.single.questionText, 'What is the sticker of?');
      expect(claim.toString(), isNot(contains('red kite')));
      claim = await claims.submitAnswers(claim.id, [
        ClaimAnswerInput(
            questionId: claim.questions.single.id, answerText: 'A red kite')
      ]);
      // A desk-written question has no drafted evidence to score against, so it goes to a person.
      expect(claim.status, anyOf('UnderReview', 'ManualReviewRequired'));
      expect((await claims.getMyClaims()).items.map((c) => c.id),
          contains(claim.id));
    });
  });
}
