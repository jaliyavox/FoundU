import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/brand.dart';
import '../../../core/widgets/flame_mark.dart';
import '../data/feed_models.dart';
import 'found_feed_controller.dart';
import 'found_post_sheet.dart';

/// The newest things people are holding, across the top of the lost board.
///
/// A row rather than a second tab: an item somebody has in their bag right now is worth
/// seeing before you scroll, and the full board is one tap away.
class FreshFindsStrip extends ConsumerWidget {
  const FreshFindsStrip({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final found = ref.watch(foundFeedControllerProvider);
    final text = Theme.of(context).textTheme;

    // A strip nobody can act on is noise above the board people came for.
    if (found.error != null || (found.isEmpty && !found.isLoading)) return const SizedBox.shrink();

    return Container(
      // Its own light panel, so the dark tiles read as resting on something. The row still
      // scrolls past the right edge, so only the left and vertical padding sit here.
      padding: const EdgeInsets.fromLTRB(16, 16, 0, 16),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Colors.white, Brand.mist],
        ),
        borderRadius: const BorderRadius.horizontal(left: Radius.circular(Brand.radiusCard)),
        border: Border.all(color: Brand.lineSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.only(right: 16),
            child: Row(
              children: [
                Container(
                  width: 40,
                  height: 40,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    gradient: const LinearGradient(
                      begin: Alignment.topCenter,
                      end: Alignment.bottomCenter,
                      colors: [Color(0xFFFFFBEB), Color(0xFFFFEDD5)],
                    ),
                    borderRadius: BorderRadius.circular(14),
                    border: Border.all(color: const Color(0x1AF97316)),
                  ),
                  child: const FlameMark(size: 34),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Fresh finds', style: text.titleLarge),
                      const SizedBox(height: 2),
                      Text(
                        'I found this just now - who owns this?',
                        style: text.bodySmall?.copyWith(color: Brand.muted),
                      ),
                    ],
                  ),
                ),
                TextButton(
                  onPressed: () => context.push('/home/found'),
                  child: const Text('See more'),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          SizedBox(
            height: 156,
            child: found.isLoading && found.items.isEmpty
                ? ListView.separated(
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.only(right: 16),
                    itemCount: 3,
                    separatorBuilder: (_, __) => const SizedBox(width: 12),
                    itemBuilder: (_, __) => const _SkeletonTile(),
                  )
                : ListView.separated(
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.only(right: 16),
                    itemCount: found.items.length,
                    separatorBuilder: (_, __) => const SizedBox(width: 12),
                    itemBuilder: (context, index) {
                      final post = found.items[index];
                      return _FindTile(post: post, onOpen: () => showFoundPostDetail(context, post));
                    },
                  ),
          ),
        ],
      ),
    );
  }
}

class _FindTile extends StatelessWidget {
  const _FindTile({required this.post, required this.onOpen});

  final FoundPost post;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final meta = [post.primaryColor, post.foundLocationName].where((v) => v != null && v.isNotEmpty).join(' · ');

    return SizedBox(
      width: 220,
      child: Material(
        color: Brand.forest,
        borderRadius: BorderRadius.circular(Brand.radiusCard),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onOpen,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: const Color(0xFFF2C14E),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    post.isMine ? 'Your post' : 'Not at a desk yet',
                    style: text.labelSmall?.copyWith(color: Brand.ink, fontWeight: FontWeight.w600),
                  ),
                ),
                const SizedBox(height: 12),
                Text(
                  post.itemTypeName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: text.titleMedium?.copyWith(color: Colors.white),
                ),
                if (meta.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(
                    meta,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: text.bodySmall?.copyWith(color: Colors.white70),
                  ),
                ],
                const Spacer(),
                Text(
                  post.description,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: text.bodySmall?.copyWith(color: Colors.white.withValues(alpha: .75)),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _SkeletonTile extends StatelessWidget {
  const _SkeletonTile();

  @override
  Widget build(BuildContext context) => Container(
        width: 220,
        decoration: BoxDecoration(
          color: Brand.surfaceTint,
          borderRadius: BorderRadius.circular(Brand.radiusCard),
        ),
      );
}
