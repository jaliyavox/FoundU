import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../features/auth/data/auth_repository.dart';
import '../auth/token_storage.dart';
import 'api_config.dart';
import 'auth_interceptor.dart';

BaseOptions _baseOptions() => BaseOptions(
      baseUrl: ApiConfig.baseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 15),
      headers: {'Accept': 'application/json'},
    );

final tokenStorageProvider = Provider<TokenStorage>((ref) {
  return SecureTokenStorage(const FlutterSecureStorage());
});

final authDioProvider = Provider<Dio>((ref) => Dio(_baseOptions()));

final authRepositoryProvider = Provider<AuthRepository>((ref) {
  final authenticatedDio = Dio(_baseOptions());
  final retryDio = Dio(_baseOptions());
  final tokenStorage = ref.watch(tokenStorageProvider);
  final repository = AuthRepository(
    authDio: ref.watch(authDioProvider),
    authenticatedDio: authenticatedDio,
    tokenStorage: tokenStorage,
  );
  authenticatedDio.interceptors.add(
    AuthInterceptor(
      retryDio: retryDio,
      tokenStorage: tokenStorage,
      refreshSession: repository.refreshSession,
      onSessionInvalidated: repository.notifySessionInvalidated,
    ),
  );
  ref.onDispose(repository.dispose);
  return repository;
});

final apiClientProvider = Provider<Dio>((ref) {
  return ref.watch(authRepositoryProvider).authenticatedDio;
});
