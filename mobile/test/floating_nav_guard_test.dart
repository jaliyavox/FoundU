import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// The floating nav sits over every tab page. A bottom sheet opened from a tab lives inside
/// that tab unless it asks for the root navigator - and then the nav covers its bottom, which
/// is where the buttons are. This reads every call in lib/ so a new sheet cannot quietly
/// forget, the way the found-post sheet's "Is this yours?" panel did.
void main() {
  test('every bottom sheet opens above the floating nav', () {
    final offenders = <String>[];

    for (final file in Directory('lib').listSync(recursive: true).whereType<File>()) {
      if (!file.path.endsWith('.dart')) continue;
      final source = file.readAsStringSync();

      for (final match in RegExp(r'showModalBottomSheet\s*(<[^>]*>)?\s*\(').allMatches(source)) {
        // The arguments of this call, up to its matching closing parenthesis.
        var depth = 0;
        var end = match.end - 1;
        for (var i = match.end - 1; i < source.length; i++) {
          if (source[i] == '(') depth++;
          if (source[i] == ')' && --depth == 0) {
            end = i;
            break;
          }
        }
        final args = source.substring(match.end, end);
        if (!RegExp(r'useRootNavigator\s*:\s*true').hasMatch(args)) {
          final line = '\n'.allMatches(source.substring(0, match.start)).length + 1;
          offenders.add('${file.path}:$line');
        }
      }
    }

    expect(offenders, isEmpty, reason: 'These sheets would open underneath the floating nav:\n${offenders.join('\n')}');
  });
}
