import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/api/api_exception.dart';
import 'package:foundu/core/theme/app_theme.dart';
import 'package:foundu/features/intake/data/intake_repository.dart';
import 'package:foundu/features/intake/presentation/ask_foundu_page.dart';
import 'package:foundu/features/reports/data/report_models.dart';
import 'package:foundu/features/reports/data/report_repository.dart';

IntakeResponse reply(String phase, String text, {IntakeMatch? match}) => IntakeResponse(
      phase: phase,
      reply: text,
      slots: const IntakeSlots(itemType: 'Backpack', colour: 'black'),
      draft: const IntakeDraft(description: 'I lost my black backpack.'),
      match: match,
    );

class FakeIntake extends IntakeRepository {
  FakeIntake(this.answer) : super(Dio());
  final IntakeResponse Function(String message) answer;
  final asked = <String>[];
  final sentSlots = <IntakeSlots?>[];
  bool fail = false;

  @override
  Future<IntakeResponse> ask(String message, IntakeSlots? slots) async {
    asked.add(message);
    sentSlots.add(slots);
    if (fail) throw const ApiException('The assistant is unavailable right now.');
    return answer(message);
  }
}

class NoReports extends LostReportRepository {
  NoReports() : super(dio: Dio());

  @override
  Future<PagedResult<LostReportListItemModel>> getMyReports({
    String? status,
    int page = 1,
    int pageSize = 20,
  }) async =>
      const PagedResult(
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 50,
        totalPages: 0,
        hasPreviousPage: false,
        hasNextPage: false,
      );
}

Future<void> mount(WidgetTester tester, FakeIntake intake, {String? first}) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [
      intakeRepositoryProvider.overrideWithValue(intake),
      reportRepositoryProvider.overrideWithValue(NoReports()),
    ],
    child: MaterialApp(
      theme: buildFoundUTheme(),
      // The flame beside each reply animates for ever, so nothing would ever settle. Asking
      // for reduced motion stops it - and checks that it does.
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(context).copyWith(disableAnimations: true),
        child: child!,
      ),
      home: AskFoundUPage(initialQuestion: first),
    ),
  ));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('a question carried in from elsewhere is sent on arrival, not parked in the box', (tester) async {
    final intake = FakeIntake((_) => reply('collecting', 'What colour is it?'));
    await mount(tester, intake, first: 'I lost my backpack');

    expect(intake.asked, ['I lost my backpack']);
    expect(find.text('I lost my backpack'), findsOneWidget);
    expect(find.text('What colour is it?'), findsOneWidget);
  });

  testWidgets('a match shows what it is and says plainly it is not proof', (tester) async {
    final intake = FakeIntake((_) => reply(
          'matched',
          'This might be yours. It is at a desk.',
          match: const IntakeMatch(
            id: 'item-1',
            kind: 'desk',
            itemType: 'Backpack',
            colour: 'Black',
            location: 'Cafeteria',
            description: 'Black backpack, two zips.',
          ),
        ));
    await mount(tester, intake, first: 'black backpack at the cafeteria');

    expect(find.text('Possible match · at a desk'), findsOneWidget);
    expect(find.text('Black Backpack'), findsOneWidget);
    expect(find.textContaining('not proof of ownership'), findsOneWidget);
    // No open report to link it to, so the way forward is the draft.
    expect(find.textContaining('no open report'), findsOneWidget);
    expect(find.text('Review report draft'), findsOneWidget);
  });

  testWidgets('an unreachable agent is said out loud, and the words stay in the transcript', (tester) async {
    final intake = FakeIntake((_) => reply('collecting', 'unused'))..fail = true;
    await mount(tester, intake, first: 'my phone');

    expect(find.text('my phone'), findsOneWidget);
    expect(find.textContaining('The assistant is unavailable right now.'), findsOneWidget);
  });

  testWidgets('"I found something" tells the agent which side the student is on', (tester) async {
    final intake = FakeIntake((_) => reply('collecting', 'What colour is it?'));
    await mount(tester, intake);

    await tester.tap(find.text('I found something'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Thanks for picking it up'), findsOneWidget);
    // The choice is made once; the buttons go.
    expect(find.text('I lost something'), findsNothing);

    await tester.enterText(find.byType(TextField), 'a blue water bottle');
    await tester.tap(find.byTooltip('Send'));
    await tester.pumpAndSettle();

    expect(intake.sentSlots.single?.intent, 'found');
  });

  testWidgets('a finder is pointed at the owner, and offered a found post if it is not theirs', (tester) async {
    final intake = FakeIntake((_) => const IntakeResponse(
          phase: 'matched',
          reply: 'Someone is looking for this.',
          slots: IntakeSlots(itemType: 'Water Bottle', colour: 'blue', intent: 'found'),
          draft: IntakeDraft(description: 'Blue water bottle, found at Library.'),
          match: IntakeMatch(
            id: 'report-1',
            kind: 'lost',
            itemType: 'Water Bottle',
            colour: 'Blue',
            location: 'Library',
            description: 'Dented lid, university sticker.',
          ),
        ));
    await mount(tester, intake, first: 'I found a blue water bottle in the library');

    expect(find.text('Someone is looking for this · reported lost'), findsOneWidget);
    expect(find.text('Last seen near Library'), findsOneWidget);
    expect(find.text('Open their report'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('Review found post'), 200, scrollable: find.byType(Scrollable).first);
    expect(find.text('Not the same one? Post it anyway'), findsOneWidget);
    // Not the owner's flow: nothing here claims the item or drafts a lost report.
    expect(find.textContaining('open a claim'), findsNothing);
    expect(find.text('Review report draft'), findsNothing);
  });
}
