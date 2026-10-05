import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/auth/data/auth_models.dart';
import '../../features/auth/data/auth_repository.dart';
import '../../features/notifications/data/push_notification_manager.dart';
import '../api/api_client.dart';
import '../api/api_exception.dart';
import 'auth_session.dart';
import '../../features/auth/data/google_sign_in_service.dart';

final authControllerProvider =
    AsyncNotifierProvider<AuthController, AuthUser?>(AuthController.new);

/// The app is the student side of FoundU; staff and admins work from the web dashboard, and
/// every tab here calls a Student-only endpoint.
const staffUseWebMessage =
    'The FoundU app is for students. Staff and admins sign in on the web dashboard.';

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
      if (user.role != 'Student') {
        await _repository.clearSession();
        return null;
      }
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
    state = await AsyncValue.guard(() async {
      final user = await _repository.login(email: email.trim(), password: password);
      if (user.role != 'Student') {
        try {
          await _repository.logout();
        } on Object {
          // The repository clears local credentials in a finally block.
        }
        throw const ApiException(staffUseWebMessage, statusCode: 403);
      }
      return user;
    });
    ref.read(authSessionEpochProvider.notifier).advance();
    if (state.value != null) unawaited(ref.read(pushNotificationManagerProvider).start());
  }

  /// The same boundary as [login], with a Google ID token instead of a password.
  Future<void> signInWithGoogle(String idToken) async {
    await _repository.clearSession();
    ref.read(authSessionEpochProvider.notifier).advance();
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final user = await _repository.signInWithGoogle(idToken);
      if (user.role != 'Student') {
        try {
          await _repository.logout();
        } on Object {
          // The repository clears local credentials in a finally block.
        }
        throw const ApiException(staffUseWebMessage, statusCode: 403);
      }
      return user;
    });
    ref.read(authSessionEpochProvider.notifier).advance();
    if (state.value != null) unawaited(ref.read(pushNotificationManagerProvider).start());
  }

  /// Returns the ApiException rather than putting the whole app into an error state: the
  /// wizard shows field errors inline and keeps the person on the step they were on.
  Future<ApiException?> register({
    required String fullName,
    required String email,
    required String password,
    String? studentNumber,
  }) async {
    try {
      final user = await _repository.register(
        fullName: fullName.trim(),
        email: email.trim(),
        password: password,
        studentNumber: studentNumber,
      );
      state = AsyncData(user);
      ref.read(authSessionEpochProvider.notifier).advance();
      unawaited(ref.read(pushNotificationManagerProvider).start());
      return null;
    } on ApiException catch (error) {
      return error;
    }
  }

  /// Swaps in the tokens a password change handed back, keeping this device signed in.
  Future<void> adoptSession(AuthResponse auth) async {
    state = AsyncData(await _repository.adoptSession(auth));
  }

  /// Reflects a profile edit straight away: the access token still carries the old name until
  /// it is next refreshed, so what is on screen comes from here.
  void applyProfile({String? fullName, String? email}) {
    final user = state.value;
    if (user == null) return;
    state = AsyncData(user.copyWith(fullName: fullName, email: email));
  }

  Future<void> logout() async {
    state = const AsyncLoading();
    ref.read(authSessionEpochProvider.notifier).advance();
    try {
      await ref.read(pushNotificationManagerProvider).unregister();
    } on Object {
      // Push availability must never prevent clearing local credentials.
    }
    try {
      await _repository.logout();
    } on Object {
      // The repository clears local credentials in a finally block.
    }
    try {
      // So the next Google sign-in asks which account, instead of silently reusing this one.
      await ref.read(googleSignInServiceProvider).signOut();
    } on Object {
      // Nothing to forget, or Play services unavailable - signing out of FoundU still stands.
    }
    ref.read(authSessionEpochProvider.notifier).advance();
    state = const AsyncData(null);
  }
}
