import 'package:flutter/material.dart';

/// The FoundU palette, from /docs/design.md. The four greens are fixed brand values and do
/// not change with the theme; the neutrals carry a slight green bias so they sit with the
/// brand rather than beside it.
///
/// Forest is the actionable colour, not the mid green: white on Green (#64BC6D) does not
/// reach 4.5:1, so buttons and links use Forest and Green is kept for accents, focus and
/// small marks.
abstract final class Brand {
  static const mist = Color(0xFFE7F3EA);
  static const sage = Color(0xFFA6D6A6);
  static const green = Color(0xFF64BC6D);
  static const forest = Color(0xFF1F5D20);

  /// Near-white ground with a green cast, matching the web app's `--background`.
  static const paper = Color(0xFFFAFBF9);
  static const surface = Color(0xFFFFFFFF);
  static const surfaceTint = Color(0xFFF2F6F2);

  /// Near-black with a green bias: the dark pill navigation and primary CTAs.
  static const ink = Color(0xFF101711);
  static const inkSoft = Color(0xFF1A241C);

  static const text = Color(0xFF141C15);
  static const muted = Color(0xFF5B6A5D);
  static const faint = Color(0xFF93A395);
  static const line = Color(0xFFDDE5DE);
  static const lineSoft = Color(0xFFEAF0EA);

  static const danger = Color(0xFFB4372B);
  static const warning = Color(0xFF9A6B12);

  /// One radius family: cards 24, controls 16, chips full. Nothing else.
  static const radiusCard = 24.0;
  static const radiusControl = 16.0;
  static const radiusTile = 12.0;
}
