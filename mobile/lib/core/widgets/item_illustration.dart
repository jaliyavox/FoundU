import 'package:flutter/material.dart';

import '../theme/brand.dart';

/// Monoline artwork for a post with no photo, chosen from the item type - the same set the
/// web feed draws, so a card looks the same in both places. Everything is a stroke on the
/// current colour: it takes whatever ink the card gives it.
class ItemIllustration extends StatelessWidget {
  const ItemIllustration({
    super.key,
    required this.itemType,
    required this.category,
    this.size = 72,
    this.color = Brand.sage,
  });

  final String itemType;
  final String category;
  final double size;
  final Color color;

  static ItemArt artFor(String itemType, String category) {
    final haystack = '$itemType $category'.toLowerCase();
    for (final entry in _byKeyword) {
      if (haystack.contains(entry.$1)) return entry.$2;
    }
    return ItemArt.package;
  }

  @override
  Widget build(BuildContext context) {
    return ExcludeSemantics(
      child: SizedBox.square(
        dimension: size,
        child: CustomPaint(painter: _ArtPainter(artFor(itemType, category), color)),
      ),
    );
  }
}

enum ItemArt { backpack, wallet, phone, keys, glasses, book, package }

const _byKeyword = <(String, ItemArt)>[
  ('backpack', ItemArt.backpack),
  ('bag', ItemArt.backpack),
  ('wallet', ItemArt.wallet),
  ('purse', ItemArt.wallet),
  ('card', ItemArt.wallet),
  ('licence', ItemArt.wallet),
  ('license', ItemArt.wallet),
  ('phone', ItemArt.phone),
  ('laptop', ItemArt.phone),
  ('electronic', ItemArt.phone),
  ('headphone', ItemArt.phone),
  ('key', ItemArt.keys),
  ('glass', ItemArt.glasses),
  ('book', ItemArt.book),
  ('document', ItemArt.book),
  ('stationery', ItemArt.book),
];

class _ArtPainter extends CustomPainter {
  _ArtPainter(this.art, this.color);
  final ItemArt art;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    canvas.scale(size.width / 64);
    final p = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;
    final faint = Paint()
      ..color = color.withValues(alpha: 0.45)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round;

    RRect rr(double x, double y, double w, double h, double r) =>
        RRect.fromRectAndRadius(Rect.fromLTWH(x, y, w, h), Radius.circular(r));

    switch (art) {
      case ItemArt.backpack:
        canvas.drawRRect(rr(16, 22, 32, 32, 9), p);
        canvas.drawPath(Path()..moveTo(16, 32)..quadraticBezierTo(32, 20, 48, 32), p);
        canvas.drawPath(Path()..moveTo(25, 22)..lineTo(25, 18)..arcToPoint(const Offset(39, 18), radius: const Radius.circular(7))..lineTo(39, 22), p);
        canvas.drawRRect(rr(24, 38, 16, 10, 4), p);
        canvas.drawLine(const Offset(32, 38), const Offset(32, 48), faint);
      case ItemArt.wallet:
        canvas.drawRRect(rr(12, 20, 40, 26, 6), p);
        canvas.drawLine(const Offset(12, 28), const Offset(52, 28), faint);
        canvas.drawCircle(const Offset(42, 37), 3.5, p);
        canvas.drawPath(Path()..moveTo(18, 20)..lineTo(18, 17)..arcToPoint(const Offset(21, 14), radius: const Radius.circular(3))..lineTo(41, 14), faint);
      case ItemArt.phone:
        canvas.drawRRect(rr(20, 10, 24, 44, 6), p);
        canvas.drawLine(const Offset(28, 15), const Offset(36, 15), faint);
        canvas.drawCircle(const Offset(32, 48), 2, p);
      case ItemArt.keys:
        canvas.drawCircle(const Offset(24, 24), 9, p);
        canvas.drawCircle(const Offset(24, 24), 3, faint);
        canvas.drawPath(Path()..moveTo(31, 31)..lineTo(50, 50)..moveTo(44, 44)..lineTo(48, 40)..moveTo(38, 38)..lineTo(42, 34), p);
      case ItemArt.glasses:
        canvas.drawCircle(const Offset(21, 34), 9, p);
        canvas.drawCircle(const Offset(43, 34), 9, p);
        canvas.drawPath(Path()..moveTo(30, 33)..quadraticBezierTo(32, 30, 34, 33), p);
        canvas.drawLine(const Offset(12, 32), const Offset(8, 26), faint);
        canvas.drawLine(const Offset(52, 32), const Offset(56, 26), faint);
      case ItemArt.book:
        canvas.drawRRect(rr(16, 12, 32, 40, 4), p);
        canvas.drawLine(const Offset(24, 12), const Offset(24, 52), faint);
        canvas.drawLine(const Offset(30, 24), const Offset(42, 24), faint);
        canvas.drawLine(const Offset(30, 32), const Offset(42, 32), faint);
      case ItemArt.package:
        canvas.drawPath(Path()..moveTo(32, 10)..lineTo(54, 22)..lineTo(54, 44)..lineTo(32, 56)..lineTo(10, 44)..lineTo(10, 22)..close(), p);
        canvas.drawPath(Path()..moveTo(10, 22)..lineTo(32, 34)..lineTo(54, 22)..moveTo(32, 34)..lineTo(32, 56), faint);
    }
  }

  @override
  bool shouldRepaint(covariant _ArtPainter old) => old.art != art || old.color != color;
}
