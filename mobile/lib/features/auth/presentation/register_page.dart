import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';
import 'onboarding_illustrations.dart';

/// Sign-up as three small steps rather than one long form. Each step has one job and its own
/// picture, and a server-side validation error sends you back to the step that owns the
/// field rather than dumping every message at the end.
class RegisterPage extends ConsumerStatefulWidget {
  const RegisterPage({super.key});

  @override
  ConsumerState<RegisterPage> createState() => _RegisterPageState();
}

class _RegisterPageState extends ConsumerState<RegisterPage> {
  final _pages = PageController();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _studentNumber = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  final _keys = [GlobalKey<FormState>(), GlobalKey<FormState>(), GlobalKey<FormState>()];

  int _step = 0;
  bool _busy = false;
  bool _showPassword = false;
  Map<String, List<String>> _serverErrors = const {};

  static const _steps = [
    (OnboardingScene.identity, 'Who are you?', 'The name the desk will greet you by when you collect something.'),
    (OnboardingScene.campus, 'Where do we reach you?', 'Your campus email, and your student number if you have it handy.'),
    (OnboardingScene.secure, 'Keep it yours', 'Eight characters or more, with an uppercase letter, a lowercase letter and a digit.'),
  ];

  @override
  void dispose() {
    _pages.dispose();
    for (final c in [_name, _email, _studentNumber, _password, _confirm]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _go(int step) async {
    setState(() => _step = step);
    await _pages.animateToPage(step, duration: const Duration(milliseconds: 380), curve: Curves.easeOutCubic);
  }

  Future<void> _next() async {
    if (!(_keys[_step].currentState?.validate() ?? false)) return;
    if (_step < 2) {
      await _go(_step + 1);
      return;
    }
    await _submit();
  }

  Future<void> _submit() async {
    setState(() {
      _busy = true;
      _serverErrors = const {};
    });
    final error = await ref.read(authControllerProvider.notifier).register(
          fullName: _name.text,
          email: _email.text,
          password: _password.text,
          studentNumber: _studentNumber.text,
        );
    if (!mounted) return;
    setState(() => _busy = false);
    if (error == null) return; // The router sees the session and moves to the feed.

    if (error.fieldErrors.isNotEmpty) {
      setState(() => _serverErrors = error.fieldErrors);
      // Jump back to the first step that owns a failing field.
      final owner = error.fieldErrors.keys.map(_stepFor).reduce((a, b) => a < b ? a : b);
      await _go(owner);
      _keys[owner].currentState?.validate();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    }
  }

  static int _stepFor(String field) => switch (field) {
        'FullName' => 0,
        'Email' || 'StudentNumber' => 1,
        _ => 2,
      };

  String? _serverErrorFor(String field) {
    final errors = _serverErrors[field];
    return errors == null || errors.isEmpty ? null : errors.join(' ');
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final (scene, title, body) = _steps[_step];

    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(12, 8, 20, 0),
              child: Row(
                children: [
                  IconButton(
                    onPressed: _busy
                        ? null
                        : () => _step == 0 ? context.pop() : _go(_step - 1),
                    icon: const Icon(Icons.arrow_back_rounded),
                    tooltip: 'Back',
                  ),
                  const Spacer(),
                  _Progress(step: _step, count: _steps.length),
                ],
              ),
            ),
            Expanded(
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(24, 8, 24, 24),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Center(
                      child: AnimatedSwitcher(
                        duration: const Duration(milliseconds: 420),
                        switchInCurve: Curves.easeOutCubic,
                        transitionBuilder: (child, animation) => FadeTransition(
                          opacity: animation,
                          child: ScaleTransition(
                            scale: Tween(begin: 0.94, end: 1.0).animate(animation),
                            child: child,
                          ),
                        ),
                        child: OnboardingIllustration(key: ValueKey(scene), scene: scene),
                      ),
                    ),
                    const SizedBox(height: 8),
                    AnimatedSwitcher(
                      duration: const Duration(milliseconds: 300),
                      child: Column(
                        key: ValueKey(_step),
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(title, style: text.headlineSmall),
                          const SizedBox(height: 6),
                          Text(body, style: text.bodyMedium?.copyWith(color: Brand.muted, height: 1.45)),
                        ],
                      ),
                    ),
                    const SizedBox(height: 22),
                    SizedBox(
                      height: 230,
                      child: PageView(
                        controller: _pages,
                        // A floating label rises above its field's top edge; a clipping
                        // viewport would cut it off, so the pages are allowed to overflow.
                        clipBehavior: Clip.none,
                        physics: const NeverScrollableScrollPhysics(),
                        children: [
                          _NameStep(formKey: _keys[0], controller: _name, serverError: _serverErrorFor('FullName'), onSubmit: _next),
                          _ContactStep(
                            formKey: _keys[1],
                            email: _email,
                            studentNumber: _studentNumber,
                            emailError: _serverErrorFor('Email'),
                            numberError: _serverErrorFor('StudentNumber'),
                            onSubmit: _next,
                          ),
                          _PasswordStep(
                            formKey: _keys[2],
                            password: _password,
                            confirm: _confirm,
                            show: _showPassword,
                            onToggle: () => setState(() => _showPassword = !_showPassword),
                            serverError: _serverErrorFor('Password'),
                            onSubmit: _next,
                          ),
                        ],
                      ),
                    ),
                    InkButton(
                      label: _step < 2 ? 'Continue' : 'Create my account',
                      icon: _step < 2 ? Icons.arrow_forward_rounded : Icons.check_rounded,
                      busy: _busy,
                      onPressed: _next,
                    ),
                    const SizedBox(height: 12),
                    Center(
                      child: TextButton(
                        onPressed: _busy ? null : () => context.go('/login'),
                        child: const Text('I already have an account'),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Three short bars; the current one is long and dark. Says where you are without a number.
class _Progress extends StatelessWidget {
  const _Progress({required this.step, required this.count});
  final int step;
  final int count;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        for (var i = 0; i < count; i++) ...[
          AnimatedContainer(
            duration: const Duration(milliseconds: 300),
            curve: Curves.easeOutCubic,
            width: i == step ? 28 : 10,
            height: 6,
            decoration: BoxDecoration(
              color: i <= step ? Brand.ink : Brand.line,
              borderRadius: BorderRadius.circular(999),
            ),
          ),
          if (i < count - 1) const SizedBox(width: 6),
        ],
      ],
    );
  }
}

class _NameStep extends StatelessWidget {
  const _NameStep({required this.formKey, required this.controller, required this.serverError, required this.onSubmit});
  final GlobalKey<FormState> formKey;
  final TextEditingController controller;
  final String? serverError;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return Form(
      key: formKey,
      child: TextFormField(
        controller: controller,
        autofocus: true,
        textCapitalization: TextCapitalization.words,
        textInputAction: TextInputAction.next,
        decoration: InputDecoration(labelText: 'Full name', errorText: serverError),
        validator: (v) => (v ?? '').trim().length < 2 ? 'Your name, as on your student card.' : null,
        onFieldSubmitted: (_) => onSubmit(),
      ),
    );
  }
}

class _ContactStep extends StatelessWidget {
  const _ContactStep({
    required this.formKey,
    required this.email,
    required this.studentNumber,
    required this.emailError,
    required this.numberError,
    required this.onSubmit,
  });
  final GlobalKey<FormState> formKey;
  final TextEditingController email;
  final TextEditingController studentNumber;
  final String? emailError;
  final String? numberError;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return Form(
      key: formKey,
      child: Column(
        children: [
          TextFormField(
            controller: email,
            keyboardType: TextInputType.emailAddress,
            autocorrect: false,
            textInputAction: TextInputAction.next,
            decoration: InputDecoration(labelText: 'Email', errorText: emailError),
            validator: (v) => !(v ?? '').contains('@') ? 'That does not look like an email address.' : null,
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: studentNumber,
            textCapitalization: TextCapitalization.characters,
            textInputAction: TextInputAction.done,
            decoration: InputDecoration(labelText: 'Student number (optional)', hintText: 'IT24101976', errorText: numberError),
            onFieldSubmitted: (_) => onSubmit(),
          ),
        ],
      ),
    );
  }
}

