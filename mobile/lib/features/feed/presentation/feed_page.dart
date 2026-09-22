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
        onRefresh: () => ref.read(feedControllerProvider.notifier).refresh(),
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
                            Text(firstName == null ? 'Lost feed' : 'Hello, $firstName', style: text.headlineSmall),
                            const SizedBox(height: 2),
                            Text('What people have lost around campus', style: text.bodyMedium?.copyWith(color: Brand.muted)),
                          ],
                        ),
                      ),
                      const FoundUMark(size: 44),
                    ],
                  ),
                  const SizedBox(height: 18),
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
            // Room for the floating nav.
            const SliverToBoxAdapter(child: SizedBox(height: 96)),
          ],
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        heroTag: 'report',
        backgroundColor: Brand.forest,
        foregroundColor: Colors.white,
        elevation: 0,
        shape: const StadiumBorder(),
        onPressed: () => context.push('/reports/new'),
        icon: const Icon(Icons.add_rounded),
        label: const Text('Report lost'),
      ),
      floatingActionButtonLocation: FloatingActionButtonLocation.endFloat,
    );
  }
}
