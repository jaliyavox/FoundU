import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:foundu/features/feed/presentation/found_board_page.dart';
import 'package:foundu/features/feed/presentation/found_feed_controller.dart';

/// No network: the board shows an empty page and never loads.
class _EmptyFoundFeed extends FoundFeedController {
  @override
  FoundFeedState build() => const FoundFeedState(hasNextPage: false);

  @override
  Future<void> refresh() async {}
}

void main() {
  testWidgets('the found board offers "Post a found item" and it opens the post form', (tester) async {
    tester.view.physicalSize = const Size(360, 740);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/home/found',
      routes: [
        GoRoute(path: '/home/found', builder: (_, __) => const FoundBoardPage()),
        GoRoute(path: '/home/found/new', builder: (_, __) => const Scaffold(body: Text('post form'))),
      ],
    );

    await tester.pumpWidget(ProviderScope(
      overrides: [foundFeedControllerProvider.overrideWith(_EmptyFoundFeed.new)],
      child: MaterialApp.router(routerConfig: router),
    ));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Post a found item'));
    await tester.pumpAndSettle();

    expect(find.text('post form'), findsOneWidget);
  });
}
