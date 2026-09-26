import 'package:flutter/material.dart';

import '../../../core/theme/brand.dart';
import '../../../core/widgets/item_illustration.dart';
import '../data/feed_models.dart';
import 'feed_controller.dart';

/// A finder's post on the board. Same shape as a lost card; the amber tag is the one
/// difference a reader needs - this thing is in somebody's bag, not at a desk.
class FoundPostCard extends StatelessWidget {
  const FoundPostCard({super.key, required this.post, required this.onOpen});

  final FoundPost post;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final meta = [post.primaryColor, post.foundLocationName].whereType<String>().join(' · ');

    return Material(
      color: Brand.surface,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(Brand.radiusCard),
        side: const BorderSide(color: Brand.lineSoft),
      ),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onOpen,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            AspectRatio(
              aspectRatio: 16 / 9,
              child: Stack(
                fit: StackFit.expand,
                children: [
                  DecoratedBox(
                    decoration: const BoxDecoration(
                      gradient: LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [Color(0xFF1B2A1D), Brand.ink]),
                    ),
                    child: Center(child: ItemIllustration(itemType: post.itemTypeName, category: post.categoryName, size: 72)),
                  ),
                  Positioned(
                    top: 12,
                    left: 12,
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                      decoration: BoxDecoration(color: const Color(0xFFFFC94A), borderRadius: BorderRadius.circular(999)),
                      child: Text(
                        post.isMine ? 'Your post' : 'Not at a desk yet',
                        style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: Brand.ink),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(18, 16, 18, 18),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(post.itemTypeName, style: text.titleLarge),
                  if (meta.isNotEmpty) ...[
                    const SizedBox(height: 2),
                    Text(meta, style: text.bodyMedium?.copyWith(color: Brand.muted)),
                  ],
                  const SizedBox(height: 8),
                  Text(post.description, maxLines: 2, overflow: TextOverflow.ellipsis,
                      style: text.bodyMedium?.copyWith(color: Brand.text.withValues(alpha: 0.8), height: 1.45)),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      const Icon(Icons.front_hand_outlined, size: 14, color: Brand.green),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text('Found by ${post.isMine ? 'you' : post.postedByName}',
                            overflow: TextOverflow.ellipsis, style: text.bodySmall?.copyWith(color: Brand.muted)),
                      ),
                      Text(timeAgo(post.createdAt), style: text.bodySmall?.copyWith(color: Brand.faint)),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
