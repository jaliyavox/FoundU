import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/router/app_router.dart';

void main() {
  test('unauthenticated users are redirected to login', () {
    expect(
      authRedirect(
        isLoading: false,
        isAuthenticated: false,
        location: '/future-protected-route',
      ),
      '/login',
    );
  });

  test('authenticated users leave auth-entry routes for home', () {
    expect(
      authRedirect(
        isLoading: false,
        isAuthenticated: true,
        location: '/login',
      ),
      '/home',
    );
    expect(
      authRedirect(
        isLoading: false,
        isAuthenticated: true,
        location: '/splash',
      ),
      '/home',
    );
  });

  test('authenticated users remain on future protected routes', () {
    expect(
      authRedirect(
        isLoading: false,
        isAuthenticated: true,
        location: '/future-protected-route',
      ),
      isNull,
    );
  });

  test('a signed-out user can reach forgot password, and a signed-in one is sent home', () {
    expect(authRedirect(isLoading: false, isAuthenticated: false, location: '/forgot-password'), isNull);
    expect(authRedirect(isLoading: false, isAuthenticated: true, location: '/forgot-password'), '/home');
  });
}
