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
    // A successful login replaces both tokens. Clearing first also ensures an in-flight
    // role switch cannot keep attaching the previous account's bearer token.
    await _tokenStorage.clear();
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

  /// The client id Google sign-in uses, or null when the API has it switched off (or cannot
  /// be reached - either way, no Google button). The id is public; the API is the one place
  /// it is configured, so the phone always asks for the same id tokens are checked against.
  Future<String?> googleClientId() async {
    try {
      final response = await _authDio.get<Map<String, dynamic>>('/api/auth/google/status');
      final data = response.data!;
      final id = data['clientId'] as String?;
      return data['enabled'] == true && id != null && id.isNotEmpty ? id : null;
    } on DioException {
      return null;
    }
  }

  /// Signs in with a Google ID token. The API verifies it against Google's keys; a new
  /// address becomes a Student account, an existing one is linked only if Google has
  /// verified the email.
  Future<AuthUser> signInWithGoogle(String idToken) async {
    await _tokenStorage.clear();
    try {
      final response = await _authDio.post<Map<String, dynamic>>(
        '/api/auth/google',
        data: {'idToken': idToken},
      );
      final auth = AuthResponse.fromJson(response.data!);
      await _saveTokens(auth);
      return auth.user;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  /// Creates a Student account and signs it in - one round trip, the same response shape as
  /// login, so the wizard lands straight on the feed.
  Future<AuthUser> register({
    required String fullName,
    required String email,
    required String password,
    String? studentNumber,
  }) async {
    try {
      final response = await _authDio.post<Map<String, dynamic>>(
        '/api/auth/register',
        data: {
          'fullName': fullName,
          'email': email,
          'password': password,
          if (studentNumber != null && studentNumber.trim().isNotEmpty) 'studentNumber': studentNumber.trim(),
        },
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
    } on DioException catch (error) {
      // Only the server saying no ends the session. A timeout on weak Wi-Fi keeps the tokens,
      // so the next request can try again instead of sending the student back to login.
      final status = error.response?.statusCode;
      if (status == 400 || status == 401 || status == 403) {
        await _tokenStorage.clear();
      }
      return false;
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

  /// Stores a token pair the app was handed outside login - a password change returns one,
  /// because the change ends every other session and would otherwise end this one too.
  Future<AuthUser> adoptSession(AuthResponse auth) async {
    await _saveTokens(auth);
    return auth.user;
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
