import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/api/api_client.dart';
import 'package:foundu/core/api/api_exception.dart';
import 'package:foundu/core/api/auth_interceptor.dart';
import 'package:foundu/core/auth/auth_controller.dart';
import 'package:foundu/core/auth/token_storage.dart';
import 'package:foundu/features/auth/data/auth_repository.dart';

void main() {
  test('login follows the API contract and stores both tokens', () async {
    final storage = MemoryTokenStorage();
    final authDio = Dio();
    authDio.httpClientAdapter = CallbackAdapter((options) {
      expect(options.path, '/api/auth/login');
      expect(options.headers.containsKey('Authorization'), isFalse);
      expect(options.data, {
        'email': 'jane@foundu.test',
        'password': 'Password123',
      });
      return jsonResponse(200, authResponseJson());
    });
    final repository = AuthRepository(
      authDio: authDio,
      authenticatedDio: Dio(),
      tokenStorage: storage,
    );

    final user = await repository.login(
      email: 'jane@foundu.test',
      password: 'Password123',
    );

    expect(user.name, 'Jane Student');
    expect(await storage.readAccessToken(), 'access-2');
    expect(await storage.readRefreshToken(), 'refresh-2');
  });

  test('startup refreshes one invalid access token and validates me', () async {
    final storage = MemoryTokenStorage(
      const AuthTokens(accessToken: 'expired', refreshToken: 'refresh-1'),
    );
    var refreshCalls = 0;

    final authDio = Dio();
    authDio.httpClientAdapter = CallbackAdapter((options) {
      expect(options.path, '/api/auth/refresh');
      expect(options.headers.containsKey('Authorization'), isFalse);
      expect(options.data, {'refreshToken': 'refresh-1'});
      refreshCalls++;
      return jsonResponse(200, authResponseJson());
    });

    final authenticatedDio = Dio();
    final retryDio = Dio();
    late final AuthRepository repository;
    final protectedAdapter = CallbackAdapter((options) {
      final authorization = options.headers['Authorization'];
      if (authorization == 'Bearer expired') return jsonResponse(401, {});
      expect(authorization, 'Bearer access-2');
      return jsonResponse(200, {
        'id': 'user-1',
        'email': 'jane@foundu.test',
        'name': 'Jane Student',
        'role': 'Student',
      });
    });
    authenticatedDio.httpClientAdapter = protectedAdapter;
    retryDio.httpClientAdapter = protectedAdapter;
    repository = AuthRepository(
      authDio: authDio,
      authenticatedDio: authenticatedDio,
      tokenStorage: storage,
    );
    authenticatedDio.interceptors.add(
      AuthInterceptor(
        retryDio: retryDio,
        tokenStorage: storage,
        refreshSession: repository.refreshSession,
        onSessionInvalidated: repository.notifySessionInvalidated,
      ),
    );

    final container = ProviderContainer(
      overrides: [authRepositoryProvider.overrideWithValue(repository)],
    );
    addTearDown(container.dispose);

    final user = await container.read(authControllerProvider.future);

    expect(user?.name, 'Jane Student');
    expect(refreshCalls, 1);
    expect(await storage.readAccessToken(), 'access-2');
    expect(await storage.readRefreshToken(), 'refresh-2');
  });

  test('failed refresh clears the stored session', () async {
    final storage = MemoryTokenStorage(
      const AuthTokens(accessToken: 'expired', refreshToken: 'invalid'),
    );
    final authDio = Dio()
      ..httpClientAdapter = CallbackAdapter(
        (_) => jsonResponse(401, {'detail': 'Invalid refresh token.'}),
      );
    final repository = AuthRepository(
      authDio: authDio,
      authenticatedDio: Dio(),
      tokenStorage: storage,
    );

    expect(await repository.refreshSession(), isFalse);
    expect(await storage.readAccessToken(), isNull);
    expect(await storage.readRefreshToken(), isNull);
  });

  test('missing refresh token clears a stale access token', () async {
    final storage = MemoryTokenStorage(
      const AuthTokens(accessToken: 'stale-access', refreshToken: ''),
    );
    final repository = AuthRepository(
      authDio: Dio(),
      authenticatedDio: Dio(),
      tokenStorage: storage,
    );

    expect(await repository.refreshSession(), isFalse);
    expect(await storage.readAccessToken(), isNull);
    expect(await storage.readRefreshToken(), isNull);
  });

  test('a retried 401 refreshes once and then returns the failure', () async {
    final storage = MemoryTokenStorage(
      const AuthTokens(accessToken: 'expired', refreshToken: 'refresh-1'),
    );
    var refreshCalls = 0;
    var protectedCalls = 0;
    var invalidationCalls = 0;

    final authDio = Dio()
      ..httpClientAdapter = CallbackAdapter((_) {
        refreshCalls++;
        return jsonResponse(200, authResponseJson());
      });
    final protectedAdapter = CallbackAdapter((_) {
      protectedCalls++;
      return jsonResponse(401, {});
    });
    final authenticatedDio = Dio()..httpClientAdapter = protectedAdapter;
    final retryDio = Dio()..httpClientAdapter = protectedAdapter;
    late final AuthRepository repository;
    repository = AuthRepository(
      authDio: authDio,
      authenticatedDio: authenticatedDio,
      tokenStorage: storage,
    );
    authenticatedDio.interceptors.add(
      AuthInterceptor(
        retryDio: retryDio,
        tokenStorage: storage,
        refreshSession: repository.refreshSession,
        onSessionInvalidated: () {
          invalidationCalls++;
          repository.notifySessionInvalidated();
        },
      ),
    );

    await expectLater(
      repository.getCurrentUser(),
      throwsA(isA<ApiException>()),
    );
    expect(refreshCalls, 1);
    expect(protectedCalls, 2);
    expect(await storage.readAccessToken(), isNull);
    expect(await storage.readRefreshToken(), isNull);
    expect(invalidationCalls, 1);
  });

  test('logout sends only the refresh token and clears locally on API failure',
      () async {
    final storage = MemoryTokenStorage(
      const AuthTokens(accessToken: 'access', refreshToken: 'refresh'),
    );
    final authDio = Dio();
    authDio.httpClientAdapter = CallbackAdapter((options) {
      expect(options.path, '/api/auth/logout');
      expect(options.headers.containsKey('Authorization'), isFalse);
      expect(options.data, {'refreshToken': 'refresh'});
      return jsonResponse(500, {});
    });
    final repository = AuthRepository(
      authDio: authDio,
      authenticatedDio: Dio(),
      tokenStorage: storage,
    );

    await expectLater(repository.logout(), throwsA(isA<DioException>()));
    expect(await storage.readAccessToken(), isNull);
    expect(await storage.readRefreshToken(), isNull);
  });
}

