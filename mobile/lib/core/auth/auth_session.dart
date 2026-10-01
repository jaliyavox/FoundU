import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Changes whenever local credentials change so user-scoped providers do not retain another
/// account's list data or errors after logout/login.
final authSessionEpochProvider =
    NotifierProvider<AuthSessionEpoch, int>(AuthSessionEpoch.new);

class AuthSessionEpoch extends Notifier<int> {
  @override
  int build() => 0;

  void advance() => state++;
}
