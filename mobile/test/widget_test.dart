import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/app/app.dart';

void main() {
  testWidgets('App smoke test renders FoundUApp', (WidgetTester tester) async {
    await tester.pumpWidget(
      const ProviderScope(
        child: FoundUApp(),
      ),
    );

    expect(find.byType(FoundUApp), findsOneWidget);
  });
}
