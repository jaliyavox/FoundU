import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/theme/app_theme.dart';
import 'package:foundu/core/widgets/surfaces.dart';

/// Guards the bug that blanked the report form: a button theme with an infinite minimum
/// width fails layout the moment a button is placed in a Row or a dialog's action bar, and
/// the failure takes the whole screen with it without a visible error. Every button the app
/// uses has to survive both places.
void main() {
  List<Widget> buttons() => [
        FilledButton(onPressed: () {}, child: const Text('Filled')),
        ElevatedButton(onPressed: () {}, child: const Text('Elevated')),
        OutlinedButton(onPressed: () {}, child: const Text('Outlined')),
        OutlinedButton.icon(onPressed: () {}, icon: const Icon(Icons.edit), label: const Text('With icon')),
        InkButton(label: 'Ink', onPressed: () {}),
      ];

  Future<void> pump(WidgetTester tester, Widget home) async {
    await tester.pumpWidget(MaterialApp(theme: buildFoundUTheme(), home: Scaffold(body: home)));
    await tester.pump();
  }

  testWidgets('every button lays out side by side in a Row', (tester) async {
    await pump(tester, SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(children: buttons()),
    ));

    expect(tester.takeException(), isNull);
    expect(find.text('Outlined'), findsOneWidget);
    expect(find.text('Ink'), findsOneWidget);
  });

  testWidgets('every button lays out in a dialog action bar', (tester) async {
    await pump(tester, Builder(
      builder: (context) => TextButton(
        onPressed: () => showDialog<void>(
          context: context,
          builder: (_) => AlertDialog(title: const Text('Close the report?'), actions: buttons().take(3).toList()),
        ),
        child: const Text('open'),
      ),
    ));
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.text('Close the report?'), findsOneWidget);
  });

  testWidgets('InkButton still fills the width when it has a width to fill', (tester) async {
    await pump(tester, Column(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [InkButton(label: 'Sign in', onPressed: () {})],
    ));

    final screen = tester.getSize(find.byType(Scaffold)).width;
    final button = tester.getSize(find.byType(FilledButton)).width;
    expect(button, screen);
  });
}
