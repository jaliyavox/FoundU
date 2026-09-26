import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/notifications/data/push_notification_manager.dart';

void main() {
  const id = '11111111-1111-1111-1111-111111111111';

  test('only recognized opaque notification data produces an authorized-app route', () {
    expect(
      PushNavigationIntent.fromData({'type': 'ClaimApproved', 'entityId': id})?.route,
      '/claims/$id',
    );
    expect(
      PushNavigationIntent.fromData({'type': 'MessageReceived', 'entityId': id})?.route,
      '/reports/$id',
    );
    expect(
      PushNavigationIntent.fromData({'type': 'PossibleMatchFound', 'entityId': id})?.route,
      '/reports',
    );
  });

  test('untrusted notification payload cannot select an arbitrary route', () {
    expect(PushNavigationIntent.fromData({'type': 'admin', 'entityId': id}), isNull);
    expect(PushNavigationIntent.fromData({'type': 'ClaimApproved', 'entityId': '/admin'}), isNull);
    expect(PushNavigationIntent.fromData({'type': 'ClaimApproved'}), isNull);
  });
}
