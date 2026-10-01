import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';

final notificationRepositoryProvider = Provider<NotificationRepository>(
  (ref) => NotificationRepository(ref.watch(apiClientProvider)),
);

class AppNotification {
  AppNotification.fromJson(Map<String, dynamic> json)
      : id = json['id'] as String,
        type = json['type'] as String,
        title = json['title'] as String,
        message = json['message'] as String,
        isRead = json['isRead'] as bool,
        entityType = json['relatedEntityType'] as String?,
        entityId = json['relatedEntityId'] as String?,
        createdAt = DateTime.parse(json['createdAt'] as String);

  final String id, type, title, message;
  final bool isRead;
  final String? entityType, entityId;
  final DateTime createdAt;

  // Never accept a URL from a notification. The destination still fetches authorized data.
  String? get route {
    final id = entityId;
    if (id == null ||
        !RegExp(r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$')
            .hasMatch(id)) {
      return null;
    }
    // These go to the finder but name the owner's report, which the finder cannot open.
    if (type == 'FoundPostConfirmed' || type == 'ItemReturnedToOwner') return '/home';
    return switch (entityType) {
      'Claim' => '/claims/$id',
      'SupportTicket' => '/profile/support/$id',
      'FoundReport' => '/home',
      // Finder replies belong to a feed thread, not a report owned by someone else.
      'LostReport' ||
      'MatchSuggestion' =>
        type == 'MessageReceived' && title.startsWith('A reply')
            ? '/home'
            : '/reports',
      _ => null,
    };
  }
}

class NotificationPage {
  NotificationPage.fromJson(Map<String, dynamic> json)
      : items = (json['items'] as List)
            .map((item) =>
                AppNotification.fromJson(item as Map<String, dynamic>))
            .toList(),
        page = json['page'] as int,
        totalPages = json['totalPages'] as int;

  final List<AppNotification> items;
  final int page, totalPages;
}

class NotificationRepository {
  NotificationRepository(this._dio);
  final Dio _dio;

  Future<T> _request<T>(Future<T> Function() action) async {
    try {
      return await action();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<NotificationPage> getPage(int page) => _request(() async {
        final response = await _dio.get<Map<String, dynamic>>(
          '/api/notifications',
          queryParameters: {'page': page, 'pageSize': 20},
        );
        return NotificationPage.fromJson(response.data!);
      });

  Future<int> getUnreadCount() => _request(() async {
        final response = await _dio
            .get<Map<String, dynamic>>('/api/notifications/unread-count');
        return response.data!['unread'] as int;
      });

  Future<void> markRead(String id) => _request(() async {
        await _dio.post<void>('/api/notifications/$id/read');
      });

  Future<void> markAllRead() => _request(() async {
        await _dio.post<void>('/api/notifications/read-all');
      });
}
