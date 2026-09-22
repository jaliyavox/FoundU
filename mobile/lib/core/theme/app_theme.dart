import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'brand.dart';

/// One place the whole app takes its look from. Pages should reach for the theme, never for
/// a literal colour - the two teammates' screens pick this up without being touched.
ThemeData buildFoundUTheme() {
  final base = ThemeData(useMaterial3: true, brightness: Brightness.light);

  // Geist, the same face as the web app. google_fonts caches it and falls back to the
  // system sans if it cannot be fetched, so an offline first launch is still legible.
  final textTheme = GoogleFonts.geistTextTheme(base.textTheme).apply(
    bodyColor: Brand.text,
    displayColor: Brand.text,
  );

  const scheme = ColorScheme(
    brightness: Brightness.light,
    primary: Brand.forest,
    onPrimary: Colors.white,
    primaryContainer: Brand.mist,
    onPrimaryContainer: Brand.forest,
    secondary: Brand.green,
    onSecondary: Brand.ink,
    secondaryContainer: Brand.mist,
    onSecondaryContainer: Brand.forest,
    tertiary: Brand.sage,
    onTertiary: Brand.ink,
    error: Brand.danger,
    onError: Colors.white,
    surface: Brand.surface,
    onSurface: Brand.text,
    onSurfaceVariant: Brand.muted,
    outline: Brand.line,
    outlineVariant: Brand.lineSoft,
    surfaceContainerHighest: Brand.surfaceTint,
    inverseSurface: Brand.ink,
    onInverseSurface: Colors.white,
  );

  return base.copyWith(
    colorScheme: scheme,
    scaffoldBackgroundColor: Brand.paper,
    textTheme: textTheme.copyWith(
      // Headings track tighter, like the web's `tracking-tight`.
      headlineLarge: textTheme.headlineLarge?.copyWith(fontWeight: FontWeight.w600, letterSpacing: -0.8),
      headlineMedium: textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w600, letterSpacing: -0.6),
      headlineSmall: textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w600, letterSpacing: -0.4),
      titleLarge: textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w600, letterSpacing: -0.3),
      titleMedium: textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w600),
      labelLarge: textTheme.labelLarge?.copyWith(fontWeight: FontWeight.w600),
    ),
    appBarTheme: AppBarTheme(
      backgroundColor: Brand.paper,
      foregroundColor: Brand.text,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: false,
      titleTextStyle: textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w600, letterSpacing: -0.3),
    ),
    cardTheme: CardThemeData(
      color: Brand.surface,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(Brand.radiusCard),
        side: const BorderSide(color: Brand.lineSoft),
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Brand.surface,
      contentPadding: const EdgeInsets.symmetric(horizontal: 18, vertical: 16),
      hintStyle: const TextStyle(color: Brand.faint),
      labelStyle: const TextStyle(color: Brand.muted),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(Brand.radiusControl),
        borderSide: const BorderSide(color: Brand.line),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(Brand.radiusControl),
        borderSide: const BorderSide(color: Brand.line),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(Brand.radiusControl),
        borderSide: const BorderSide(color: Brand.green, width: 2),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(Brand.radiusControl),
        borderSide: const BorderSide(color: Brand.danger),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(Brand.radiusControl),
        borderSide: const BorderSide(color: Brand.danger, width: 2),
      ),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: Brand.forest,
        foregroundColor: Colors.white,
        minimumSize: const Size.fromHeight(54),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(Brand.radiusControl)),
        textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
      ),
    ),
    elevatedButtonTheme: ElevatedButtonThemeData(
      style: ElevatedButton.styleFrom(
        backgroundColor: Brand.forest,
        foregroundColor: Colors.white,
        elevation: 0,
        minimumSize: const Size.fromHeight(54),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(Brand.radiusControl)),
        textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: Brand.text,
        minimumSize: const Size.fromHeight(54),
        side: const BorderSide(color: Brand.line),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(Brand.radiusControl)),
        textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: Brand.forest,
        textStyle: const TextStyle(fontWeight: FontWeight.w600),
      ),
    ),
    chipTheme: const ChipThemeData(
      backgroundColor: Brand.surfaceTint,
      selectedColor: Brand.ink,
      labelStyle: TextStyle(color: Brand.text, fontWeight: FontWeight.w500),
      secondaryLabelStyle: TextStyle(color: Colors.white, fontWeight: FontWeight.w500),
      side: BorderSide.none,
      shape: StadiumBorder(),
      padding: EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      showCheckmark: false,
    ),
    dividerTheme: const DividerThemeData(color: Brand.lineSoft, thickness: 1, space: 1),
    bottomSheetTheme: const BottomSheetThemeData(
      backgroundColor: Brand.surface,
      showDragHandle: true,
      dragHandleColor: Brand.line,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
    ),
    snackBarTheme: SnackBarThemeData(
      backgroundColor: Brand.ink,
      contentTextStyle: const TextStyle(color: Colors.white),
      behavior: SnackBarBehavior.floating,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(Brand.radiusControl)),
    ),
    progressIndicatorTheme: const ProgressIndicatorThemeData(color: Brand.forest),
  );
}
