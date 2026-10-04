import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/auth/auth_controller.dart';
import 'package:foundu/features/auth/data/auth_models.dart';
import 'package:foundu/features/feed/data/feed_models.dart';
import 'package:foundu/features/feed/data/feed_repository.dart';
import 'package:foundu/features/feed/presentation/found_feed_controller.dart';
import 'package:foundu/features/feed/presentation/found_post_sheet.dart';
import 'package:foundu/features/feed/presentation/message_thread.dart';
import 'package:foundu/features/reports/presentation/providers/report_providers.dart';

void main() {
  for (final custody in [false, true]) {
    testWidgets('finder messaging reflects custody $custody', (tester) async {
      final post = FoundPost(id: 'bottle', postedByName: 'Student B', isMine: false,
        categoryName: 'Other', itemTypeName: 'Water Bottle', foundLocationName: 'Library',
        description: 'Red water bottle', primaryColor: 'Red', foundAt: DateTime.utc(2026),
        status: custody ? 'Unclaimed' : 'Posted', handInCode: null, createdAt: DateTime.utc(2026),
        storageLocationName: custody ? 'Security Desk – Building A' : null);
      await tester.pumpWidget(ProviderScope(overrides: [
        authControllerProvider.overrideWith(_Student.new),
        foundFeedControllerProvider.overrideWith(_EmptyFeed.new),
        feedRepositoryProvider.overrideWithValue(_Messages()),
        foundItemMatchesProvider('bottle').overrideWith((ref) async => []),
      ], child: MaterialApp(home: Scaffold(body: Builder(builder: (context) => TextButton(
        onPressed: () => showFoundPostDetail(context, post), child: const Text('Open')))))));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Open'));
      await tester.pumpAndSettle();
      final target = custody
        ? find.textContaining('This item is now held at Security Desk')
        : find.textContaining('Nothing is claimed by asking.');
      await tester.scrollUntilVisible(target, 250, scrollable: find.byType(Scrollable).last);
      expect(target, findsOneWidget);
      expect(find.byType(MessageThread), custody ? findsNothing : findsOneWidget);
      expect(find.byType(DropdownButtonFormField<String>), findsNothing);
      if (custody) expect(find.textContaining('Ask Student'), findsNothing);
      await tester.pumpWidget(const SizedBox());
    });
  }
}

class _Student extends AuthController {
  @override
  Future<AuthUser?> build() async => const AuthUser(id: 'A', name: 'Student A', email: 'a@test', role: 'Student');
}
class _EmptyFeed extends FoundFeedController {
  @override
  FoundFeedState build() => const FoundFeedState();
}
class _Messages extends FeedRepository {
  _Messages() : super(Dio());
  @override
  Future<List<ReportMessage>> getFoundPostMessages(String postId) async => [];
}
