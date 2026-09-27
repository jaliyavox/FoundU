import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/flame_mark.dart';
import '../../../core/widgets/foundu_mark.dart';
import '../../../core/widgets/pill_nav.dart';
import '../../../core/widgets/surfaces.dart';
import '../../reference/data/reference_models.dart';
import '../../reference/data/reference_repository.dart';
import '../../notifications/presentation/notification_button.dart';
import 'feed_card.dart';
import 'feed_controller.dart';
import 'feed_detail_sheet.dart';
import 'found_feed_controller.dart';
import 'fresh_finds_strip.dart';

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

class _FeedPageState extends ConsumerState<FeedPage> {
  final _scroll = ScrollController();
  final _search = TextEditingController();
  Timer? _debounce;

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_scroll.position.pixels > _scroll.position.maxScrollExtent - 600) {
        ref.read(feedControllerProvider.notifier).loadMore();
      }
    });
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
      ref.read(feedControllerProvider.notifier).setSearch(value);
    });
  }

  @override
  Widget build(BuildContext context) {
    final feed = ref.watch(feedControllerProvider);
    final categories = ref.watch(_categoriesProvider).value ?? const <CategoryModel>[];
    final user = ref.watch(authControllerProvider).value;
    final text = Theme.of(context).textTheme;
    final firstName = user?.name.split(' ').first;

    return Scaffold(
      body: RefreshIndicator(
        color: Brand.forest,
        onRefresh: () async {
          // The strip rides along: both boards are on this screen now.
          await Future.wait([
            ref.read(feedControllerProvider.notifier).refresh(),
            ref.read(foundFeedControllerProvider.notifier).refresh(),
          ]);
        },
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
                              'What people have lost around campus',
                              style: text.bodyMedium?.copyWith(color: Brand.muted),
                            ),
                          ],
                        ),
                      ),
                      const NotificationButton(),
                      const FoundUMark(size: 44),
                    ],
                  ),
                  const SizedBox(height: 16),
                  const _AskBanner(),
                  const SizedBox(height: 18),
                ]),
              ),
            ),
            // The newest finds sit above the reports, edge to edge so the row can scroll out
            // past the page padding.
            const SliverToBoxAdapter(
              child: Padding(
                padding: EdgeInsets.fromLTRB(20, 0, 0, 18),
                child: FreshFindsStrip(),
              ),
            ),
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 0),
              sliver: SliverList.list(children: [
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
            SliverToBoxAdapter(
              child: ChipRow<String?>(
                options: [null, ...categories.map((c) => c.id)],
                selected: feed.categoryId,
                onSelect: (id) => ref.read(feedControllerProvider.notifier).setCategory(id),
                labelOf: (id) => id == null ? 'All' : categories.firstWhere((c) => c.id == id).name,
              ),
            ),
            const SliverToBoxAdapter(child: SizedBox(height: 16)),
            if (feed.error != null)
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
                child: feed.isLoading
                    ? const Center(child: SizedBox.square(dimension: 22, child: CircularProgressIndicator(strokeWidth: 2)))
                    : feed.items.isNotEmpty && !feed.hasNextPage
                        ? Center(child: Text('That is everything.', style: text.bodySmall?.copyWith(color: Brand.faint)))
                        : const SizedBox.shrink(),
              ),
            ),
            // Room for the floating nav and the Post button raised above it.
            const SliverToBoxAdapter(child: SizedBox(height: AboveNavFabLocation.listEndPadding)),
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
      floatingActionButtonLocation: const AboveNavFabLocation(),
    );
  }
}


extension on _FeedPageState {
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
                icon: Icons.auto_awesome_rounded,
                title: 'Ask FoundU first',
                body: 'Describe it and I will check what has already been handed in.',
                onTap: () { Navigator.of(sheet).pop(); context.push('/ask'); },
              ),
              const SizedBox(height: 10),
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

/// The way in to Ask FoundU from the feed - the first thing someone who has just lost
/// something should see, above the reports of what other people have lost.
class _AskBanner extends StatelessWidget {
  const _AskBanner();

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Material(
      color: Brand.forest,
      borderRadius: BorderRadius.circular(Brand.radiusCard),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/ask'),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 14, 12, 14),
          child: Row(
            children: [
              Container(
                width: 40,
                height: 40,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: .10),
                  borderRadius: BorderRadius.circular(14),
                ),
                child: const FlameMark(size: 32),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Lost something?', style: text.titleMedium?.copyWith(color: Colors.white)),
                    const SizedBox(height: 2),
                    Text(
                      'Ask FoundU - it checks what has been handed in.',
                      style: text.bodySmall?.copyWith(color: Colors.white70),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.arrow_forward_rounded, color: Colors.white70),
            ],
          ),
        ),
      ),
    );
  }
}
