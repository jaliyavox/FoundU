import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/reports/presentation/report_stage_track.dart';
import 'package:foundu/features/reports/presentation/report_progress.dart';

void main() {
  test('stage labels describe suggestion and claim progress', () {
    expect(reportProgressLabels, ['Reported', 'Possible Match', 'Claim Submitted', 'Back with Owner']);
  });

  testWidgets('every dot sits over its own label, the last one at the end of the bar', (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(body: SizedBox(width: 360, child: ReportStageTrack(stage: 3))),
    ));

    final dots = tester
        .widgetList<Container>(find.byWidgetPredicate(
            (w) => w is Container && w.decoration is BoxDecoration && (w.decoration as BoxDecoration).shape == BoxShape.circle))
        .toList();
    expect(dots, hasLength(4));

    for (final label in reportProgressLabels) {
      final labelCentre = tester.getCenter(find.text(label)).dx;
      final dotCentres = find
          .byWidgetPredicate((w) =>
              w is Container && w.decoration is BoxDecoration && (w.decoration as BoxDecoration).shape == BoxShape.circle)
          .evaluate()
          .map((e) => tester.getCenter(find.byWidget(e.widget)).dx);
      expect(dotCentres.any((x) => (x - labelCentre).abs() < 1), isTrue, reason: '$label has no dot above it');
    }

    // The final dot is in the last quarter, not at 75%.
    final returned = tester.getCenter(find.text('Back with Owner')).dx;
    expect(returned, greaterThan(360 * 0.75));
  });
}
