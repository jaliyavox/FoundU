import 'package:flutter/material.dart';

import '../theme/brand.dart';

/// The FoundU mark: a crate of handed-in items with a magnifier asking whose it is. The
/// same drawing as the web logo, painted rather than loaded, so it is crisp at every size
/// and needs no asset. Forest tile, mist artwork, in every theme - the way a printed logo
/// would be.
class FoundUMark extends StatelessWidget {
  const FoundUMark({super.key, this.size = 40});

  final double size;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'FoundU',
      child: SizedBox.square(
        dimension: size,
        child: CustomPaint(painter: _MarkPainter()),
      ),
    );
  }
}

class _MarkPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final s = size.width / 64;
    canvas.scale(s);

    final tile = Paint()..color = Brand.forest;
    canvas.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(0, 0, 64, 64), const Radius.circular(14)), tile);

    final fill = Paint()..color = Brand.mist;
    final stroke = Paint()
      ..color = Brand.mist
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round;

    // Items spilling out of the crate.
    _rotated(canvas, -24, const Offset(22, 24), () {
      canvas.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(16.5, 14, 11, 18), const Radius.circular(2.5)), fill);
    });
    _rotated(canvas, -9, const Offset(29, 23), () {
      canvas.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(25.5, 13.5, 8, 17), const Radius.circular(2)), fill);
    });
    _rotated(canvas, 19, const Offset(41, 23), () {
      canvas.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(34.5, 17, 13.5, 12.5), const Radius.circular(2.5)), fill);
      canvas.drawArc(const Rect.fromLTWH(38, 14.1, 6.8, 6.8), 3.1416, 3.1416, false, stroke);
    });

    // Crate, with the lens knocked out of it.
    final crate = Path()
      ..addRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(12, 29.5, 40, 23), const Radius.circular(2.5)))
      ..addOval(Rect.fromCircle(center: const Offset(31.4, 41.8), radius: 8.4))
      ..fillType = PathFillType.evenOdd;
    canvas.drawPath(crate, fill);

    // Lens ring, question mark, handle.
    canvas.drawCircle(const Offset(31.4, 41.8), 6.6, stroke..strokeWidth = 2.2);
    final q = Path()
      ..moveTo(28.9, 39.6)
      ..cubicTo(28.9, 37.6, 33.9, 37.6, 33.9, 39.8)
      ..cubicTo(33.9, 41.4, 31.4, 41.4, 31.4, 43.2);
    canvas.drawPath(q, stroke..strokeWidth = 1.9);
    canvas.drawCircle(const Offset(31.4, 45.9), 1.05, fill);
    canvas.drawLine(const Offset(36.4, 46.6), const Offset(41.5, 51.6), stroke..strokeWidth = 3);
  }

  void _rotated(Canvas canvas, double degrees, Offset pivot, VoidCallback draw) {
    canvas.save();
    canvas.translate(pivot.dx, pivot.dy);
    canvas.rotate(degrees * 3.1415926 / 180);
    canvas.translate(-pivot.dx, -pivot.dy);
    draw();
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
