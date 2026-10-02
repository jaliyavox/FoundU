import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:foundu/core/theme/app_theme.dart';
import 'package:foundu/features/support/data/support_repository.dart';
import 'package:foundu/features/support/presentation/support_assistant_page.dart';

class FakeAssistant extends SupportRepository {
  FakeAssistant(this.answers) : super(Dio());
  final List<AssistantResponse> answers;
  final asked = <({String message, List<AssistantTurn> history, String? lastTopic})>[];
  final opened = <({String subject, String category, String body, bool viaAssistant})>[];

  @override
  Future<AssistantResponse> ask(String message, List<AssistantTurn> history, String? lastTopic) async {
    asked.add((message: message, history: history, lastTopic: lastTopic));
    return answers[asked.length - 1];
  }

  @override
  Future<TicketDetail> open({
    required String subject,
    required String category,
    required String body,
    bool viaAssistant = false,
  }) async {
    opened.add((subject: subject, category: category, body: body, viaAssistant: viaAssistant));
    return TicketDetail(id: 'ticket-1', subject: subject, category: category, status: 'Open', createdAt: DateTime.now(), messages: const []);
  }

  @override
  Future<List<TicketSummary>> mine() async => const [];
}

const answered = AssistantResponse(
  phase: 'answered',
  reply: 'Your collection code is on the claim page. Did that solve it?',
  topic: 'collection_code',
);

const escalated = AssistantResponse(
  phase: 'escalate',
  reply: 'Of course - this needs a person.',
  topic: 'collection_code',
  ticket: TicketDraft(subject: 'where is my code', category: 'Collection', body: 'Raised through the FoundU assistant.'),
);

Future<void> mount(WidgetTester tester, FakeAssistant repository) async {
  tester.view.physicalSize = const Size(412, 915);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);

  final router = GoRouter(
    initialLocation: '/profile/support/assistant',
    routes: [
      GoRoute(path: '/profile/support/assistant', builder: (_, __) => const SupportAssistantPage()),
      GoRoute(path: '/profile/support/:id', builder: (_, state) => Scaffold(body: Text('ticket ${state.pathParameters['id']}'))),
    ],
  );
  await tester.pumpWidget(ProviderScope(
    overrides: [supportRepositoryProvider.overrideWithValue(repository)],
    child: MaterialApp.router(theme: buildFoundUTheme(), routerConfig: router),
  ));
  await tester.pumpAndSettle();
}

Future<void> say(WidgetTester tester, String text) async {
  await tester.enterText(find.byType(TextField), text);
  await tester.tap(find.byTooltip('Send'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('an answer asks whether it helped, and "no" goes back with its topic', (tester) async {
    final repository = FakeAssistant([answered, escalated]);
    await mount(tester, repository);

    await say(tester, 'where is my code');
    expect(find.textContaining('on the claim page'), findsOneWidget);

    await tester.tap(find.text('No, I still need help'));
    await tester.pumpAndSettle();

    expect(repository.asked, hasLength(2));
    expect(repository.asked[1].lastTopic, 'collection_code');
    // The greeting is the page's own - it is not sent as if the agent had said it.
    expect(repository.asked[1].history.map((t) => t.text), [
      'where is my code',
      'Your collection code is on the claim page. Did that solve it?',
    ]);
    expect(find.text('A ticket for the support team'), findsOneWidget);
  });

  testWidgets('the drafted ticket is only sent when the person presses send, marked as via the assistant',
      (tester) async {
    final repository = FakeAssistant([escalated]);
    await mount(tester, repository);

    await say(tester, 'where is my code');
    expect(repository.opened, isEmpty);

    await tester.scrollUntilVisible(find.text('Send to the support team'), 200, scrollable: find.byType(Scrollable).first);
    await tester.tap(find.text('Send to the support team'));
    await tester.pumpAndSettle();

    expect(repository.opened.single.viaAssistant, isTrue);
    expect(repository.opened.single.category, 'Collection');
    expect(find.text('ticket ticket-1'), findsOneWidget);
  });
}
