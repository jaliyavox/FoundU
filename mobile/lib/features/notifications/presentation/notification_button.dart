import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'notification_providers.dart';

class NotificationButton extends ConsumerWidget {
  const NotificationButton({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unread = ref.watch(unreadNotificationsProvider).asData?.value;
    return IconButton(
      tooltip:
          unread == null ? 'Notifications' : 'Notifications, $unread unread',
      onPressed: () => context.push('/notifications'),
      icon: Badge(
        isLabelVisible: unread != null && unread > 0,
        label: Text(unread != null && unread > 99 ? '99+' : '${unread ?? 0}'),
        child: const Icon(Icons.notifications_outlined),
      ),
    );
  }
}
