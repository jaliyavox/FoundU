import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:foundu/features/notifications/data/notification_repository.dart';
import 'package:foundu/features/notifications/presentation/notifications_page.dart';

const claimId = '11111111-1111-1111-1111-111111111111';

class OneNotification extends NotificationRepository {
  OneNotification({this.entityType = 'Claim', this.type = 'ClaimApproved', this.title = 'Ready to collect'}) : super(Dio());

  final String entityType;
  final String type;
  final String title;

  @override
  Future<NotificationPage> getPage(int number) async => NotificationPage.fromJson({
        'items': [
          {
            'id': 'n1',
            'type': type,
            'title': title,
            'message': 'Visit the desk with your collection code.',
            'isRead': false,
            'relatedEntityType': entityType,
            'relatedEntityId': claimId,
            'createdAt': '2026-09-27T06:00:00Z',
          },
        ],
        'page': number,
        'totalPages': 1,
      });

  @override
  Future<int> getUnreadCount() async => 1;

  @override
  Future<void> markRead(String id) async {}

  @override
  Future<void> markAllRead() async {}
}

/// The app's real routing shape: tabs in a StatefulShellRoute, the notification inbox
/// outside it, detail pages on the root navigator. The older notifications test used a
/// flat router, which is why it never saw a tap stack a second copy of the whole shell - the
/// duplicate page key that crashed the app with '!keyReservation.contains(key)'.
void main() {
  Future<void> openFrom(WidgetTester tester, OneNotification repository, String destination) async {
    final rootKey = GlobalKey<NavigatorState>();
    final router = GoRouter(
      navigatorKey: rootKey,
      initialLocation: '/home',
      routes: [
        GoRoute(path: '/notifications', builder: (_, __) => const NotificationsPage()),
        StatefulShellRoute.indexedStack(
          builder: (context, state, shell) => Scaffold(body: shell),
          branches: [
            StatefulShellBranch(routes: [
              GoRoute(path: '/home', builder: (_, __) => const Text('Feed tab')),
            ]),
            StatefulShellBranch(routes: [
              GoRoute(path: '/reports', builder: (_, __) => const Text('Reports tab')),
            ]),
            StatefulShellBranch(routes: [
              GoRoute(
                path: '/claims',
                builder: (_, __) => const Text('Claims tab'),
                routes: [
                  GoRoute(
                    path: ':id',
                    parentNavigatorKey: rootKey,
                    builder: (_, __) => const Scaffold(body: Text('Claim destination')),
                  ),
                ],
              ),
            ]),
          ],
        ),
      ],
    );
    addTearDown(router.dispose);

    await tester.pumpWidget(ProviderScope(
      overrides: [notificationRepositoryProvider.overrideWithValue(repository)],
      child: MaterialApp.router(routerConfig: router),
    ));
    await tester.pumpAndSettle();

    // From a tab, open the inbox the way the bell does, then tap the notification.
    router.push('/notifications');
    await tester.pumpAndSettle();
    await tester.tap(find.text(repository.title));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.text(destination), findsOneWidget);
  }

  testWidgets('a claim notification opens the claim', (tester) async {
    await openFrom(tester, OneNotification(), 'Claim destination');
  });

  testWidgets('a report notification opens the reports tab', (tester) async {
    await openFrom(
      tester,
      OneNotification(entityType: 'LostReport', type: 'ItemReportedFound', title: 'Someone says they found your item'),
      'Reports tab',
    );
  });

  testWidgets('a found-item notification opens the feed', (tester) async {
    await openFrom(
      tester,
      OneNotification(entityType: 'FoundReport', type: 'FoundPostConfirmed', title: 'Thank you - the desk has it'),
      'Feed tab',
    );
  });
}
