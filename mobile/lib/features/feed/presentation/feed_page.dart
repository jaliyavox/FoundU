import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/foundu_mark.dart';
import '../../../core/widgets/surfaces.dart';
import '../../reference/data/reference_models.dart';
import '../../reference/data/reference_repository.dart';
import 'feed_card.dart';
import 'feed_controller.dart';
import 'feed_detail_sheet.dart';
import 'found_feed_controller.dart';
import 'found_post_card.dart';
import 'found_post_sheet.dart';

final _categoriesProvider = FutureProvider<List<CategoryModel>>(
  (ref) => ref.watch(referenceRepositoryProvider).getCategories(),
);

/// The lost feed: what other students have lost, as a board anyone can read.
///
/// Reported items only - there is no list of what has been handed in, because that is how
/// someone shops for a thing to claim. Pressing "I found this" routes the finder to a desk,
/// never to the poster.
class FeedPage extends ConsumerStatefulWidget {
  const FeedPage({super.key});

  @override
  ConsumerState<FeedPage> createState() => _FeedPageState();
}

enum _Board { lost, found }

class _FeedPageState extends ConsumerState<FeedPage> {
  final _scroll = ScrollController();
  final _search = TextEditingController();
  Timer? _debounce;
  // Two boards, one page: what people are looking for, and what people have found and not
  // yet walked to a desk. Search and category apply to whichever is showing.
  _Board _board = _Board.lost;

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_scroll.position.pixels > _scroll.position.maxScrollExtent - 600) {
        if (_board == _Board.lost) {
          ref.read(feedControllerProvider.notifier).loadMore();
        } else {
          ref.read(foundFeedControllerProvider.notifier).loadMore();
        }
      }
    });
  }

  void _switchBoard(_Board board) {
    if (board == _board) return;
    setState(() => _board = board);
    // Carry the search and category across so switching does not lose what you typed.
    final search = _search.text;
    final category = board == _Board.lost ? ref.read(foundFeedControllerProvider).categoryId : ref.read(feedControllerProvider).categoryId;
    if (board == _Board.lost) {
      ref.read(feedControllerProvider.notifier)
        ..setSearch(search)
        ..setCategory(category);
    } else {
      ref.read(foundFeedControllerProvider.notifier)
        ..setSearch(search)
        ..setCategory(category);
    }
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _scroll.dispose();
    _search.dispose();
    super.dispose();
  }

  void _onSearchChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), () {
      if (_board == _Board.lost) {
        ref.read(feedControllerProvider.notifier).setSearch(value);
      } else {
        ref.read(foundFeedControllerProvider.notifier).setSearch(value);
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final feed = ref.watch(feedControllerProvider);
    final found = ref.watch(foundFeedControllerProvider);
    final showingFound = _board == _Board.found;
    final categories = ref.watch(_categoriesProvider).value ?? const <CategoryModel>[];
    final user = ref.watch(authControllerProvider).value;
    final text = Theme.of(context).textTheme;
    final firstName = user?.name.split(' ').first;

    return Scaffold(
      body: RefreshIndicator(
        color: Brand.forest,
        onRefresh: () => showingFound
            ? ref.read(foundFeedControllerProvider.notifier).refresh()
            : ref.read(feedControllerProvider.notifier).refresh(),
        child: CustomScrollView(
          controller: _scroll,
          physics: const AlwaysScrollableScrollPhysics(),
          slivers: [
            SliverSafeArea(
              bottom: false,
              sliver: SliverPadding(
                padding: const EdgeInsets.fromLTRB(20, 12, 20, 0),
                sliver: SliverList.list(children: [
                  Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(firstName == null ? 'The board' : 'Hello, $firstName', style: text.headlineSmall),
                            const SizedBox(height: 2),
                            Text(
                              showingFound ? 'What people have found, not yet at a desk' : 'What people have lost around campus',
                              style: text.bodyMedium?.copyWith(color: Brand.muted),
                            ),
                          ],
                        ),
                      ),
                      const FoundUMark(size: 44),
                    ],
                  ),
                  const SizedBox(height: 16),
                  _BoardToggle(board: _board, onChanged: _switchBoard),
                  const SizedBox(height: 14),
                  TextField(
                    controller: _search,
                    onChanged: _onSearchChanged,
                    textInputAction: TextInputAction.search,
                    decoration: InputDecoration(
                      hintText: 'Search the feed',
                      prefixIcon: const Icon(Icons.search_rounded, color: Brand.muted),
                      suffixIcon: _search.text.isEmpty
                          ? null
                          : IconButton(
                              icon: const Icon(Icons.close_rounded, size: 18),
                              onPressed: () {
                                _search.clear();
                                _onSearchChanged('');
                                setState(() {});
                              },
                            ),
                    ),
                  ),
                  const SizedBox(height: 18),
                  Text('Browse by category', style: text.titleMedium),
                  const SizedBox(height: 10),
                ]),
              ),
            ),
            SliverToBoxAdapter(
              child: ChipRow<String?>(
                options: [null, ...categories.map((c) => c.id)],
                selected: showingFound ? found.categoryId : feed.categoryId,
                onSelect: (id) => showingFound
                    ? ref.read(foundFeedControllerProvider.notifier).setCategory(id)
                    : ref.read(feedControllerProvider.notifier).setCategory(id),
                labelOf: (id) => id == null ? 'All' : categories.firstWhere((c) => c.id == id).name,
              ),
            ),
            const SliverToBoxAdapter(child: SizedBox(height: 16)),
            if (showingFound)
              ..._foundSlivers(found, text)
            else if (feed.error != null)
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  child: Panel(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Could not load the feed', style: text.titleMedium),
                        const SizedBox(height: 4),
                        Text('Check the API is running, then pull to refresh.', style: text.bodyMedium?.copyWith(color: Brand.muted)),
                      ],
                    ),
                  ),
                ),
              )
            else if (feed.isEmpty)
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(20, 24, 20, 0),
                  child: Column(
                    children: [
                      const Icon(Icons.inbox_outlined, size: 36, color: Brand.faint),
                      const SizedBox(height: 10),
                      Text('Nothing here yet', style: text.titleMedium),
                      const SizedBox(height: 4),
                      Text(
                        feed.search.isNotEmpty || feed.categoryId != null
                            ? 'Nothing matches that. Try another category.'
                            : 'Nobody has reported anything lost. Good news, for now.',
                        textAlign: TextAlign.center,
                        style: text.bodyMedium?.copyWith(color: Brand.muted),
                      ),
                    ],
                  ),
                ),
              )
            else
              SliverPadding(
                padding: const EdgeInsets.symmetric(horizontal: 20),
                sliver: SliverList.separated(
                  itemCount: feed.items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 14),
                  itemBuilder: (context, index) {
                    final item = feed.items[index];
                    return FeedCard(item: item, onOpen: () => showFeedDetail(context, item));
                  },
                ),
              ),
            SliverToBoxAdapter(
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 20),
                child: (showingFound ? found.isLoading : feed.isLoading)
                    ? const Center(child: SizedBox.square(dimension: 22, child: CircularProgressIndicator(strokeWidth: 2)))
                    : (showingFound ? found.items.isNotEmpty && !found.hasNextPage : feed.items.isNotEmpty && !feed.hasNextPage)
                        ? Center(child: Text('That is everything.', style: text.bodySmall?.copyWith(color: Brand.faint)))
                        : const SizedBox.shrink(),
              ),
            ),
            // Room for the floating nav.
            const SliverToBoxAdapter(child: SizedBox(height: 96)),
          ],
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        heroTag: 'post',
        backgroundColor: Brand.forest,
        foregroundColor: Colors.white,
        elevation: 0,
        shape: const StadiumBorder(),
        onPressed: () => _showPostChooser(context),
        icon: const Icon(Icons.add_rounded),
        label: const Text('Post'),
      ),
      floatingActionButtonLocation: FloatingActionButtonLocation.endFloat,
    );
  }
}


