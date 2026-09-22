import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../../core/theme/brand.dart';

/// The three drawings the sign-up wizard walks through, in the same monoline hand as the
/// logo and the feed placeholders. Each one moves a little on its own - the loop is slow
/// and small, there to make the screen feel alive rather than to be watched.
enum OnboardingScene { identity, campus, secure }

class OnboardingIllustration extends StatefulWidget {
  const OnboardingIllustration({super.key, required this.scene, this.size = 220});

  final OnboardingScene scene;
  final double size;

  @override
  State<OnboardingIllustration> createState() => _OnboardingIllustrationState();
}

class _OnboardingIllustrationState extends State<OnboardingIllustration> with SingleTickerProviderStateMixin {
  late final AnimationController _loop = AnimationController(vsync: this, duration: const Duration(seconds: 4))..repeat();

  @override
  void dispose() {
    _loop.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final reduceMotion = MediaQuery.disableAnimationsOf(context);
    return ExcludeSemantics(
      child: SizedBox.square(
        dimension: widget.size,
        child: AnimatedBuilder(
          animation: _loop,
          builder: (context, _) => CustomPaint(
            painter: _ScenePainter(widget.scene, reduceMotion ? 0.25 : _loop.value),
          ),
        ),
      ),
    );
  }
}

class _ScenePainter extends CustomPainter {
  _ScenePainter(this.scene, this.t);
  final OnboardingScene scene;
  final double t;

  // A gentle 0..1..0 wave from the loop, for bobbing and breathing.
  double get wave => (1 - math.cos(t * 2 * math.pi)) / 2;

  @override
  void paint(Canvas canvas, Size size) {
    canvas.scale(size.width / 200);

    final ink = Paint()
      ..color = Brand.forest
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.4
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;
    final soft = Paint()
      ..color = Brand.green
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.4
      ..strokeCap = StrokeCap.round;
    final mist = Paint()..color = Brand.mist;
    final sage = Paint()..color = Brand.sage;

    // Ground: a soft disc every scene sits on.
    canvas.drawCircle(const Offset(100, 108), 78, mist);

    switch (scene) {
      case OnboardingScene.identity:
        _identity(canvas, ink, soft, sage);
      case OnboardingScene.campus:
        _campus(canvas, ink, soft, sage);
      case OnboardingScene.secure:
        _secure(canvas, ink, soft, sage);
    }
  }

  /// A student card with a face, and a small tag floating beside it.
  void _identity(Canvas c, Paint ink, Paint soft, Paint sage) {
    final bob = (wave - 0.5) * 6;
    c.save();
    c.translate(0, bob);
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(52, 62, 96, 68), const Radius.circular(12)), Paint()..color = Colors.white);
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(52, 62, 96, 68), const Radius.circular(12)), ink);
    c.drawCircle(const Offset(80, 92), 12, sage);
    c.drawCircle(const Offset(80, 92), 12, ink);
    c.drawPath(Path()..moveTo(62, 122)..quadraticBezierTo(80, 104, 98, 122), ink);
    c.drawLine(const Offset(106, 84), const Offset(136, 84), soft);
    c.drawLine(const Offset(106, 96), const Offset(128, 96), soft);
    c.drawLine(const Offset(106, 108), const Offset(132, 108), soft..color = Brand.sage);
    c.restore();

    // The tag orbits slowly.
    final a = t * 2 * math.pi;
    final tag = Offset(100 + math.cos(a) * 62, 96 + math.sin(a) * 30);
    c.drawCircle(tag, 9, Paint()..color = Brand.green);
    c.drawCircle(tag, 9, ink);
    c.drawCircle(tag, 2.2, Paint()..color = Colors.white);
  }

  /// Campus buildings with a pin that drops and settles.
  void _campus(Canvas c, Paint ink, Paint soft, Paint sage) {
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(44, 96, 34, 44), const Radius.circular(4)), Paint()..color = Colors.white);
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(44, 96, 34, 44), const Radius.circular(4)), ink);
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(84, 80, 44, 60), const Radius.circular(4)), Paint()..color = Colors.white);
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(84, 80, 44, 60), const Radius.circular(4)), ink);
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(134, 104, 26, 36), const Radius.circular(4)), Paint()..color = Colors.white);
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(134, 104, 26, 36), const Radius.circular(4)), ink);
    for (final x in [92.0, 104.0, 116.0]) {
      for (final y in [90.0, 104.0, 118.0]) {
        c.drawRect(Rect.fromLTWH(x, y, 6, 8), sage);
      }
    }
    c.drawLine(const Offset(36, 140), const Offset(168, 140), ink);

    // The pin drops in, overshoots a touch, and rests - once per loop.
    final drop = Curves.easeOutBack.transform((t * 1.6).clamp(0.0, 1.0));
    final y = 30 + drop * 34;
    final pin = Path()
      ..moveTo(106, y + 18)
      ..arcTo(Rect.fromCircle(center: Offset(106, y), radius: 11), math.pi * 0.8, -math.pi * 1.6, false)
      ..close();
    c.drawPath(pin, Paint()..color = Brand.green);
    c.drawPath(pin, ink);
    c.drawCircle(Offset(106, y - 1), 4, Paint()..color = Colors.white);
    // Its shadow grows as it lands.
    c.drawOval(Rect.fromCenter(center: const Offset(106, 80), width: 10 + drop * 12, height: 3 + drop * 2), Paint()..color = Brand.forest.withValues(alpha: 0.12 * drop));
  }

  /// A shield that breathes, with a check drawn across it.
  void _secure(Canvas c, Paint ink, Paint soft, Paint sage) {
    final breathe = 1 + (wave - 0.5) * 0.04;
    c.save();
    c.translate(100, 104);
    c.scale(breathe);
    c.translate(-100, -104);
    final shield = Path()
      ..moveTo(100, 54)
      ..lineTo(140, 68)
      ..cubicTo(140, 108, 124, 136, 100, 150)
      ..cubicTo(76, 136, 60, 108, 60, 68)
      ..close();
    c.drawPath(shield, Paint()..color = Colors.white);
    c.drawPath(shield, ink);
    final inner = Path()
      ..moveTo(100, 66)
      ..lineTo(130, 76)
      ..cubicTo(130, 106, 118, 126, 100, 137)
      ..cubicTo(82, 126, 70, 106, 70, 76)
      ..close();
    c.drawPath(inner, sage);
    c.restore();

    // The check draws itself in over the first part of each loop.
    final progress = Curves.easeOut.transform((t * 2.2).clamp(0.0, 1.0));
    final check = Path()..moveTo(84, 104)..lineTo(96, 116)..lineTo(118, 90);
    final metric = check.computeMetrics().first;
    c.drawPath(metric.extractPath(0, metric.length * progress), ink..strokeWidth = 3.4);
  }

  @override
  bool shouldRepaint(covariant _ScenePainter old) => old.t != t || old.scene != scene;
}
