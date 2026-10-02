import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';

/// Category names are the wire values; people see these labels instead.
const ticketCategories = <String, String>{
  'Account': 'My account',
  'LostReport': 'A lost report',
  'FoundItem': 'Something I found',
  'Claim': 'A claim',
  'Collection': 'Collecting an item',
  'Technical': 'Something is broken',
  'Other': 'Something else',
};

const ticketStatusLabels = <String, String>{
  'Open': 'With us',
  'Waiting': 'Waiting on you',
  'Resolved': 'Resolved',
  'Closed': 'Closed',
};

class TicketMessage {
  const TicketMessage({
    required this.id,
    required this.senderName,
    required this.isMine,
    required this.isStaffReply,
    required this.body,
    required this.createdAt,
  });

  final String id;
  final String senderName;
  final bool isMine;
  final bool isStaffReply;
  final String body;
  final DateTime createdAt;

  factory TicketMessage.fromJson(Map<String, dynamic> json) => TicketMessage(
        id: json['id'] as String,
        senderName: json['senderName'] as String,
        isMine: json['isMine'] as bool? ?? false,
        isStaffReply: json['isStaffReply'] as bool? ?? false,
        body: json['body'] as String,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}

class TicketSummary {
  const TicketSummary({
    required this.id,
    required this.subject,
    required this.category,
    required this.status,
    required this.assignedToName,
    required this.unreadCount,
    required this.lastActivityAt,
  });

  final String id;
  final String subject;
  final String category;
  final String status;
  final String? assignedToName;

  /// Only the desk's messages count here - nobody is nagged about their own words.
  final int unreadCount;
  final DateTime lastActivityAt;

  factory TicketSummary.fromJson(Map<String, dynamic> json) => TicketSummary(
        id: json['id'] as String,
        subject: json['subject'] as String,
        category: json['category'] as String,
        status: json['status'] as String,
        assignedToName: json['assignedToName'] as String?,
        unreadCount: json['unreadCount'] as int? ?? 0,
        lastActivityAt: DateTime.parse(json['lastActivityAt'] as String),
      );
}

class TicketDetail {
  const TicketDetail({
    required this.id,
    required this.subject,
    required this.category,
    required this.status,
    required this.createdAt,
    required this.messages,
  });

  final String id;
  final String subject;
  final String category;
  final String status;
  final DateTime createdAt;
  final List<TicketMessage> messages;

  bool get isClosed => status == 'Closed';

  factory TicketDetail.fromJson(Map<String, dynamic> json) => TicketDetail(
        id: json['id'] as String,
        subject: json['subject'] as String,
        category: json['category'] as String,
        status: json['status'] as String,
        createdAt: DateTime.parse(json['createdAt'] as String),
        messages: ((json['messages'] as List<dynamic>?) ?? const [])
            .map((m) => TicketMessage.fromJson(m as Map<String, dynamic>))
            .toList(),
      );
}

class AssistantTurn {
  const AssistantTurn({required this.fromMe, required this.text});
  final bool fromMe;
  final String text;

  Map<String, dynamic> toJson() => {'role': fromMe ? 'user' : 'assistant', 'text': text};
}

/// A ticket the assistant prepared. The person reads it and sends it - it is never sent for them.
class TicketDraft {
  const TicketDraft({required this.subject, required this.category, required this.body});
  final String subject;
  final String category;
  final String body;

  factory TicketDraft.fromJson(Map<String, dynamic> json) => TicketDraft(
        subject: json['subject'] as String,
        category: json['category'] as String,
        body: json['body'] as String,
      );
}

/// answered - a help-guide entry fits; clarify - it asks for more; escalate - it cannot fix
/// this and [ticket] is a draft; unavailable - the AI is down and the draft is the person's
/// own words.
class AssistantResponse {
  const AssistantResponse({required this.phase, required this.reply, this.topic, this.ticket});
  final String phase;
  final String reply;
  final String? topic;
  final TicketDraft? ticket;

  factory AssistantResponse.fromJson(Map<String, dynamic> json) => AssistantResponse(
        phase: json['phase'] as String,
        reply: json['reply'] as String,
        topic: json['topic'] as String?,
        ticket: json['ticket'] == null ? null : TicketDraft.fromJson(json['ticket'] as Map<String, dynamic>),
      );
}

class SupportRepository {
  SupportRepository(this._dio);

  final Dio _dio;

  Future<List<TicketSummary>> mine() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/api/support/tickets',
        queryParameters: {'page': 1, 'pageSize': 50},
      );
      return ((response.data!['items'] as List<dynamic>?) ?? const [])
          .map((t) => TicketSummary.fromJson(t as Map<String, dynamic>))
          .toList();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<TicketDetail> get(String id) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/support/tickets/$id');
      return TicketDetail.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  /// [viaAssistant] marks a ticket sent from the support assistant's draft, so the desk knows
  /// what was already suggested.
  Future<TicketDetail> open({
    required String subject,
    required String category,
    required String body,
    bool viaAssistant = false,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/support/tickets',
        data: {'subject': subject, 'category': category, 'body': body, 'viaAssistant': viaAssistant},
      );
      return TicketDetail.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  /// One message to the support assistant, with what was said before it. The greeting the
  /// page shows is not part of [history].
  Future<AssistantResponse> ask(String message, List<AssistantTurn> history, String? lastTopic) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/support/assistant',
        data: {
          'message': message,
          'history': [for (final turn in history) turn.toJson()],
          'lastTopic': lastTopic,
        },
      );
      return AssistantResponse.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<TicketDetail> reply(String id, String body) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>('/api/support/tickets/$id/messages', data: {'body': body});
      return TicketDetail.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }
}

final supportRepositoryProvider =
    Provider<SupportRepository>((ref) => SupportRepository(ref.watch(apiClientProvider)));

final myTicketsProvider = FutureProvider.autoDispose<List<TicketSummary>>(
  (ref) => ref.watch(supportRepositoryProvider).mine(),
);

/// Reading a ticket marks the desk's messages seen, so it is fetched fresh each time.
final ticketProvider = FutureProvider.autoDispose.family<TicketDetail, String>(
  (ref, id) => ref.watch(supportRepositoryProvider).get(id),
);