class _PasswordStep extends StatelessWidget {
  const _PasswordStep({
    required this.formKey,
    required this.password,
    required this.confirm,
    required this.show,
    required this.onToggle,
    required this.serverError,
    required this.onSubmit,
  });
  final GlobalKey<FormState> formKey;
  final TextEditingController password;
  final TextEditingController confirm;
  final bool show;
  final VoidCallback onToggle;
  final String? serverError;
  final VoidCallback onSubmit;

  // Mirrors the API's Identity policy so the person gets a fast, local answer.
  static final _policy = RegExp(r'^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$');

  @override
  Widget build(BuildContext context) {
    return Form(
      key: formKey,
      child: Column(
        children: [
          TextFormField(
            controller: password,
            obscureText: !show,
            textInputAction: TextInputAction.next,
            decoration: InputDecoration(
              labelText: 'Password',
              errorText: serverError,
              suffixIcon: IconButton(
                onPressed: onToggle,
                icon: Icon(show ? Icons.visibility_off_outlined : Icons.visibility_outlined),
                tooltip: show ? 'Hide password' : 'Show password',
              ),
            ),
            validator: (v) => _policy.hasMatch(v ?? '') ? null : 'Eight or more, with an uppercase letter, a lowercase letter and a digit.',
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: confirm,
            obscureText: !show,
            textInputAction: TextInputAction.done,
            decoration: const InputDecoration(labelText: 'Type it again'),
            validator: (v) => v != password.text ? 'These do not match.' : null,
            onFieldSubmitted: (_) => onSubmit(),
          ),
        ],
      ),
    );
  }
}