Map<String, dynamic> authResponseJson() => {
      'accessToken': 'access-2',
      'accessTokenExpiresAtUtc': '2026-09-09T12:00:00Z',
      'refreshToken': 'refresh-2',
      'refreshTokenExpiresAtUtc': '2026-09-23T12:00:00Z',
      'user': {
        'id': 'user-1',
        'fullName': 'Jane Student',
        'email': 'jane@foundu.test',
        'role': 'Student',
        'studentNumber': null,
        'isSuspended': false,
      },
    };

ResponseBody jsonResponse(int statusCode, Object body) {
  return ResponseBody.fromString(
    jsonEncode(body),
    statusCode,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );
}

class CallbackAdapter implements HttpClientAdapter {
  CallbackAdapter(this.callback);

  final ResponseBody Function(RequestOptions options) callback;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    return callback(options);
  }

  @override
  void close({bool force = false}) {}
}

class MemoryTokenStorage implements TokenStorage {
  MemoryTokenStorage([AuthTokens? tokens])
      : _accessToken = tokens?.accessToken,
        _refreshToken = tokens?.refreshToken;

  String? _accessToken;
  String? _refreshToken;

  @override
  Future<void> clear() async {
    _accessToken = null;
    _refreshToken = null;
  }

  @override
  Future<String?> readAccessToken() async => _accessToken;

  @override
  Future<String?> readRefreshToken() async => _refreshToken;

  @override
  Future<void> save(AuthTokens tokens) async {
    _accessToken = tokens.accessToken;
    _refreshToken = tokens.refreshToken;
  }
}
