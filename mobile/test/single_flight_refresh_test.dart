import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/api/auth_interceptor.dart';

void main() {
  test('concurrent refresh calls share one in-flight operation', () async {
    final coordinator = SingleFlightRefresh();
    final completer = Completer<bool>();
    var calls = 0;

    Future<bool> refresh() {
      calls++;
      return completer.future;
    }

    final first = coordinator.run(refresh);
    final second = coordinator.run(refresh);

    expect(calls, 1);
    expect(identical(first, second), isTrue);

    completer.complete(true);
    expect(await Future.wait([first, second]), [true, true]);
  });
}
