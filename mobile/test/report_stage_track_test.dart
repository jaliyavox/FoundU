import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/reports/presentation/report_stage_track.dart';

void main() {
  test('stages follow the same data as the web', () {
    expect(reportStageOf('Active'), 0);
    expect(reportStageOf('Active', foundClaimCount: 1), 1);
    expect(reportStageOf('Active', messageCount: 2), 1);
    expect(reportStageOf('Matched'), 2);
    expect(reportStageOf('Resolved'), 3);
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

    for (final label in reportStages) {
      final labelCentre = tester.getCenter(find.text(label)).dx;
      final dotCentres = find
          .byWidgetPredicate((w) =>
              w is Container && w.decoration is BoxDecoration && (w.decoration as BoxDecoration).shape == BoxShape.circle)
          .evaluate()
          .map((e) => tester.getCenter(find.byWidget(e.widget)).dx);
      expect(dotCentres.any((x) => (x - labelCentre).abs() < 1), isTrue, reason: '$label has no dot above it');
    }

    // The Returned dot is in the last quarter, not at 75%.
    final returned = tester.getCenter(find.text('Returned')).dx;
    expect(returned, greaterThan(360 * 0.75));
  });
}
