import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/auth/data/auth_models.dart';

void main() {
  test('parses the API auth response contract', () {
    final response = AuthResponse.fromJson({
      'accessToken': 'access',
      'accessTokenExpiresAtUtc': '2026-09-09T12:00:00Z',
      'refreshToken': 'refresh',
      'refreshTokenExpiresAtUtc': '2026-09-23T12:00:00Z',
      'user': {
        'id': 'f98bfab0-1ef5-48e7-b81e-d4bf1a651487',
        'fullName': 'Jane Student',
        'email': 'jane@foundu.test',
        'role': 'Student',
        'studentNumber': 'STU-1',
        'isSuspended': false,
      },
    });

    expect(response.accessToken, 'access');
    expect(response.refreshToken, 'refresh');
    expect(response.user.name, 'Jane Student');
    expect(response.user.role, 'Student');
    expect(response.user.studentNumber, 'STU-1');
    expect(response.user.isSuspended, isFalse);
  });

  test('parses the smaller me endpoint contract', () {
    final user = AuthUser.fromMeJson({
      'id': 'f98bfab0-1ef5-48e7-b81e-d4bf1a651487',
      'email': 'jane@foundu.test',
      'name': 'Jane Student',
      'role': 'Student',
    });

    expect(user.name, 'Jane Student');
    expect(user.email, 'jane@foundu.test');
    expect(user.studentNumber, isNull);
  });
}
