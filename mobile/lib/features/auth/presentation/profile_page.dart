import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';
import '../../notifications/presentation/notification_button.dart';

/// Who is signed in, and the way out. Deliberately small - the app's work happens on the
/// other tabs.
class ProfilePage extends ConsumerWidget {
  const ProfilePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final user = auth.value;
    final text = Theme.of(context).textTheme;
    final initials = (user?.name ?? '?').trim().split(RegExp(r'\s+')).take(2).map((p) => p.isEmpty ? '' : p[0]).join().toUpperCase();

    return Scaffold(
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 120),
          children: [
            Row(children: [
              Expanded(child: Text('Profile', style: text.headlineSmall)),
              const NotificationButton(),
            ]),
            const SizedBox(height: 18),
            Panel(
              color: Brand.ink,
              child: Row(
                children: [
                  Container(
                    width: 56,
                    height: 56,
                    decoration: const BoxDecoration(color: Brand.green, shape: BoxShape.circle),
                    alignment: Alignment.center,
                    child: Text(initials, style: const TextStyle(color: Brand.ink, fontWeight: FontWeight.w700, fontSize: 18)),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(user?.name ?? '', style: text.titleMedium?.copyWith(color: Colors.white)),
                        const SizedBox(height: 2),
                        Text(user?.email ?? '', style: text.bodySmall?.copyWith(color: Colors.white70)),
                        if (user?.studentNumber != null) ...[
                          const SizedBox(height: 2),
                          Text(user!.studentNumber!, style: text.bodySmall?.copyWith(color: Colors.white54)),
                        ],
                      ],
                    ),
                  ),
                  StatusChip(user?.role ?? '', tone: ChipTone.good),
                ],
              ),
            ),
            const SizedBox(height: 14),
            // The four things a student comes to their profile for, in one panel.
            Panel(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  _Link(
                    icon: Icons.volunteer_activism_outlined,
                    title: 'Help to find',
                    subtitle: 'Your finding activity, desk codes and honor points',
                    onTap: () => context.push('/profile/help-to-find'),
                  ),
                  const Divider(),
                  _Link(
                    icon: Icons.auto_awesome_outlined,
                    title: 'Ask FoundU',
                    subtitle: 'Describe what you lost and check what has been handed in',
                    onTap: () => context.push('/ask'),
                  ),
                  const Divider(),
                  _Link(
                    icon: Icons.manage_accounts_outlined,
                    title: 'Account settings',
                    subtitle: 'Name, email and password',
                    onTap: () => context.push('/profile/account'),
                  ),
                  const Divider(),
                  _Link(
                    icon: Icons.support_agent_outlined,
                    title: 'Help & support',
                    subtitle: 'Ask the desk when something is stuck',
                    onTap: () => context.push('/profile/support'),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 14),
            const Panel(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  _Row(icon: Icons.info_outline_rounded, title: 'How verification works', body: 'When staff match an item to your report, you answer a question only the owner could - the desk never shows you the answer.'),
                  Divider(),
                  _Row(icon: Icons.shield_outlined, title: 'Your details stay private', body: 'The feed shows your name only. Finders never see your email or student number.'),
                ],
              ),
            ),
            const SizedBox(height: 22),
            OutlinedButton.icon(
              onPressed: auth.isLoading ? null : () => ref.read(authControllerProvider.notifier).logout(),
              icon: const Icon(Icons.logout_rounded, size: 18),
              label: const Text('Sign out'),
            ),
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({required this.icon, required this.title, required this.body});
  final IconData icon;
  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: const BoxDecoration(color: Brand.mist, shape: BoxShape.circle),
            child: Icon(icon, size: 18, color: Brand.forest),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: text.bodyMedium?.copyWith(fontWeight: FontWeight.w600)),
                const SizedBox(height: 2),
                Text(body, style: text.bodySmall?.copyWith(color: Brand.muted, height: 1.4)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Link extends StatelessWidget {
  const _Link({required this.icon, required this.title, required this.subtitle, required this.onTap});

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: 18, vertical: 4),
      leading: Icon(icon, color: Brand.forest),
      title: Text(title),
      subtitle: Text(subtitle),
      trailing: const Icon(Icons.chevron_right_rounded, color: Brand.faint),
      onTap: onTap,
    );
  }
}
