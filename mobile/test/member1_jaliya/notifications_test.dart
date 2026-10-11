import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:foundu/core/auth/auth_session.dart';
import 'package:foundu/features/notifications/data/notification_repository.dart';
import 'package:foundu/features/notifications/presentation/notification_providers.dart';
import 'package:foundu/features/notifications/presentation/notifications_page.dart';

const entityId = '11111111-1111-1111-1111-111111111111';

Map<String, dynamic> notification(
        {String entityType = 'Claim',
        String? id = entityId,
        bool read = false}) =>
    {
      'id': 'notification-1',
      'type': 'ClaimApproved',
      'title': 'Ready to collect',
      'message': 'Visit the desk with your collection code.',
      'isRead': read,
      'relatedEntityType': entityType,
      'relatedEntityId': id,
      'createdAt': '2026-09-26T12:00:00Z',
    };

NotificationPage page(List<Map<String, dynamic>> items,
        {int number = 1, int total = 1}) =>
    NotificationPage.fromJson(
        {'items': items, 'page': number, 'totalPages': total});

class FakeNotifications extends NotificationRepository {
  FakeNotifications() : super(Dio());
  bool failRead = false;
  bool failList = false;
  bool empty = false;
  int unread = 1;
  final requestedPages = <int>[];
  final marked = <String>[];
  Completer<void>? pendingRead;

  @override
  Future<NotificationPage> getPage(int number) async {
    requestedPages.add(number);
    if (failList) throw StateError('private transport details');
    return page(empty ? [] : [notification(read: unread == 0)],
        number: number, total: empty ? 0 : 2);
  }

  @override
  Future<int> getUnreadCount() async => unread;

  @override
  Future<void> markRead(String id) async {
    if (failRead) throw StateError('offline');
    if (pendingRead != null) await pendingRead!.future;
    marked.add(id);
    unread = 0;
  }

  @override
  Future<void> markAllRead() async {
    if (failRead) throw StateError('offline');
    unread = 0;
  }
}

Future<GoRouter> mount(WidgetTester tester, FakeNotifications repository,
    {ProviderContainer? container}) async {
  final router = GoRouter(initialLocation: '/notifications', routes: [
    GoRoute(
        path: '/notifications', builder: (_, __) => const NotificationsPage()),
    GoRoute(
        path: '/claims/:id',
        builder: (_, __) => const Scaffold(body: Text('Claim destination'))),
  ]);
  addTearDown(router.dispose);
  final app = MaterialApp.router(routerConfig: router);
  await tester.pumpWidget(container == null
      ? ProviderScope(overrides: [
          notificationRepositoryProvider.overrideWithValue(repository)
        ], child: app)
      : UncontrolledProviderScope(container: container, child: app));
  await tester.pumpAndSettle();
  return router;
}

void main() {
  test('routes only known entities with valid IDs; finder replies use the feed',
      () {
    expect(AppNotification.fromJson(notification()).route, '/claims/$entityId');
    expect(
        AppNotification.fromJson(notification(entityType: 'FoundReport')).route,
        '/home');
    expect(
        AppNotification.fromJson(notification(entityType: 'MatchSuggestion'))
            .route,
        '/reports');
    expect(AppNotification.fromJson(notification(entityType: 'Unknown')).route,
        isNull);
    expect(AppNotification.fromJson(notification(id: '../../admin')).route,
        isNull);
    expect(AppNotification.fromJson(notification(id: null)).route, isNull);
    expect(
        AppNotification.fromJson({
          ...notification(entityType: 'LostReport'),
          'type': 'MessageReceived',
          'title': 'A reply from the owner'
        }).route,
        '/home');
  });

  testWidgets('marks a notification read before opening its claim',
      (tester) async {
    final repository = FakeNotifications();
    await mount(tester, repository);
    expect(find.text('1 unread'), findsOneWidget);
    await tester.tap(find.text('Ready to collect'));
    await tester.pumpAndSettle();
    expect(repository.marked, ['notification-1']);
    expect(find.text('Claim destination'), findsOneWidget);
  });

  testWidgets('read failure preserves unread state and allows retry',
      (tester) async {
    final repository = FakeNotifications()..failRead = true;
    await mount(tester, repository);
    await tester.tap(find.text('Ready to collect'));
    await tester.pumpAndSettle();
    expect(find.text('1 unread'), findsOneWidget);
    expect(find.text('Claim destination'), findsNothing);
    expect(find.text('Could not mark notifications as read. Please try again.'),
        findsOneWidget);
    repository.failRead = false;
    await tester.tap(find.text('Mark all read'));
    await tester.pumpAndSettle();
    expect(find.text('0 unread'), findsOneWidget);
  });

  testWidgets('supports pagination and mark all read', (tester) async {
    final repository = FakeNotifications();
    await mount(tester, repository);
    await tester.tap(find.text('Next'));
    await tester.pumpAndSettle();
    expect(find.text('Page 2 of 2'), findsOneWidget);
    expect(repository.requestedPages, [1, 2]);
    await tester.tap(find.text('Mark all read'));
    await tester.pumpAndSettle();
    expect(find.text('0 unread'), findsOneWidget);
    expect(find.bySemanticsLabel('Unread'), findsNothing);
  });

  testWidgets('list failure offers retry without exposing internal errors',
      (tester) async {
    final repository = FakeNotifications()..failList = true;
    await mount(tester, repository);
    expect(find.text('Could not load notifications.'), findsOneWidget);
    expect(find.text('private transport details'), findsNothing);
    repository
      ..failList = false
      ..empty = true;
    await tester.tap(find.text('Retry'));
    await tester.pumpAndSettle();
    expect(find.textContaining('You’re all caught up.'), findsOneWidget);
  });

  testWidgets('account switch discards a pending notification navigation',
      (tester) async {
    final repository = FakeNotifications()..pendingRead = Completer<void>();
    final container = ProviderContainer(overrides: [
      notificationRepositoryProvider.overrideWithValue(repository)
    ]);
    addTearDown(container.dispose);
    await mount(tester, repository, container: container);
    await tester.tap(find.text('Ready to collect'));
    await tester.pump();
    container.read(authSessionEpochProvider.notifier).advance();
    await tester.pump();
    repository.pendingRead!.complete();
    await tester.pumpAndSettle();
    expect(find.text('Claim destination'), findsNothing);
    await tester.pumpWidget(const SizedBox.shrink());
    container.dispose();
  });

  test(
      'late unread result from the previous account cannot replace the current count',
      () async {
    final repository = DeferredCounts();
    final container = ProviderContainer(overrides: [
      notificationRepositoryProvider.overrideWithValue(repository)
    ]);
    addTearDown(container.dispose);
    final subscription =
        container.listen(unreadNotificationsProvider, (_, __) {});
    addTearDown(subscription.close);
    container.read(authSessionEpochProvider.notifier).advance();
    final current = container.read(unreadNotificationsProvider.future);
    repository.newCount.complete(2);
    expect(await current, 2);
    repository.oldCount.complete(99);
    await Future<void>.delayed(Duration.zero);
    expect(container.read(unreadNotificationsProvider).requireValue, 2);
  });
}

class DeferredCounts extends FakeNotifications {
  final oldCount = Completer<int>();
  final newCount = Completer<int>();
  int calls = 0;
  @override
  Future<int> getUnreadCount() =>
      calls++ == 0 ? oldCount.future : newCount.future;
}
