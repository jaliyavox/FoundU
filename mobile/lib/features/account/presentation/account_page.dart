import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';
import '../data/account_repository.dart';

/// Your own account: name, email, student number and password.
///
/// Two separate forms because they have different stakes - one is a correction, the other
/// signs out every other device you have.
class AccountPage extends ConsumerWidget {
  const AccountPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = ref.watch(profileProvider);
    final text = Theme.of(context).textTheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Account settings')),
      body: profile.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Panel(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Could not load your account', style: text.titleMedium),
                  const SizedBox(height: 4),
                  Text(
                    error is ApiException ? error.message : 'Check your connection and try again.',
                    style: text.bodyMedium?.copyWith(color: Brand.muted),
                  ),
                  const SizedBox(height: 12),
                  InkButton(label: 'Try again', onPressed: () => ref.invalidate(profileProvider)),
                ],
              ),
            ),
          ],
        ),
        data: (data) => ListView(
          padding: const EdgeInsets.fromLTRB(20, 8, 20, 40),
          children: [
            _DetailsForm(profile: data),
            const SizedBox(height: 16),
            _PasswordForm(profile: data),
          ],
        ),
      ),
    );
  }
}

class _DetailsForm extends ConsumerStatefulWidget {
  const _DetailsForm({required this.profile});
  final Profile profile;

  @override
  ConsumerState<_DetailsForm> createState() => _DetailsFormState();
}

class _DetailsFormState extends ConsumerState<_DetailsForm> {
  late final _name = TextEditingController(text: widget.profile.fullName);
  late final _email = TextEditingController(text: widget.profile.email);
  late final _studentNumber = TextEditingController(text: widget.profile.studentNumber ?? '');
  final _currentPassword = TextEditingController();
  Map<String, List<String>> _errors = const {};
  bool _busy = false;

  bool get _emailChanged => _email.text.trim().toLowerCase() != widget.profile.email.toLowerCase();

  // Moving the address needs the password: it is the sign-in name and where a reset would go.
  bool get _needsPassword => _emailChanged && widget.profile.hasPassword;

