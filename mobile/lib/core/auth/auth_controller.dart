import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/auth/data/auth_models.dart';
import '../../features/auth/data/auth_repository.dart';
import '../../features/notifications/data/push_notification_manager.dart';
import '../api/api_client.dart';
import '../api/api_exception.dart';
import 'auth_session.dart';

final authControllerProvider =
    AsyncNotifierProvider<AuthController, AuthUser?>(AuthController.new);

class AuthController extends AsyncNotifier<AuthUser?> {
  StreamSubscription<void>? _invalidationSubscription;

  AuthRepository get _repository => ref.read(authRepositoryProvider);

  @override
  Future<AuthUser?> build() async {
    _invalidationSubscription = _repository.sessionInvalidated.listen((_) {
      ref.read(authSessionEpochProvider.notifier).advance();
      state = const AsyncData(null);
    });
    ref.onDispose(() => _invalidationSubscription?.cancel());

    if (!await _repository.hasStoredSession()) return null;

    try {
      final user = await _repository.getCurrentUser();
      unawaited(ref.read(pushNotificationManagerProvider).start());
      return user;
    } on ApiException catch (error) {
      if (error.statusCode != 401) rethrow;
      await _repository.clearSession();
      return null;
    }
  }

  Future<void> login({required String email, required String password}) async {
    // The login screen is a new identity boundary. Clear credentials before any new
    // authenticated list work can run, then invalidate account-scoped provider state.
    await _repository.clearSession();
    ref.read(authSessionEpochProvider.notifier).advance();
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => _repository.login(email: email.trim(), password: password),
    );
    if (state.value != null) unawaited(ref.read(pushNotificationManagerProvider).start());
  }

  Future<void> logout() async {
    state = const AsyncLoading();
    await ref.read(pushNotificationManagerProvider).unregister();
    try {
      await _repository.logout();
    } on Object {
      // The repository clears local credentials in a finally block.
    }
    ref.read(authSessionEpochProvider.notifier).advance();
    state = const AsyncData(null);
  }
}
