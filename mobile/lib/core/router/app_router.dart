import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/app_shell.dart';
import '../../features/auth/presentation/login_page.dart';
import '../../features/auth/presentation/profile_page.dart';
import '../../features/auth/presentation/register_page.dart';
import '../../features/auth/presentation/splash_page.dart';
import '../../features/claims/presentation/claim_detail_page.dart';
import '../../features/claims/presentation/claim_submission_page.dart';
import '../../features/claims/presentation/my_claims_page.dart';
import '../../features/notifications/data/push_notification_manager.dart';
import '../../features/notifications/presentation/notifications_page.dart';
import '../../features/feed/presentation/feed_page.dart';
import '../../features/feed/presentation/found_board_page.dart';
import '../../features/intake/data/intake_repository.dart';
import '../../features/intake/presentation/ask_foundu_page.dart';
import '../../features/account/presentation/account_page.dart';
import '../../features/help/presentation/help_to_find_page.dart';
import '../../features/support/presentation/support_assistant_page.dart';
import '../../features/support/presentation/support_page.dart';
import '../../features/support/presentation/ticket_page.dart';
import '../../features/feed/presentation/post_found_page.dart';
import '../../features/reports/presentation/my_reports_page.dart';
import '../../features/reports/presentation/possible_matches_page.dart';
import '../../features/reports/presentation/report_detail_page.dart';
import '../../features/reports/presentation/report_form_page.dart';
import '../auth/auth_controller.dart';

final _rootKey = GlobalKey<NavigatorState>();

final appRouterProvider = Provider<GoRouter>((ref) {
  final refresh = _RouterRefreshNotifier();
  ref.onDispose(refresh.dispose);
  ref.listen(authControllerProvider, (_, __) => refresh.notify());

  final router = GoRouter(
    navigatorKey: _rootKey,
    initialLocation: '/splash',
    refreshListenable: refresh,
    redirect: (context, state) {
      final auth = ref.read(authControllerProvider);
      return authRedirect(
        isLoading: auth.isLoading,
        isAuthenticated: auth.value != null,
        location: state.matchedLocation,
      );
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, __) => const SplashPage()),
      GoRoute(path: '/login', builder: (_, __) => const LoginPage()),
      GoRoute(path: '/register', builder: (_, __) => const RegisterPage()),
      GoRoute(path: '/notifications', builder: (_, __) => const NotificationsPage()),
      // Full screen, over the tabs: a conversation needs the keyboard and the whole height.
      GoRoute(
        path: '/ask',
        builder: (_, state) => AskFoundUPage(initialQuestion: state.extra is String ? state.extra as String : null),
      ),

      // The signed-in app: four tabs under one floating nav, each with its own stack so
      // going back to a tab lands where you left it.
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => AppShell(navigationShell: shell),
        branches: [
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/home',
              builder: (_, __) => const FeedPage(),
              routes: [
                // Ask FoundU hands a finder's draft over as `extra`; anywhere else opens it blank.
                GoRoute(
                  path: 'found/new',
                  parentNavigatorKey: _rootKey,
                  builder: (_, state) => PostFoundPage(draft: state.extra is IntakeDraft ? state.extra as IntakeDraft : null),
                ),
                // The board behind the "Fresh finds" strip.
                GoRoute(path: 'found', parentNavigatorKey: _rootKey, builder: (_, __) => const FoundBoardPage()),
              ],
            ),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/reports',
              builder: (_, __) => const MyReportsPage(),
              routes: [
                GoRoute(
                  path: 'new',
                  parentNavigatorKey: _rootKey,
                  // Ask FoundU hands its draft over as `extra`; anywhere else opens it blank.
                  builder: (_, state) => ReportFormPage(draft: state.extra is IntakeDraft ? state.extra as IntakeDraft : null),
                ),
                GoRoute(
                  path: ':id',
                  parentNavigatorKey: _rootKey,
                  builder: (_, state) => LostReportDetailPage(reportId: state.pathParameters['id']!),
                  routes: [
                    GoRoute(
                      path: 'edit',
                      parentNavigatorKey: _rootKey,
                      builder: (_, state) => ReportFormPage(reportId: state.pathParameters['id']!),
                    ),
                    GoRoute(
                      path: 'matches',
                      parentNavigatorKey: _rootKey,
                      builder: (_, state) => PossibleMatchesPage(reportId: state.pathParameters['id']!),
                    ),
                  ],
                ),
              ],
            ),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/claims',
              builder: (_, __) => const MyClaimsPage(),
              routes: [
                GoRoute(
                  path: 'new',
                  parentNavigatorKey: _rootKey,
                  builder: (_, state) => claimSubmissionRoutePage(state.uri),
                ),
                GoRoute(
                  path: ':id',
                  parentNavigatorKey: _rootKey,
                  builder: (_, state) => ClaimDetailPage(claimId: state.pathParameters['id']!),
                ),
              ],
            ),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/profile',
              builder: (_, __) => const ProfilePage(),
              routes: [
                GoRoute(path: 'help-to-find', parentNavigatorKey: _rootKey, builder: (_, __) => const HelpToFindPage()),
                GoRoute(path: 'account', parentNavigatorKey: _rootKey, builder: (_, __) => const AccountPage()),
                GoRoute(
                  path: 'support',
                  parentNavigatorKey: _rootKey,
                  builder: (_, __) => const SupportPage(),
                  routes: [
                    // Before ':ticketId', or "assistant" would be read as a ticket id.
                    GoRoute(
                      path: 'assistant',
                      parentNavigatorKey: _rootKey,
                      builder: (_, __) => const SupportAssistantPage(),
                    ),
                    GoRoute(
                      path: ':ticketId',
                      parentNavigatorKey: _rootKey,
                      builder: (_, state) => TicketPage(ticketId: state.pathParameters['ticketId']!),
                    ),
                  ],
                ),
              ],
            ),
          ]),
        ],
      ),
    ],
  );
  final subscription = ref.read(pushNotificationManagerProvider).navigationIntents.listen((intent) {
    // Route only after authentication; selected data is always reloaded from FoundU API.
    if (ref.read(authControllerProvider).value != null) router.go(intent.route);
  });
  ref.onDispose(subscription.cancel);
  return router;
});

String? authRedirect({
  required bool isLoading,
  required bool isAuthenticated,
  required String location,
}) {
  const public = {'/login', '/register'};

  if (isLoading) {
    if (public.contains(location) || location == '/home') return null;
    return location == '/splash' ? null : '/splash';
  }

  if (!isAuthenticated) return public.contains(location) ? null : '/login';
  if (public.contains(location) || location == '/splash') return '/home';
  return null;
}

class _RouterRefreshNotifier extends ChangeNotifier {
  void notify() => notifyListeners();
}
