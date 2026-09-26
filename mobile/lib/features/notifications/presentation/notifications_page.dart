import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/auth/auth_session.dart';
import '../data/notification_repository.dart';
import 'notification_providers.dart';

class NotificationsPage extends ConsumerStatefulWidget {
  const NotificationsPage({super.key});

  @override
  ConsumerState<NotificationsPage> createState() => _NotificationsPageState();
}

class _NotificationsPageState extends ConsumerState<NotificationsPage> {
  int _page = 1;
  bool _saving = false;

  Future<void> _refresh() async {
    ref.invalidate(unreadNotificationsProvider);
    ref.invalidate(notificationsProvider(_page));
    try {
      await ref.read(notificationsProvider(_page).future);
    } catch (_) {
      // The page exposes the failure and its retry action.
    }
  }

  Future<void> _read([AppNotification? notification]) async {
    if (_saving) return;
    final epoch = ref.read(authSessionEpochProvider);
    setState(() => _saving = true);
    try {
      final repository = ref.read(notificationRepositoryProvider);
      if (notification == null) {
        await repository.markAllRead();
      } else if (!notification.isRead) {
        await repository.markRead(notification.id);
      }
      if (!mounted || epoch != ref.read(authSessionEpochProvider)) return;
      ref.invalidate(notificationsProvider);
      ref.invalidate(unreadNotificationsProvider);
      final route = notification?.route;
      if (route != null) context.push(route);
    } catch (_) {
      if (!mounted || epoch != ref.read(authSessionEpochProvider)) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
            content: Text(
                'Could not mark notifications as read. Please try again.')),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(authSessionEpochProvider, (_, __) {
      setState(() => _page = 1);
    });
    final notifications = ref.watch(notificationsProvider(_page));
    final unread = ref.watch(unreadNotificationsProvider).asData?.value;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: [
          IconButton(
              tooltip: 'Refresh notifications',
              onPressed: _saving ? null : _refresh,
              icon: const Icon(Icons.refresh)),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Row(children: [
              Expanded(
                  child: Semantics(
                      liveRegion: true,
                      child: Text(
                          unread == null ? 'Your updates' : '$unread unread'))),
              TextButton(
                  onPressed: _saving || unread == 0 ? null : () => _read(),
                  child: const Text('Mark all read')),
            ]),
          ),
          if (_saving)
            const LinearProgressIndicator(
                semanticsLabel: 'Updating notifications'),
          Expanded(
            child: notifications.when(
              skipLoadingOnRefresh: false,
              skipLoadingOnReload: false,
              loading: () => const Center(
                  child: CircularProgressIndicator(
                      semanticsLabel: 'Loading notifications')),
              error: (_, __) => Center(
                  child: Column(mainAxisSize: MainAxisSize.min, children: [
                const Text('Could not load notifications.'),
                const Text('Check your connection and try again.'),
                TextButton(onPressed: _refresh, child: const Text('Retry')),
              ])),
              data: (page) => RefreshIndicator(
                onRefresh: _refresh,
                child: ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
                  children: [
                    if (page.items.isEmpty)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 64),
                        child: Text(
                            'You’re all caught up. Matches, messages and claim updates will appear here.',
                            textAlign: TextAlign.center),
                      ),
                    for (final item in page.items)
                      Card(
                          child: ListTile(
                        enabled: !_saving,
                        onTap: () => _read(item),
                        leading: Icon(
                            item.isRead
                                ? Icons.notifications_none
                                : Icons.notifications_active_outlined,
                            semanticLabel: item.isRead ? 'Read' : 'Unread'),
                        title: Text(item.title,
                            style: TextStyle(
                                fontWeight: item.isRead
                                    ? FontWeight.normal
                                    : FontWeight.bold)),
                        subtitle: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(item.message),
                              const SizedBox(height: 6),
                              Text(
                                  DateFormat.yMMMd()
                                      .add_jm()
                                      .format(item.createdAt.toLocal()),
                                  style: Theme.of(context).textTheme.bodySmall),
                            ]),
                        trailing: item.route == null
                            ? null
                            : const Icon(Icons.chevron_right),
                      )),
                    if (page.totalPages > 1 || _page > 1)
                      Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            TextButton(
                                onPressed: _page > 1 && !_saving
                                    ? () => setState(() => _page--)
                                    : null,
                                child: const Text('Previous')),
                            Text('Page $_page of ${page.totalPages}'),
                            TextButton(
                                onPressed: _page < page.totalPages && !_saving
                                    ? () => setState(() => _page++)
                                    : null,
                                child: const Text('Next')),
                          ]),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
