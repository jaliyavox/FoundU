import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/presentation/home_page.dart';
import '../../features/auth/presentation/login_page.dart';
import '../../features/auth/presentation/splash_page.dart';
import '../../features/claims/presentation/claim_detail_page.dart';
import '../../features/claims/presentation/claim_submission_page.dart';
import '../../features/claims/presentation/my_claims_page.dart';
import '../../features/reports/presentation/my_reports_page.dart';
import '../../features/reports/presentation/possible_matches_page.dart';
import '../../features/reports/presentation/report_detail_page.dart';
import '../../features/reports/presentation/report_form_page.dart';
import '../auth/auth_controller.dart';

final appRouterProvider = Provider<GoRouter>((ref) {
  final refresh = _RouterRefreshNotifier();
  ref.onDispose(refresh.dispose);
  ref.listen(authControllerProvider, (_, __) => refresh.notify());

  return GoRouter(
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
      GoRoute(path: '/home', builder: (_, __) => const HomePage()),
      GoRoute(path: '/reports', builder: (_, __) => const MyReportsPage()),
      GoRoute(path: '/claims', builder: (_, __) => const MyClaimsPage()),
      GoRoute(
        path: '/claims/new',
        builder: (_, state) => claimSubmissionRoutePage(state.uri),
      ),
      GoRoute(
        path: '/claims/:id',
        builder: (_, state) =>
            ClaimDetailPage(claimId: state.pathParameters['id']!),
      ),
      GoRoute(path: '/reports/new', builder: (_, __) => const ReportFormPage()),
      GoRoute(
        path: '/reports/:id',
        builder: (_, state) => LostReportDetailPage(
          reportId: state.pathParameters['id']!,
        ),
      ),
      GoRoute(
        path: '/reports/:id/edit',
        builder: (_, state) => ReportFormPage(
          reportId: state.pathParameters['id']!,
        ),
      ),
      GoRoute(
        path: '/reports/:id/matches',
        builder: (_, state) => PossibleMatchesPage(
          reportId: state.pathParameters['id']!,
        ),
      ),
    ],
  );
});

String? authRedirect({
  required bool isLoading,
  required bool isAuthenticated,
  required String location,
}) {
  if (isLoading) {
    if (location == '/login' || location == '/home') return null;
    return location == '/splash' ? null : '/splash';
  }

  if (!isAuthenticated) return location == '/login' ? null : '/login';
  if (location == '/login' || location == '/splash') return '/home';
  return null;
}

class _RouterRefreshNotifier extends ChangeNotifier {
  void notify() => notifyListeners();
}
