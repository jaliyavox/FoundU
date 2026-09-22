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
import '../../features/feed/presentation/feed_page.dart';
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

  return GoRouter(
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

      // The signed-in app: four tabs under one floating nav, each with its own stack so
      // going back to a tab lands where you left it.
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => AppShell(navigationShell: shell),
        branches: [
          StatefulShellBranch(routes: [
            GoRoute(path: '/home', builder: (_, __) => const FeedPage()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/reports',
              builder: (_, __) => const MyReportsPage(),
              routes: [
                GoRoute(path: 'new', parentNavigatorKey: _rootKey, builder: (_, __) => const ReportFormPage()),
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
            GoRoute(path: '/profile', builder: (_, __) => const ProfilePage()),
          ]),
        ],
      ),
    ],
  );
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
