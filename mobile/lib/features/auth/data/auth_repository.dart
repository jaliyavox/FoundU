import 'dart:async';

import 'package:dio/dio.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/auth/token_storage.dart';
import 'auth_models.dart';

class AuthRepository {
  AuthRepository({
    required Dio authDio,
    required Dio authenticatedDio,
    required TokenStorage tokenStorage,
  })  : _authDio = authDio,
        _authenticatedDio = authenticatedDio,
        _tokenStorage = tokenStorage;

  final Dio _authDio;
  final Dio _authenticatedDio;
  final TokenStorage _tokenStorage;
  final _sessionInvalidated = StreamController<void>.broadcast();

  Dio get authenticatedDio => _authenticatedDio;
  Stream<void> get sessionInvalidated => _sessionInvalidated.stream;

  void notifySessionInvalidated() => _sessionInvalidated.add(null);

  void dispose() => _sessionInvalidated.close();

  Future<AuthUser> login(
      {required String email, required String password}) async {
    try {
      final response = await _authDio.post<Map<String, dynamic>>(
        '/api/auth/login',
        data: {'email': email, 'password': password},
      );
      final auth = AuthResponse.fromJson(response.data!);
      await _saveTokens(auth);
      return auth.user;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<AuthUser> getCurrentUser() async {
    try {
      final response = await _authenticatedDio.get<Map<String, dynamic>>(
        '/api/auth/me',
      );
      return AuthUser.fromMeJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<bool> refreshSession() async {
    final refreshToken = await _tokenStorage.readRefreshToken();
    if (refreshToken == null || refreshToken.trim().isEmpty) {
      await _tokenStorage.clear();
      return false;
    }

    try {
      final response = await _authDio.post<Map<String, dynamic>>(
        '/api/auth/refresh',
        data: {'refreshToken': refreshToken},
      );
      final auth = AuthResponse.fromJson(response.data!);
      await _saveTokens(auth);
      return true;
    } on Object {
      await _tokenStorage.clear();
      return false;
    }
  }

  Future<void> logout() async {
    try {
      final refreshToken = await _tokenStorage.readRefreshToken();
      if (refreshToken != null && refreshToken.isNotEmpty) {
        await _authDio.post<void>(
          '/api/auth/logout',
          data: {'refreshToken': refreshToken},
        );
      }
    } finally {
      await _tokenStorage.clear();
    }
  }

  Future<void> clearSession() => _tokenStorage.clear();

  Future<bool> hasStoredSession() async {
    final accessToken = await _tokenStorage.readAccessToken();
    final refreshToken = await _tokenStorage.readRefreshToken();
    return (accessToken != null && accessToken.isNotEmpty) ||
        (refreshToken != null && refreshToken.isNotEmpty);
  }

  Future<void> _saveTokens(AuthResponse auth) {
    return _tokenStorage.save(
      AuthTokens(
        accessToken: auth.accessToken,
        refreshToken: auth.refreshToken,
      ),
    );
  }
}
