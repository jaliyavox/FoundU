import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/auth/data/auth_models.dart';
import '../../features/auth/data/auth_repository.dart';
import '../api/api_client.dart';
import '../api/api_exception.dart';

final authControllerProvider =
    AsyncNotifierProvider<AuthController, AuthUser?>(AuthController.new);

class AuthController extends AsyncNotifier<AuthUser?> {
  StreamSubscription<void>? _invalidationSubscription;

  AuthRepository get _repository => ref.read(authRepositoryProvider);

  @override
  Future<AuthUser?> build() async {
    _invalidationSubscription = _repository.sessionInvalidated.listen((_) {
      state = const AsyncData(null);
    });
    ref.onDispose(() => _invalidationSubscription?.cancel());

    if (!await _repository.hasStoredSession()) return null;

    try {
      return await _repository.getCurrentUser();
    } on ApiException catch (error) {
      if (error.statusCode != 401) rethrow;
      await _repository.clearSession();
      return null;
    }
  }

  Future<void> login({required String email, required String password}) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => _repository.login(email: email.trim(), password: password),
    );
  }

  Future<void> logout() async {
    state = const AsyncLoading();
    try {
      await _repository.logout();
    } on Object {
      // The repository clears local credentials in a finally block.
    }
    state = const AsyncData(null);
  }
}
