import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/foundu_mark.dart';
import '../../../core/widgets/surfaces.dart';
import '../data/google_sign_in_service.dart';
import 'onboarding_illustrations.dart';

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _showPassword = false;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  bool _googleBusy = false;

  Future<void> _google(String clientId) async {
    setState(() => _googleBusy = true);
    try {
      final idToken = await ref.read(googleSignInServiceProvider).idToken(clientId);
      // Backed out of the picker: nothing happened, so nothing to say.
      if (idToken == null || !mounted) return;
      await ref.read(authControllerProvider.notifier).signInWithGoogle(idToken);
    } on Object {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
        content: Text('Google sign-in is not available on this device right now. Use your email and password.'),
      ));
    } finally {
      if (mounted) setState(() => _googleBusy = false);
    }
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    FocusScope.of(context).unfocus();
    await ref.read(authControllerProvider.notifier).login(
          email: _emailController.text,
          password: _passwordController.text,
        );
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authControllerProvider);
    final isLoading = auth.isLoading;
    final text = Theme.of(context).textTheme;

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(24, 16, 24, 24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Row(
                      children: [
                        FoundUMark(size: 40),
                        SizedBox(width: 12),
                        Text('FoundU', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700, letterSpacing: -0.3)),
                      ],
                    ),
                    const SizedBox(height: 8),
                    const Center(child: OnboardingIllustration(scene: OnboardingScene.secure, size: 190)),
                    const SizedBox(height: 8),
                    Text('Welcome back', style: text.headlineSmall),
                    const SizedBox(height: 6),
                    Text(
                      'Sign in to see what has been found, and what is still missing.',
                      style: text.bodyMedium?.copyWith(color: Brand.muted, height: 1.45),
                    ),
                    const SizedBox(height: 22),
                    TextFormField(
                      controller: _emailController,
                      keyboardType: TextInputType.emailAddress,
                      autocorrect: false,
                      textInputAction: TextInputAction.next,
                      decoration: const InputDecoration(labelText: 'Email'),
                      validator: (v) => !(v ?? '').contains('@') ? 'Enter your email address.' : null,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _passwordController,
                      obscureText: !_showPassword,
                      textInputAction: TextInputAction.done,
                      onFieldSubmitted: (_) => _submit(),
                      decoration: InputDecoration(
                        labelText: 'Password',
                        suffixIcon: IconButton(
                          onPressed: () => setState(() => _showPassword = !_showPassword),
                          icon: Icon(_showPassword ? Icons.visibility_off_outlined : Icons.visibility_outlined),
                          tooltip: _showPassword ? 'Hide password' : 'Show password',
                        ),
                      ),
                      validator: (v) => (v ?? '').isEmpty ? 'Enter your password.' : null,
                    ),
                    Align(
                      alignment: Alignment.centerRight,
                      child: TextButton(
                        onPressed: isLoading ? null : () => context.push('/forgot-password'),
                        child: const Text('Forgot password?'),
                      ),
                    ),
                    if (auth.hasError) ...[
                      const SizedBox(height: 12),
                      Panel(
                        color: const Color(0xFFFBE9E7),
                        padding: const EdgeInsets.all(14),
                        child: Text(
                          auth.error.toString(),
                          style: text.bodySmall?.copyWith(color: Brand.danger, fontWeight: FontWeight.w500),
                        ),
                      ),
                    ],
                    const SizedBox(height: 20),
                    InkButton(label: 'Sign in', busy: isLoading, onPressed: _submit),
                    if (ref.watch(googleClientIdProvider).value case final clientId?) ...[
                      const SizedBox(height: 16),
                      Row(
                        children: [
                          const Expanded(child: Divider()),
                          Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 12),
                            child: Text('or', style: text.bodySmall?.copyWith(color: Brand.muted)),
                          ),
                          const Expanded(child: Divider()),
                        ],
                      ),
                      const SizedBox(height: 16),
                      _GoogleButton(
                        busy: _googleBusy,
                        onPressed: isLoading || _googleBusy ? null : () => _google(clientId),
                      ),
                    ],
                    const SizedBox(height: 12),
                    Center(
                      child: TextButton(
                        onPressed: isLoading ? null : () => context.push('/register'),
                        child: const Text('New here? Create an account'),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Google's own wording and a neutral outline, as its branding rules ask for.
class _GoogleButton extends StatelessWidget {
  const _GoogleButton({required this.busy, required this.onPressed});
  final bool busy;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton(
      onPressed: onPressed,
      style: OutlinedButton.styleFrom(
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF1F1F1F),
        side: const BorderSide(color: Color(0xFF747775)),
        shape: const StadiumBorder(),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (busy)
            const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2))
          else
            const Text('G', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: Color(0xFF4285F4))),
          const SizedBox(width: 10),
          const Flexible(child: Text('Continue with Google', overflow: TextOverflow.ellipsis)),
        ],
      ),
    );
  }
}
