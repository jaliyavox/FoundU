import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/main.dart';

void main() {
  testWidgets('App renders skeleton home', (WidgetTester tester) async {
    await tester.pumpWidget(const FoundUApp());
    expect(find.text('FoundU mobile — skeleton'), findsOneWidget);
  });
}
