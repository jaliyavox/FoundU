import 'package:dio/dio.dart';

import '../auth/token_storage.dart';

typedef RefreshSession = Future<bool> Function();

class SingleFlightRefresh {
  Future<bool>? _inFlight;

  Future<bool> run(RefreshSession refresh) {
    final current = _inFlight;
    if (current != null) return current;

    final next = refresh();
    _inFlight = next;
    next.whenComplete(() {
      if (identical(_inFlight, next)) _inFlight = null;
    });
    return next;
  }
}

class AuthInterceptor extends QueuedInterceptor {
  AuthInterceptor({
    required Dio retryDio,
    required TokenStorage tokenStorage,
    required RefreshSession refreshSession,
    required void Function() onSessionInvalidated,
    SingleFlightRefresh? refreshCoordinator,
  })  : _retryDio = retryDio,
        _tokenStorage = tokenStorage,
        _refreshSession = refreshSession,
        _onSessionInvalidated = onSessionInvalidated,
        _refreshCoordinator = refreshCoordinator ?? SingleFlightRefresh();

  static const _anonymousPaths = {
    '/api/auth/login',
    '/api/auth/refresh',
    '/api/auth/logout',
  };
  static const _retriedKey = 'foundu_auth_retried';

  final Dio _retryDio;
  final TokenStorage _tokenStorage;
  final RefreshSession _refreshSession;
  final void Function() _onSessionInvalidated;
  final SingleFlightRefresh _refreshCoordinator;

  bool _isAnonymous(String path) => _anonymousPaths.contains(path);

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    if (!_isAnonymous(options.path)) {
      final token = await _tokenStorage.readAccessToken();
      if (token != null && token.isNotEmpty) {
        options.headers['Authorization'] = 'Bearer $token';
      }
    } else {
      options.headers.remove('Authorization');
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final request = err.requestOptions;
    if (err.response?.statusCode != 401 ||
        _isAnonymous(request.path) ||
        request.extra[_retriedKey] == true) {
      handler.next(err);
      return;
    }

    final sentHeader = request.headers['Authorization'];
    final currentToken = await _tokenStorage.readAccessToken();
    var canRetry = currentToken != null &&
        currentToken.isNotEmpty &&
        sentHeader != 'Bearer $currentToken';

    if (!canRetry) {
      canRetry = await _refreshCoordinator.run(_refreshSession);
    }

    if (!canRetry) {
      _onSessionInvalidated();
      handler.next(err);
      return;
    }

    final refreshedToken = await _tokenStorage.readAccessToken();
    if (refreshedToken == null || refreshedToken.isEmpty) {
      _onSessionInvalidated();
      handler.next(err);
      return;
    }

    request.extra[_retriedKey] = true;
    request.headers['Authorization'] = 'Bearer $refreshedToken';
    try {
      handler.resolve(await _retryDio.fetch<dynamic>(request));
    } on DioException catch (retryError) {
      if (retryError.response?.statusCode == 401) {
        await _tokenStorage.clear();
        _onSessionInvalidated();
      }
      handler.next(retryError);
    }
  }
}
