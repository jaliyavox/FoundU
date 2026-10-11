import 'dart:async';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/core/api/api_client.dart';
import 'package:foundu/core/api/auth_interceptor.dart';
import 'package:foundu/core/auth/auth_controller.dart';
import 'package:foundu/core/auth/auth_session.dart';
import 'package:foundu/core/auth/token_storage.dart';
import 'package:foundu/core/router/app_router.dart';
import 'package:foundu/features/auth/data/auth_repository.dart';
import 'package:foundu/features/feed/data/feed_models.dart';
import 'package:foundu/features/feed/data/feed_repository.dart';
import 'package:foundu/features/feed/presentation/found_feed_controller.dart';
import 'package:foundu/features/feed/presentation/found_post_card.dart';
import 'package:foundu/features/feed/presentation/found_post_sheet.dart';
import 'package:foundu/features/notifications/data/push_notification_manager.dart';

import 'auth_repository_test.dart' show MemoryTokenStorage, CallbackAdapter, jsonResponse, authResponseJson;

void main() {
  test('an in-flight refresh cannot restore tokens after logout', () async {
    final storage = MemoryTokenStorage(const AuthTokens(accessToken: 'B', refreshToken: 'refresh-B'));
    final adapter = _DeferredAdapter();
    final repository = AuthRepository(authDio: Dio()..httpClientAdapter = adapter,
      authenticatedDio: Dio(), tokenStorage: storage);
    addTearDown(repository.dispose);
    final refreshing = repository.refreshSession();
    await adapter.started.future;
    await repository.clearSession();
    adapter.response.complete(jsonResponse(200, authResponseJson()));
    expect(await refreshing, isFalse);
    expect(await storage.readAccessToken(), isNull);
    expect(await storage.readRefreshToken(), isNull);
  });

  test('an authenticated request from a previous account is discarded instead of retried as the new account', () async {
    var version = 0;
    final storage = MemoryTokenStorage(const AuthTokens(accessToken: 'B', refreshToken: 'refresh-B'));
    final adapter = _DeferredAdapter();
    final dio = Dio()..httpClientAdapter = adapter;
    var refreshed = false;
    dio.interceptors.add(AuthInterceptor(retryDio: Dio(), tokenStorage: storage,
      refreshSession: () async { refreshed = true; return true; },
      onSessionInvalidated: () => fail('An old request must not invalidate the new account'),
      sessionVersion: () => version));
    final request = dio.get<dynamic>('/api/found-posts/feed');
    final rejected = expectLater(request, throwsA(isA<DioException>()));
    await adapter.started.future;
    version++;
    await storage.save(const AuthTokens(accessToken: 'A', refreshToken: 'refresh-A'));
    adapter.response.complete(jsonResponse(200, {'handInCode': 'private-to-B'}));
    await rejected;
    expect(refreshed, isFalse);
    expect(await storage.readAccessToken(), 'A');
  });
  testWidgets('finder logs out and owner logs in on the same installation with fresh capabilities', (tester) async {
    final storage = MemoryTokenStorage();
    final authDio = Dio()..httpClientAdapter = CallbackAdapter((request) {
      if (request.path == '/api/auth/logout') return jsonResponse(200, {});
      final id = request.data['email'] == 'b@test' ? 'B' : 'A';
      final json = authResponseJson();
      json['accessToken'] = id;
      (json['user'] as Map<String, dynamic>)['id'] = id;
      return jsonResponse(200, json);
    });
    final api = Dio()..httpClientAdapter = CallbackAdapter((request) {
      if (request.path.endsWith('/messages')) return jsonResponse(200, []);
      if (request.path == '/api/lost-reports/my-reports') {
        return jsonResponse(200, {
          'items': [], 'page': 1, 'totalCount': 0, 'hasNextPage': false,
        });
      }
      final mine = request.headers['Authorization'] == 'Bearer B';
      return jsonResponse(200, {
        'items': [{
          'id': 'backpack', 'postedByName': 'Student B', 'isMine': mine,
          'categoryName': 'Bags', 'itemTypeName': 'Backpack', 'foundLocationName': 'Library',
          'description': 'Blue backpack', 'primaryColor': 'Blue', 'foundAt': '2026-10-01T00:00:00Z',
          'status': 'Posted', 'handInCode': mine ? '123456' : null, 'createdAt': '2026-10-01T00:00:00Z',
        }], 'page': 1, 'totalCount': 1, 'hasNextPage': false,
      });
    });
    final repository = AuthRepository(authDio: authDio, authenticatedDio: api, tokenStorage: storage);
    api.interceptors.add(AuthInterceptor(retryDio: Dio(), tokenStorage: storage,
      refreshSession: repository.refreshSession, onSessionInvalidated: repository.notifySessionInvalidated,
      sessionVersion: () => repository.sessionVersion));
    final container = ProviderContainer(overrides: [
      authRepositoryProvider.overrideWithValue(repository),
      pushNotificationManagerProvider.overrideWithValue(_NoPush()),
    ]);
    addTearDown(repository.dispose);
    await tester.runAsync(() => container.read(authControllerProvider.future));
    await tester.runAsync(() => container.read(authControllerProvider.notifier).login(email: 'b@test', password: 'password'));
    final finderRouter = container.read(appRouterProvider);
    finderRouter.go('/claims/private-to-B');
    await tester.pumpWidget(UncontrolledProviderScope(container: container,
      child: MaterialApp(home: Scaffold(body: Consumer(builder: (context, ref, _) {
        final items = ref.watch(foundFeedControllerProvider).items;
        if (items.isEmpty) return const Text('Loading');
        final post = items.single;
        return SingleChildScrollView(child: FoundPostCard(post: post,
          onOpen: () => showFoundPostDetail(context, post)));
      })))));
    await tester.pumpAndSettle();
    expect(find.text('Your post'), findsOneWidget);
    expect(find.text('Found by you'), findsOneWidget);
    await tester.tap(find.text('Backpack'));
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(find.text('123 456'), 200, scrollable: find.byType(Scrollable).last);
    expect(find.text('123 456'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('Take this post down'), 100, scrollable: find.byType(Scrollable).last);
    expect(find.text('Take this post down'), findsOneWidget);
    Navigator.of(tester.element(find.text('Take this post down'))).pop();
    await tester.pumpAndSettle();
    await tester.runAsync(() => container.read(authControllerProvider.notifier).logout());
    expect(await storage.readAccessToken(), isNull);
    expect(await storage.readRefreshToken(), isNull);
    await tester.runAsync(() => container.read(authControllerProvider.notifier).login(email: 'a@test', password: 'password'));
    final ownerRouter = container.read(appRouterProvider);
    expect(identical(finderRouter, ownerRouter), isFalse);
    expect(ownerRouter.routeInformationProvider.value.uri.path, isNot('/claims/private-to-B'));
    await tester.pumpAndSettle();
    expect(find.text('Your post'), findsNothing);
    expect(find.text('Found by you'), findsNothing);
    expect(container.read(foundFeedControllerProvider).items.single.handInCode, isNull);
    await tester.tap(find.text('Backpack'));
    await tester.pumpAndSettle();
    expect(find.text('123 456'), findsNothing);
    expect(find.text('Take this post down'), findsNothing);
    await tester.pumpWidget(const SizedBox());
    container.dispose();
  });

  test('an old feed response cannot restore finder data after an identity boundary', () async {
    final repository = _DeferredFeed();
    final container = ProviderContainer(overrides: [feedRepositoryProvider.overrideWithValue(repository)]);
    addTearDown(container.dispose);
    container.read(foundFeedControllerProvider);
    await Future<void>.delayed(Duration.zero);
    container.read(authSessionEpochProvider.notifier).advance();
    expect(container.read(foundFeedControllerProvider).items, isEmpty);
    await Future<void>.delayed(Duration.zero);
    repository.requests[1].complete(_page(false));
    await Future<void>.delayed(Duration.zero);
    repository.requests[0].complete(_page(true));
    await Future<void>.delayed(Duration.zero);
    final post = container.read(foundFeedControllerProvider).items.single;
    expect(post.isMine, isFalse);
    expect(post.handInCode, isNull);
  });
}

FoundPostPage _page(bool mine) => FoundPostPage(items: [FoundPost(
  id: 'item', postedByName: 'Student B', isMine: mine, categoryName: 'Bags',
  itemTypeName: 'Backpack', foundLocationName: 'Library', description: 'Blue backpack',
  primaryColor: 'Blue', foundAt: DateTime.utc(2026), status: 'Posted',
  handInCode: mine ? '123456' : null, createdAt: DateTime.utc(2026),
)], page: 1, totalCount: 1, hasNextPage: false);

class _DeferredFeed extends FeedRepository {
  _DeferredFeed() : super(Dio());
  final requests = <Completer<FoundPostPage>>[];
  @override
  Future<FoundPostPage> getFoundFeed({int page = 1, int pageSize = 20, String? search, String? categoryId}) {
    final response = Completer<FoundPostPage>();
    requests.add(response);
    return response.future;
  }
}

class _NoPush extends PushNotificationManager {
  _NoPush() : super(Dio());
  @override
  Future<void> start() async {}
  @override
  Future<void> unregister() async {}
}

class _DeferredAdapter implements HttpClientAdapter {
  final started = Completer<void>();
  final response = Completer<ResponseBody>();
  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) {
    started.complete();
    return response.future;
  }
  @override
  void close({bool force = false}) {}
}
