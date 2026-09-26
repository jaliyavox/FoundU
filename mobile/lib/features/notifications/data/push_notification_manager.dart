import 'dart:async';

import 'package:dio/dio.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';

final pushNotificationManagerProvider = Provider<PushNotificationManager>((ref) {
  final manager = PushNotificationManager(ref.watch(apiClientProvider));
  ref.onDispose(manager.dispose);
  return manager;
});

/// Manages a device delivery token only. FoundU's backend remains responsible for deciding when
/// to send notifications and Flutter treats every push payload as non-authoritative navigation.
class PushNotificationManager {
  PushNotificationManager(this._api);

  final Dio _api;
  StreamSubscription<String>? _tokenRefreshSubscription;
  StreamSubscription<RemoteMessage>? _tapSubscription;
  final _navigationIntents = StreamController<PushNavigationIntent>.broadcast();
  String? _registeredToken;

  Stream<PushNavigationIntent> get navigationIntents => _navigationIntents.stream;

  Future<void> start() async {
    try {
      await FirebaseMessaging.instance.requestPermission();
      final token = await FirebaseMessaging.instance.getToken();
      if (token != null && token.isNotEmpty) await _register(token);
      _tokenRefreshSubscription ??= FirebaseMessaging.instance.onTokenRefresh.listen(_register);
      _tapSubscription ??= FirebaseMessaging.onMessageOpenedApp.listen(_emitNavigationIntent);
      final initial = await FirebaseMessaging.instance.getInitialMessage();
      if (initial != null) _emitNavigationIntent(initial);
    } on Object {
      // Push is a convenience integration. Login and all internal notifications remain usable if
      // Firebase has not yet been configured or the device is offline.
    }
  }

  Future<void> unregister() async {
    final token = _registeredToken;
    _registeredToken = null;
    if (token == null) return;
    try {
      await _api.post<void>('/api/device-registrations/unregister', data: {'token': token});
    } on DioException {
      // Local logout still proceeds; a subsequent account registration moves this token safely.
    }
  }

  Future<void> _register(String token) async {
    await _api.post<void>('/api/device-registrations', data: {
      'token': token,
      'platform': _platform,
    });
    _registeredToken = token;
  }

  String get _platform => switch (defaultTargetPlatform) {
        TargetPlatform.android => 'android',
        TargetPlatform.iOS => 'ios',
        _ => 'web',
      };

  void _emitNavigationIntent(RemoteMessage message) {
    final intent = PushNavigationIntent.fromData(message.data);
    if (intent != null) _navigationIntents.add(intent);
  }

  void dispose() {
    _tokenRefreshSubscription?.cancel();
    _tapSubscription?.cancel();
    _navigationIntents.close();
  }
}

class PushNavigationIntent {
  const PushNavigationIntent(this.route);

  final String route;

  static PushNavigationIntent? fromData(Map<String, dynamic> data) {
    final type = data['type'];
    final entityId = data['entityId'];
    if (entityId is! String || !_isGuid(entityId)) return null;
    return switch (type) {
      'ClaimApproved' ||
      'ClaimRejected' ||
      'RevisionRequested' ||
      'VerificationQuestionAvailable' ||
      'CollectionInstructions' =>
        PushNavigationIntent('/claims/$entityId'),
      'MessageReceived' || 'ItemReportedFound' =>
        PushNavigationIntent('/reports/$entityId'),
      // A possible-match notification names a MatchSuggestion, not a LostReport. Its detail is
      // deliberately loaded from the user's normal report list rather than trusting that ID.
      'PossibleMatchFound' => const PushNavigationIntent('/reports'),
      _ => null,
    };
  }

  static bool _isGuid(String value) => RegExp(
        r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
      ).hasMatch(value);
}
