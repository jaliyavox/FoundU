import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_sign_in/google_sign_in.dart';

import '../../../core/api/api_client.dart';

/// The Google account picker, behind a seam so the login screen can be tested without
/// Google Play services.
class GoogleSignInService {
  bool _initialised = false;

  /// Android and iOS show the system picker; elsewhere (the web build) Google requires its
  /// own rendered button, so the phone button is not offered there.
  bool get isSupported => GoogleSignIn.instance.supportsAuthenticate();

  /// Shows the picker and returns the account's ID token - or null if the person backed out.
  /// [serverClientId] is the web client id the API checks tokens against, so the token is
  /// issued for the API rather than for this app.
  Future<String?> idToken(String serverClientId) async {
    if (!_initialised) {
      await GoogleSignIn.instance.initialize(serverClientId: serverClientId);
      _initialised = true;
    }
    try {
      final account = await GoogleSignIn.instance.authenticate();
      return account.authentication.idToken;
    } on GoogleSignInException catch (error) {
      if (error.code == GoogleSignInExceptionCode.canceled ||
          error.code == GoogleSignInExceptionCode.interrupted) {
        return null;
      }
      rethrow;
    }
  }

  /// Forgets the picked account on this device, so the next sign-in asks again.
  Future<void> signOut() async {
    if (_initialised) await GoogleSignIn.instance.signOut();
  }
}

final googleSignInServiceProvider = Provider<GoogleSignInService>((ref) => GoogleSignInService());

/// The client id, when Google sign-in is switched on and this platform can show the picker.
final googleClientIdProvider = FutureProvider<String?>((ref) async {
  if (!ref.watch(googleSignInServiceProvider).isSupported) return null;
  return ref.watch(authRepositoryProvider).googleClientId();
});
