import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/auth_session.dart';
import '../data/notification_repository.dart';

final notificationsProvider =
    FutureProvider.autoDispose.family<NotificationPage, int>((ref, page) {
  ref.watch(authSessionEpochProvider);
  return ref.watch(notificationRepositoryProvider).getPage(page);
});

final unreadNotificationsProvider = FutureProvider.autoDispose<int>((ref) {
  ref.watch(authSessionEpochProvider);
  // Poll only while an inbox entry point is mounted. Never hold an old account's count.
  final timer = Timer(const Duration(seconds: 60), ref.invalidateSelf);
  ref.onDispose(timer.cancel);
  return ref.watch(notificationRepositoryProvider).getUnreadCount();
});
