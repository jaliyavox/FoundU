import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';

/// What the agent has understood so far. Sent back with every message - there is no server
/// session, so nothing is lost if the API restarts mid-conversation.
class IntakeSlots {
  const IntakeSlots({this.itemType, this.colour, this.location, this.when, this.distinctive, this.intent});

  final String? itemType;
  final String? colour;
  final String? location;
  final String? when;
  final String? distinctive;

  /// Which side of the counter: 'lost' (an owner looking - the default when unset) or
  /// 'found' (a finder holding someone else's item).
  final String? intent;

  bool get isFinder => intent == 'found';

  factory IntakeSlots.fromJson(Map<String, dynamic> json) => IntakeSlots(
        itemType: json['itemType'] as String?,
        colour: json['colour'] as String?,
        location: json['location'] as String?,
        when: json['when'] as String?,
        distinctive: json['distinctive'] as String?,
        intent: json['intent'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'itemType': itemType,
        'colour': colour,
        'location': location,
        'when': when,
        'distinctive': distinctive,
        'intent': intent,
      };
}

/// A lost report prefilled from the conversation - ids only where a name matched exactly.
class IntakeDraft {
  const IntakeDraft({
    this.categoryId,
    this.itemTypeId,
    this.locationId,
    required this.description,
    this.primaryColor,
  });

  final String? categoryId;
  final String? itemTypeId;
  final String? locationId;
  final String description;
  final String? primaryColor;

  factory IntakeDraft.fromJson(Map<String, dynamic> json) => IntakeDraft(
        categoryId: json['categoryId'] as String?,
        itemTypeId: json['itemTypeId'] as String?,
        locationId: json['locationId'] as String?,
        description: json['description'] as String? ?? '',
        primaryColor: json['primaryColor'] as String?,
      );
}

/// The one thing the agent pointed at. For an owner, a found item: kind "post" - a finder
/// still has it; "desk" - it is in custody and can be claimed. For a finder, kind "lost" - an
/// open lost report, the same one the feed shows.
class IntakeMatch {
  const IntakeMatch({
    required this.id,
    required this.kind,
    required this.itemType,
    required this.colour,
    required this.location,
    required this.description,
  });

  final String id;
  final String kind;
  final String itemType;
  final String? colour;
  final String location;
  final String description;

  bool get isAtDesk => kind == 'desk';
  bool get isLostReport => kind == 'lost';

  factory IntakeMatch.fromJson(Map<String, dynamic> json) => IntakeMatch(
        id: json['id'] as String,
        kind: json['kind'] as String,
        itemType: json['itemType'] as String,
        colour: json['colour'] as String?,
        location: json['location'] as String,
        description: json['description'] as String,
      );
}

/// One turn back. Phase is collecting, matched, no_match - or unavailable, where the reply
/// says so honestly and the draft is built from the person's own words.
class IntakeResponse {
  const IntakeResponse({
    required this.phase,
    required this.reply,
    required this.slots,
    required this.draft,
    required this.match,
  });

  final String phase;
  final String reply;
  final IntakeSlots slots;
  final IntakeDraft draft;
  final IntakeMatch? match;

  factory IntakeResponse.fromJson(Map<String, dynamic> json) => IntakeResponse(
        phase: json['phase'] as String,
        reply: json['reply'] as String,
        slots: IntakeSlots.fromJson(json['slots'] as Map<String, dynamic>),
        draft: IntakeDraft.fromJson(json['draft'] as Map<String, dynamic>),
        match: json['match'] == null ? null : IntakeMatch.fromJson(json['match'] as Map<String, dynamic>),
      );
}

class IntakeRepository {
  IntakeRepository(this._dio);

  final Dio _dio;

  Future<IntakeResponse> ask(String message, IntakeSlots? slots) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/intake',
        data: {'message': message, 'slots': slots?.toJson()},
      );
      return IntakeResponse.fromJson(response.data!);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }
}

final intakeRepositoryProvider =
    Provider<IntakeRepository>((ref) => IntakeRepository(ref.watch(apiClientProvider)));