extension on _FeedPageState {
  List<Widget> _foundSlivers(FoundFeedState found, TextTheme text) {
    if (found.error != null) {
      return [
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 20),
            child: Panel(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Could not load the board', style: text.titleMedium),
                  const SizedBox(height: 4),
                  Text('Check the API is running, then pull to refresh.', style: text.bodyMedium?.copyWith(color: Brand.muted)),
                ],
              ),
            ),
          ),
        ),
      ];
    }
    if (found.isEmpty) {
      return [
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(20, 24, 20, 0),
            child: Column(
              children: [
                const Icon(Icons.front_hand_outlined, size: 36, color: Brand.faint),
                const SizedBox(height: 10),
                Text('Nothing posted yet', style: text.titleMedium),
                const SizedBox(height: 4),
                Text('When someone finds something and posts it, it shows here until they hand it in.',
                    textAlign: TextAlign.center, style: text.bodyMedium?.copyWith(color: Brand.muted)),
              ],
            ),
          ),
        ),
      ];
    }
    return [
      SliverPadding(
        padding: const EdgeInsets.symmetric(horizontal: 20),
        sliver: SliverList.separated(
          itemCount: found.items.length,
          separatorBuilder: (_, __) => const SizedBox(height: 14),
          itemBuilder: (context, index) {
            final post = found.items[index];
            return FoundPostCard(post: post, onOpen: () => showFoundPostDetail(context, post));
          },
        ),
      ),
    ];
  }

  void _showPostChooser(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      backgroundColor: Brand.paper,
      builder: (sheet) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 4, 20, 20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text('What happened?', style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 14),
              _ChooserTile(
                icon: Icons.search_rounded,
                title: 'I lost something',
                body: 'Post it so finders and the desk know what to look out for.',
                onTap: () { Navigator.of(sheet).pop(); context.push('/reports/new'); },
              ),
              const SizedBox(height: 10),
              _ChooserTile(
                icon: Icons.front_hand_outlined,
                title: 'I found something',
                body: 'Post it so the owner can spot it, then hand it in at a desk.',
                onTap: () { Navigator.of(sheet).pop(); context.push('/home/found/new'); },
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ChooserTile extends StatelessWidget {
  const _ChooserTile({required this.icon, required this.title, required this.body, required this.onTap});
  final IconData icon;
  final String title;
  final String body;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Panel(
      padding: const EdgeInsets.all(16),
      onTap: onTap,
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: const BoxDecoration(color: Brand.mist, shape: BoxShape.circle),
            child: Icon(icon, color: Brand.forest),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: text.titleMedium),
                Text(body, style: text.bodySmall?.copyWith(color: Brand.muted)),
              ],
            ),
          ),
          const Icon(Icons.chevron_right_rounded, color: Brand.faint),
        ],
      ),
    );
  }
}

/// Lost · Found. A pill pair rather than tabs, to match the category chips beneath it.
class _BoardToggle extends StatelessWidget {
  const _BoardToggle({required this.board, required this.onChanged});
  final _Board board;
  final ValueChanged<_Board> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(color: Brand.surfaceTint, borderRadius: BorderRadius.circular(999)),
      child: Row(
        children: [
          for (final option in _Board.values)
            Expanded(
              child: GestureDetector(
                onTap: () => onChanged(option),
                child: AnimatedContainer(
                  duration: const Duration(milliseconds: 220),
                  curve: Curves.easeOutCubic,
                  padding: const EdgeInsets.symmetric(vertical: 10),
                  decoration: BoxDecoration(
                    color: board == option ? Brand.ink : Colors.transparent,
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    option == _Board.lost ? 'Lost' : 'Found',
                    textAlign: TextAlign.center,
                    style: TextStyle(fontWeight: FontWeight.w600, color: board == option ? Colors.white : Brand.text),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}
