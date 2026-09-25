class AuthUser {
  const AuthUser({
    required this.id,
    required this.name,
    required this.email,
    required this.role,
    this.studentNumber,
    this.isSuspended,
  });

  final String id;
  final String name;
  final String email;
  final String role;
  final String? studentNumber;
  final bool? isSuspended;

  factory AuthUser.fromAuthResponseJson(Map<String, dynamic> json) {
    return AuthUser(
      id: json['id'] as String,
      name: json['fullName'] as String,
      email: json['email'] as String,
      role: json['role'] as String,
      studentNumber: json['studentNumber'] as String?,
      isSuspended: json['isSuspended'] as bool,
    );
  }

  factory AuthUser.fromMeJson(Map<String, dynamic> json) {
    return AuthUser(
      id: json['id'] as String,
      name: json['name'] as String,
      email: json['email'] as String,
      role: json['role'] as String,
    );
  }
}

class AuthResponse {
  const AuthResponse({
    required this.accessToken,
    required this.accessTokenExpiresAtUtc,
    required this.refreshToken,
    required this.refreshTokenExpiresAtUtc,
    required this.user,
  });

  final String accessToken;
  final DateTime accessTokenExpiresAtUtc;
  final String refreshToken;
  final DateTime refreshTokenExpiresAtUtc;
  final AuthUser user;

  factory AuthResponse.fromJson(Map<String, dynamic> json) {
    return AuthResponse(
      accessToken: json['accessToken'] as String,
      accessTokenExpiresAtUtc:
          DateTime.parse(json['accessTokenExpiresAtUtc'] as String),
      refreshToken: json['refreshToken'] as String,
      refreshTokenExpiresAtUtc:
          DateTime.parse(json['refreshTokenExpiresAtUtc'] as String),
      user: AuthUser.fromAuthResponseJson(json['user'] as Map<String, dynamic>),
    );
  }
}
