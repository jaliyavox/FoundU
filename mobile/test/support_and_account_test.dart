import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/theme/app_theme.dart';
import 'package:foundu/features/account/data/account_repository.dart';
import 'package:foundu/features/account/presentation/account_page.dart';
import 'package:foundu/features/support/data/support_repository.dart';
import 'package:foundu/features/support/presentation/support_page.dart';
import 'package:foundu/features/support/presentation/ticket_page.dart';

class FakeSupport extends SupportRepository {
  FakeSupport({this.tickets = const [], this.ticket, this.fail = false}) : super(Dio());
  final List<TicketSummary> tickets;
  final TicketDetail? ticket;
  final bool fail;

  @override
  Future<List<TicketSummary>> mine() async {
    if (fail) throw StateError('private transport details');
    return tickets;
  }

  @override
  Future<TicketDetail> get(String id) async => ticket!;
}

class FakeAccount extends AccountRepository {
  FakeAccount(this.profile) : super(Dio());
  final Profile profile;

  @override
  Future<Profile> getProfile() async => profile;
}

TicketDetail ticket(String status) => TicketDetail(
      id: 't1',
      subject: 'My code will not scan',
      category: 'Collection',
      status: status,
      createdAt: DateTime.now().subtract(const Duration(hours: 2)),
      messages: [
        TicketMessage(
          id: 'm1',
          senderName: 'Dev Fernando',
          isMine: true,
          isStaffReply: false,
          body: 'The desk says my code is not recognised.',
          createdAt: DateTime.now().subtract(const Duration(hours: 2)),
        ),
        TicketMessage(
          id: 'm2',
          senderName: 'Priya Desk',
          isMine: false,
          isStaffReply: true,
          body: 'Come to the library desk and ask for Priya.',
          createdAt: DateTime.now().subtract(const Duration(hours: 1)),
        ),
      ],
    );

Future<void> mount(WidgetTester tester, List<Override> overrides, Widget home) async {
  await tester.pumpWidget(ProviderScope(
    overrides: overrides,
    child: MaterialApp(theme: buildFoundUTheme(), home: home),
  ));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('no tickets yet explains where the conversation will live', (tester) async {
    await mount(tester, [supportRepositoryProvider.overrideWithValue(FakeSupport())], const SupportPage());

    expect(find.textContaining('not asked us anything yet'), findsOneWidget);
    expect(find.text('New ticket'), findsOneWidget);
  });

  testWidgets('a failure offers a retry and keeps transport details off screen', (tester) async {
    await mount(tester, [supportRepositoryProvider.overrideWithValue(FakeSupport(fail: true))], const SupportPage());

    expect(find.text('Could not load your tickets'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(find.textContaining('private transport details'), findsNothing);
  });

  testWidgets('a desk reply is marked as coming from FoundU', (tester) async {
    await mount(
      tester,
      [supportRepositoryProvider.overrideWithValue(FakeSupport(ticket: ticket('Waiting')))],
      const TicketPage(ticketId: 't1'),
    );

    expect(find.text('Come to the library desk and ask for Priya.'), findsOneWidget);
    expect(find.textContaining('Priya · FoundU'), findsOneWidget);
    expect(find.textContaining('Waiting on you'), findsOneWidget);
    expect(find.byType(TextField), findsOneWidget);
  });

  testWidgets('a closed ticket shows its history and no reply box', (tester) async {
    await mount(
      tester,
      [supportRepositoryProvider.overrideWithValue(FakeSupport(ticket: ticket('Closed')))],
      const TicketPage(ticketId: 't1'),
    );

    expect(find.textContaining('This ticket is closed'), findsOneWidget);
    expect(find.byType(TextField), findsNothing);
  });

  testWidgets('changing the email asks for the current password, and only then', (tester) async {
    const profile = Profile(
      id: 'u1',
      fullName: 'Dev Fernando',
      email: 'dev@foundu.test',
      role: 'Student',
      studentNumber: 'IT26001002',
      hasPassword: true,
      isGoogleLinked: false,
    );
    await mount(tester, [accountRepositoryProvider.overrideWithValue(FakeAccount(profile))], const AccountPage());

    // Two "Current password" fields would mean the details form is asking already.
    expect(find.widgetWithText(TextField, 'Current password'), findsOneWidget);

    await tester.enterText(find.widgetWithText(TextField, 'Email'), 'dev.new@foundu.test');
    await tester.pump();

    expect(find.widgetWithText(TextField, 'Current password'), findsNWidgets(2));
    expect(find.text('Needed to move your email address.'), findsOneWidget);
  });

  testWidgets('an account made through Google is offered a first password, not a change', (tester) async {
    const profile = Profile(
      id: 'u2',
      fullName: 'Google Person',
      email: 'g@foundu.test',
      role: 'Student',
      studentNumber: null,
      hasPassword: false,
      isGoogleLinked: true,
    );
    await mount(tester, [accountRepositoryProvider.overrideWithValue(FakeAccount(profile))], const AccountPage());

    expect(find.text('Set a password'), findsWidgets);
    expect(find.text('Google is linked to this account'), findsOneWidget);
    expect(find.widgetWithText(TextField, 'Current password'), findsNothing);
  });
}
