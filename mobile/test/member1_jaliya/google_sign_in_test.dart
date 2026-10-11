import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/auth/auth_controller.dart';
import 'package:foundu/core/theme/app_theme.dart';
import 'package:foundu/features/auth/data/auth_models.dart';
import 'package:foundu/features/auth/data/google_sign_in_service.dart';
import 'package:foundu/features/auth/presentation/login_page.dart';

/// The Google account picker, without Google Play services.
class FakeGoogle extends GoogleSignInService {
  FakeGoogle(this.token);
  final String? token;
  final asked = <String>[];

  @override
  bool get isSupported => true;

  @override
  Future<String?> idToken(String serverClientId) async {
    asked.add(serverClientId);
    return token;
  }
}

/// Signed out, and remembers the token it was handed instead of calling the API.
class SignedOut extends AuthController {
  final tokens = <String>[];

  @override
  Future<AuthUser?> build() async => null;

  @override
  Future<void> signInWithGoogle(String idToken) async => tokens.add(idToken);
}

Future<SignedOut> mount(WidgetTester tester, {String? clientId, FakeGoogle? google}) async {
  final auth = SignedOut();
  await tester.pumpWidget(ProviderScope(
    overrides: [
      authControllerProvider.overrideWith(() => auth),
      googleClientIdProvider.overrideWith((ref) async => clientId),
      googleSignInServiceProvider.overrideWithValue(google ?? FakeGoogle(null)),
    ],
    child: MaterialApp(theme: buildFoundUTheme(), home: const LoginPage()),
  ));
  await settle(tester);
  return auth;
}

/// The login illustration animates for ever, so "settled" never comes: pump a fixed time.
Future<void> settle(WidgetTester tester) async {
  for (var i = 0; i < 10; i++) {
    await tester.pump(const Duration(milliseconds: 100));
  }
}

void main() {
  testWidgets('with Google switched off on the API there is no Google button', (tester) async {
    await mount(tester);

    expect(find.text('Continue with Google'), findsNothing);
    expect(find.text('Sign in'), findsOneWidget);
  });

  testWidgets('the picker is asked for a token for the API client id, and the token signs in', (tester) async {
    final google = FakeGoogle('id-token-from-google');
    final auth = await mount(tester, clientId: 'web-client.apps.googleusercontent.com', google: google);

    await tester.ensureVisible(find.text('Continue with Google'));
    await tester.tap(find.text('Continue with Google'));
    await settle(tester);

    expect(google.asked, ['web-client.apps.googleusercontent.com']);
    expect(auth.tokens, ['id-token-from-google']);
  });

  testWidgets('backing out of the picker signs nobody in and says nothing', (tester) async {
    final auth = await mount(tester, clientId: 'web-client', google: FakeGoogle(null));

    await tester.ensureVisible(find.text('Continue with Google'));
    await tester.tap(find.text('Continue with Google'));
    await settle(tester);

    expect(auth.tokens, isEmpty);
    expect(find.byType(SnackBar), findsNothing);
  });
}
