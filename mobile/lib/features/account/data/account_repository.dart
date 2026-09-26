import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';
import '../../auth/data/auth_models.dart';

/// Mirrors FoundU.Application.Auth.Dtos.ProfileDto.
class Profile {
  const Profile({
    required this.id,
    required this.fullName,
    required this.email,
    required this.role,
    required this.studentNumber,
    required this.hasPassword,
    required this.isGoogleLinked,
  });

  final String id;
  final String fullName;
  final String email;
  final String role;
  final String? studentNumber;

  /// False for an account created through Google that has never set one.
  final bool hasPassword;
  final bool isGoogleLinked;

  factory Profile.fromJson(Map<String, dynamic> json) => Profile(
        id: json['id'] as String,
        fullName: json['fullName'] as String,
        email: json['email'] as String,
        role: json['role'] as String,
        studentNumber: json['studentNumber'] as String?,
        hasPassword: json['hasPassword'] as bool? ?? true,
        isGoogleLinked: json['isGoogleLinked'] as bool? ?? false,
      );
}

class AccountRepository {
  AccountRepository(this._dio);

  final Dio _dio;

  Future<Profile> getProfile() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/profile');
      return Profile.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  /// [currentPassword] is required by the API when the email address is changing.
  Future<Profile> updateProfile({
    required String fullName,
    required String email,
    String? studentNumber,
    String? currentPassword,
  }) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>('/api/profile', data: {
        'fullName': fullName,
        'email': email,
        'studentNumber': studentNumber,
        if (currentPassword != null && currentPassword.isNotEmpty) 'currentPassword': currentPassword,
      });
      return Profile.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  /// Comes back with a fresh token pair: the change signs out every other device, and this
  /// one would go with them otherwise.
  Future<AuthResponse> changePassword({String? currentPassword, required String newPassword}) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>('/api/profile/password', data: {
        if (currentPassword != null && currentPassword.isNotEmpty) 'currentPassword': currentPassword,
        'newPassword': newPassword,
      });
      return AuthResponse.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }
}

final accountRepositoryProvider =
    Provider<AccountRepository>((ref) => AccountRepository(ref.watch(apiClientProvider)));

final profileProvider = FutureProvider.autoDispose<Profile>(
  (ref) => ref.watch(accountRepositoryProvider).getProfile(),
);
