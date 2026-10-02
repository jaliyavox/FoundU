import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';

/// "Forgot password". Sends the reset email; the link in it opens FoundU on the web to choose
/// the new password. The answer is the same whether or not the address has an account.
class ForgotPasswordPage extends ConsumerStatefulWidget {
  const ForgotPasswordPage({super.key});

  @override
  ConsumerState<ForgotPasswordPage> createState() => _ForgotPasswordPageState();
}

class _ForgotPasswordPageState extends ConsumerState<ForgotPasswordPage> {
  final _email = TextEditingController();
  bool _busy = false;
  bool _sent = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final email = _email.text.trim();
    if (!email.contains('@')) {
      setState(() => _error = 'Enter the email you signed up with.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(authRepositoryProvider).requestPasswordReset(email);
      if (mounted) setState(() => _sent = true);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(24, 8, 24, 24),
          children: [
            Text(_sent ? 'Check your inbox' : 'Forgot your password?', style: text.headlineSmall),
            const SizedBox(height: 8),
            Text(
              _sent
                  ? 'If ${_email.text.trim()} has a FoundU account, a link to choose a new password is on its way '
                      'from noreply@thejaliya.com. It works once and expires in a day.'
                  : 'Enter the email you signed up with and we will send you a link to choose a new one.',
              style: text.bodyMedium?.copyWith(color: Brand.muted, height: 1.45),
            ),
            const SizedBox(height: 24),
            if (_sent) ...[
              Text('Nothing after a few minutes? Check your spam folder, then try again.',
                  style: text.bodySmall?.copyWith(color: Brand.muted)),
              const SizedBox(height: 16),
              OutlinedButton(onPressed: () => setState(() => _sent = false), child: const Text('Use a different email')),
            ] else ...[
              TextField(
                controller: _email,
                keyboardType: TextInputType.emailAddress,
                autofillHints: const [AutofillHints.email],
                textInputAction: TextInputAction.send,
                onSubmitted: (_) => _send(),
                decoration: InputDecoration(labelText: 'Email', errorText: _error),
              ),
              const SizedBox(height: 20),
              InkButton(label: 'Send the reset link', busy: _busy, onPressed: _send),
            ],
          ],
        ),
      ),
    );
  }
}
