import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';
import 'found_feed_controller.dart';
import 'found_post_card.dart';
import 'found_post_sheet.dart';

/// The board behind the "Fresh finds" strip: everything students have picked up and not yet
/// walked to a desk. Nothing here can be claimed - recognising yours asks the finder to hand
/// it in, and the desk's questions still decide who gets it.
class FoundBoardPage extends ConsumerStatefulWidget {
  const FoundBoardPage({super.key});

  @override
  ConsumerState<FoundBoardPage> createState() => _FoundBoardPageState();
}

class _FoundBoardPageState extends ConsumerState<FoundBoardPage> {
  final _scroll = ScrollController();
  final _search = TextEditingController();
  Timer? _debounce;

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_scroll.position.pixels > _scroll.position.maxScrollExtent - 600) {
        ref.read(foundFeedControllerProvider.notifier).loadMore();
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
      ref.read(foundFeedControllerProvider.notifier).setSearch(value);
    });
  }

  @override
  Widget build(BuildContext context) {
    final found = ref.watch(foundFeedControllerProvider);
    final text = Theme.of(context).textTheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Fresh finds')),
      body: RefreshIndicator(
        color: Brand.forest,
        onRefresh: () => ref.read(foundFeedControllerProvider.notifier).refresh(),
        child: CustomScrollView(
          controller: _scroll,
          physics: const AlwaysScrollableScrollPhysics(),
          slivers: [
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
              sliver: SliverList.list(children: [
                Text('What people have found', style: text.headlineSmall),
                const SizedBox(height: 4),
                Text(
                  'Picked up by a student and not yet at a desk. Recognise yours? Say so, and '
                  'the finder is asked to hand it in.',
                  style: text.bodyMedium?.copyWith(color: Brand.muted),
                ),
                const SizedBox(height: 16),
                TextField(
                  controller: _search,
                  onChanged: _onSearchChanged,
                  textInputAction: TextInputAction.search,
                  decoration: const InputDecoration(
                    hintText: 'Search found items',
                    prefixIcon: Icon(Icons.search_rounded, color: Brand.muted),
                  ),
                ),
                const SizedBox(height: 18),
              ]),
            ),
            if (found.error != null)
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  child: Panel(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Could not load the board', style: text.titleMedium),
                        const SizedBox(height: 4),
                        Text('Check your connection, then pull to refresh.',
                            style: text.bodyMedium?.copyWith(color: Brand.muted)),
                      ],
                    ),
                  ),
                ),
              )
            else if (found.isEmpty)
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(20, 24, 20, 0),
                  child: Column(
                    children: [
                      const Icon(Icons.front_hand_outlined, size: 36, color: Brand.faint),
                      const SizedBox(height: 10),
                      Text('Nothing posted yet', style: text.titleMedium),
                      const SizedBox(height: 4),
                      Text(
                        'When someone finds something and posts it, it shows here until they hand it in.',
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
                  itemCount: found.items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 14),
                  itemBuilder: (context, index) {
                    final post = found.items[index];
                    return FoundPostCard(post: post, onOpen: () => showFoundPostDetail(context, post));
                  },
                ),
              ),
            SliverToBoxAdapter(
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 20),
                child: found.isLoading
                    ? const Center(child: SizedBox.square(dimension: 22, child: CircularProgressIndicator(strokeWidth: 2)))
                    : found.items.isNotEmpty && !found.hasNextPage
                        ? Center(child: Text('That is everything.', style: text.bodySmall?.copyWith(color: Brand.faint)))
                        : const SizedBox.shrink(),
              ),
            ),
            const SliverToBoxAdapter(child: SizedBox(height: 40)),
          ],
        ),
      ),
    );
  }
}