  @override
  void initState() {
    super.initState();
    _email.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    _studentNumber.dispose();
    _currentPassword.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() {
      _busy = true;
      _errors = const {};
    });
    try {
      final saved = await ref.read(accountRepositoryProvider).updateProfile(
            fullName: _name.text.trim(),
            email: _email.text.trim(),
            studentNumber: _studentNumber.text.trim().isEmpty ? null : _studentNumber.text.trim(),
            currentPassword: _needsPassword ? _currentPassword.text : null,
          );
      // The name shown around the app comes from the signed-in user, not from this form.
      ref.read(authControllerProvider.notifier).applyProfile(fullName: saved.fullName, email: saved.email);
      ref.invalidate(profileProvider);
      _currentPassword.clear();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Saved.')));
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() => _errors = error.fieldErrors);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(child: Text('Your details', style: text.titleMedium)),
              StatusChip(widget.profile.role, tone: ChipTone.neutral),
            ],
          ),
          const SizedBox(height: 16),
          _Field(
            controller: _name,
            label: 'Full name',
            help: 'Finders see this on your reports.',
            errors: _errors['FullName'],
            autofill: const [AutofillHints.name],
          ),
          _Field(
            controller: _email,
            label: 'Email',
            help: 'What you sign in with. Never shown on the feed.',
            errors: _errors['Email'],
            keyboard: TextInputType.emailAddress,
            autofill: const [AutofillHints.email],
          ),
          _Field(
            controller: _studentNumber,
            label: 'Student number (optional)',
            errors: _errors['StudentNumber'],
          ),
          if (_needsPassword)
            _Field(
              controller: _currentPassword,
              label: 'Current password',
              help: 'Needed to move your email address.',
              errors: _errors['CurrentPassword'],
              obscure: true,
              autofill: const [AutofillHints.password],
            ),
          const SizedBox(height: 4),
          InkButton(label: 'Save changes', busy: _busy, onPressed: _save),
          if (widget.profile.isGoogleLinked) ...[
            const SizedBox(height: 10),
            Row(
              children: [
                const Icon(Icons.verified_user_outlined, size: 16, color: Brand.muted),
                const SizedBox(width: 6),
                Text('Google is linked to this account', style: text.bodySmall?.copyWith(color: Brand.muted)),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _PasswordForm extends ConsumerStatefulWidget {
  const _PasswordForm({required this.profile});
  final Profile profile;

  @override
  ConsumerState<_PasswordForm> createState() => _PasswordFormState();
}

class _PasswordFormState extends ConsumerState<_PasswordForm> {
  final _current = TextEditingController();
  final _next = TextEditingController();
  final _repeat = TextEditingController();
  Map<String, List<String>> _errors = const {};
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _repeat.addListener(() => setState(() {}));
    _next.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    _repeat.dispose();
    super.dispose();
  }

  bool get _mismatch => _repeat.text.isNotEmpty && _repeat.text != _next.text;

  Future<void> _change() async {
    if (_mismatch) return;
    setState(() {
      _busy = true;
      _errors = const {};
    });
    try {
      final fresh = await ref.read(accountRepositoryProvider).changePassword(
            currentPassword: widget.profile.hasPassword ? _current.text : null,
            newPassword: _next.text,
          );
      // Every other session has just ended; this device gets a fresh pair so it stays in.
      await ref.read(authControllerProvider.notifier).adoptSession(fresh);
      ref.invalidate(profileProvider);
      _current.clear();
      _next.clear();
      _repeat.clear();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Password changed. Your other devices have been signed out.')),
      );
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() => _errors = error.fieldErrors);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final hasPassword = widget.profile.hasPassword;

    return Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(hasPassword ? 'Change your password' : 'Set a password', style: text.titleMedium),
          if (!hasPassword) ...[
            const SizedBox(height: 6),
            Text(
              'You signed up with Google, so this account has no password yet. Setting one lets you sign in either way.',
              style: text.bodySmall?.copyWith(color: Brand.muted, height: 1.4),
            ),
          ],
          const SizedBox(height: 16),
          if (hasPassword)
            _Field(
              controller: _current,
              label: 'Current password',
              errors: _errors['CurrentPassword'] ?? _errors['PasswordMismatch'],
              obscure: true,
              autofill: const [AutofillHints.password],
            ),
          _Field(
            controller: _next,
            label: 'New password',
            help: 'At least 8 characters.',
            errors: _errors['NewPassword'] ?? _errors['PasswordTooShort'],
            obscure: true,
            autofill: const [AutofillHints.newPassword],
          ),
          _Field(
            controller: _repeat,
            label: 'Repeat the new password',
            errors: _mismatch ? const ['These two do not match.'] : null,
            obscure: true,
            autofill: const [AutofillHints.newPassword],
          ),
          const SizedBox(height: 4),
          InkButton(
            label: hasPassword ? 'Change password' : 'Set password',
            busy: _busy,
            onPressed: _mismatch || _next.text.isEmpty ? null : _change,
          ),
          const SizedBox(height: 8),
          Text(
            'This signs out every other device. You stay signed in here.',
            style: text.bodySmall?.copyWith(color: Brand.muted),
          ),
        ],
      ),
    );
  }
}

class _Field extends StatelessWidget {
  const _Field({
    required this.controller,
    required this.label,
    this.help,
    this.errors,
    this.obscure = false,
    this.keyboard,
    this.autofill,
  });

  final TextEditingController controller;
  final String label;
  final String? help;
  final List<String>? errors;
  final bool obscure;
  final TextInputType? keyboard;
  final Iterable<String>? autofill;

  @override
  Widget build(BuildContext context) {
    final error = (errors ?? const []).isEmpty ? null : errors!.join('\n');
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: TextField(
        controller: controller,
        obscureText: obscure,
        keyboardType: keyboard,
        autofillHints: autofill,
        decoration: InputDecoration(labelText: label, helperText: error == null ? help : null, errorText: error, helperMaxLines: 2),
      ),
    );
  }
}
