import 'package:flutter/material.dart';

import '../../../core/theme/brand.dart';
import '../../../core/widgets/item_illustration.dart';
import '../data/feed_models.dart';
import '../data/feed_repository.dart';
import 'feed_controller.dart';

/// One post on the feed. Image-led, like the reference: the photo (or the item's line
/// drawing on a dark tinted block when there is none) carries the card, and the words sit
/// under it in a fixed rhythm so every card is the same height for the same content.
class FeedCard extends StatelessWidget {
  const FeedCard({super.key, required this.item, required this.onOpen});

  final FeedItem item;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final meta = [item.primaryColor, item.lastSeenLocationName].whereType<String>().join(' · ');

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
              aspectRatio: 4 / 3,
              child: Stack(
                fit: StackFit.expand,
                children: [
                  ItemMedia(item: item),
                  if (item.isMine)
                    const Positioned(top: 12, left: 12, child: _Tag('Your post')),
                  Positioned(
                    right: 12,
                    bottom: 12,
                    child: Container(
                      width: 40,
                      height: 40,
                      decoration: const BoxDecoration(color: Brand.ink, shape: BoxShape.circle),
                      child: const Icon(Icons.arrow_outward_rounded, color: Colors.white, size: 18),
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
                  Text(item.itemTypeName, style: text.titleLarge),
                  if (meta.isNotEmpty) ...[
                    const SizedBox(height: 2),
                    Text(meta, style: text.bodyMedium?.copyWith(color: Brand.muted)),
                  ],
                  const SizedBox(height: 8),
                  Text(
                    item.description,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: text.bodyMedium?.copyWith(color: Brand.text.withValues(alpha: 0.8), height: 1.45),
                  ),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      _Initials(name: item.postedByName),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          item.isMine ? 'You' : item.postedByName,
                          overflow: TextOverflow.ellipsis,
                          style: text.bodySmall?.copyWith(color: Brand.muted),
                        ),
                      ),
                      Text(timeAgo(item.createdAt), style: text.bodySmall?.copyWith(color: Brand.faint)),
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

/// The photo when there is one, the illustration when there is not.
class ItemMedia extends StatelessWidget {
  const ItemMedia({super.key, required this.item, this.illustrationSize = 84});

  final FeedItem item;
  final double illustrationSize;

  @override
  Widget build(BuildContext context) {
    final photo = item.photoUrl;
    if (photo != null) {
      return Image.network(
        resolvePhotoUrl(photo),
        fit: BoxFit.cover,
        errorBuilder: (_, __, ___) => _Placeholder(item: item, size: illustrationSize),
        loadingBuilder: (context, child, progress) =>
            progress == null ? child : _Placeholder(item: item, size: illustrationSize),
      );
    }
    return _Placeholder(item: item, size: illustrationSize);
  }
}

class _Placeholder extends StatelessWidget {
  const _Placeholder({required this.item, required this.size});
  final FeedItem item;
  final double size;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFF1B2A1D), Brand.ink],
        ),
      ),
      child: Center(
        child: ItemIllustration(itemType: item.itemTypeName, category: item.categoryName, size: size),
      ),
    );
  }
}

class _Initials extends StatelessWidget {
  const _Initials({required this.name});
  final String name;

  @override
  Widget build(BuildContext context) {
    final parts = name.trim().split(RegExp(r'\s+'));
    final initials = parts.take(2).map((p) => p.isEmpty ? '' : p[0]).join().toUpperCase();
    return Container(
      width: 22,
      height: 22,
      decoration: const BoxDecoration(color: Brand.mist, shape: BoxShape.circle),
      alignment: Alignment.center,
      child: Text(initials, style: const TextStyle(fontSize: 9, fontWeight: FontWeight.w700, color: Brand.forest)),
    );
  }
}

class _Tag extends StatelessWidget {
  const _Tag(this.label);
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(color: Colors.white.withValues(alpha: 0.92), borderRadius: BorderRadius.circular(999)),
      child: Text(label, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: Brand.ink)),
    );
  }
}
