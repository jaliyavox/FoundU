import 'dart:math' as math;

import 'package:flutter/material.dart';

/// The "Fresh finds" flame: a fire glyph over a soft halo, each moving on its own cycle so
/// the flicker never reads as a loop.
///
/// Two controllers rather than one: a single cycle makes the whole thing pulse in time,
/// which looks like a heartbeat, not a flame. Stops still when the platform asks for
/// reduced motion.
class FlameMark extends StatefulWidget {
  const FlameMark({super.key, this.size = 40});

  final double size;

  @override
  State<FlameMark> createState() => _FlameMarkState();
}

class _FlameMarkState extends State<FlameMark> with TickerProviderStateMixin {
  late final AnimationController _body = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 2400),
  );
  late final AnimationController _core = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1500),
  );

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (MediaQuery.disableAnimationsOf(context)) {
      _body.stop();
      _core.stop();
    } else if (!_body.isAnimating) {
      _body.repeat();
      _core.repeat();
    }
  }

  @override
  void dispose() {
    _body.dispose();
    _core.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final icon = widget.size * 0.6;

    return SizedBox(
      width: widget.size,
      height: widget.size,
      child: AnimatedBuilder(
        animation: Listenable.merge([_body, _core]),
        builder: (context, _) {
          // Sine rather than a curve, so the loop has no seam where it restarts.
          final breathe = math.sin(_body.value * 2 * math.pi);
          final flicker = math.sin(_core.value * 2 * math.pi);

          return Stack(
            alignment: Alignment.center,
            children: [
              Transform.scale(
                scale: 1 + breathe * 0.09,
                child: Container(
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    gradient: RadialGradient(
                      colors: [
                        const Color(0xFFFB923C).withValues(alpha: .34 + flicker * .08),
                        const Color(0xFFFB923C).withValues(alpha: 0),
                      ],
                    ),
                  ),
                ),
              ),
              Transform.scale(
                scaleX: 1 - flicker * 0.06,
                scaleY: 1 + flicker * 0.08,
                child: ShaderMask(
                  shaderCallback: (bounds) => const LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [Color(0xFFFFC24A), Color(0xFFF97316), Color(0xFFDC2626)],
                    stops: [0, .45, 1],
                  ).createShader(bounds),
                  child: Icon(Icons.local_fire_department_rounded, size: icon, color: Colors.white),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
