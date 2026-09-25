import 'package:flutter/material.dart';

import '../theme/brand.dart';

/// A rounded card on the paper ground. One shape for every section, so screens read as a
/// set of the same object rather than a pile of different ones.
class Panel extends StatelessWidget {
  const Panel({super.key, required this.child, this.padding = const EdgeInsets.all(20), this.color, this.onTap});

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color? color;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final body = Padding(padding: padding, child: child);
    return Material(
      color: color ?? Brand.surface,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(Brand.radiusCard),
        side: const BorderSide(color: Brand.lineSoft),
      ),
      clipBehavior: Clip.antiAlias,
      child: onTap == null ? body : InkWell(onTap: onTap, child: body),
    );
  }
}

/// A small label with a dot - status as a shape as well as a word.
class StatusChip extends StatelessWidget {
  const StatusChip(this.label, {super.key, this.tone = ChipTone.neutral});

  final String label;
  final ChipTone tone;

  @override
  Widget build(BuildContext context) {
    final (bg, fg) = switch (tone) {
      ChipTone.good => (Brand.mist, Brand.forest),
      ChipTone.action => (const Color(0xFFFFF3DD), Brand.warning),
      ChipTone.bad => (const Color(0xFFFBE9E7), Brand.danger),
      ChipTone.neutral => (Brand.surfaceTint, Brand.muted),
      ChipTone.dark => (Brand.ink, Colors.white),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(999)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(width: 6, height: 6, decoration: BoxDecoration(color: fg, shape: BoxShape.circle)),
          const SizedBox(width: 6),
          Text(label, style: TextStyle(color: fg, fontSize: 12, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}

enum ChipTone { good, action, bad, neutral, dark }

/// The dark, full-width primary action - "Book a tour" in the reference, "I found this" here.
class InkButton extends StatelessWidget {
  const InkButton({super.key, required this.label, required this.onPressed, this.icon, this.busy = false});

  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      style: FilledButton.styleFrom(
        backgroundColor: Brand.ink,
        minimumSize: const Size.fromHeight(58),
        shape: const StadiumBorder(),
      ),
      onPressed: busy ? null : onPressed,
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          if (busy)
            const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
          else if (icon != null)
            Icon(icon, size: 20),
          if (busy || icon != null) const SizedBox(width: 10),
          Text(label),
        ],
      ),
    );
  }
}

/// A row of selectable pills - the "Asia · Europe · South America" strip.
class ChipRow<T> extends StatelessWidget {
  const ChipRow({super.key, required this.options, required this.selected, required this.onSelect, required this.labelOf});

  final List<T> options;
  final T selected;
  final ValueChanged<T> onSelect;
  final String Function(T) labelOf;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 44,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 20),
        itemCount: options.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (context, index) {
          final option = options[index];
          final isSelected = option == selected;
          return AnimatedContainer(
            duration: const Duration(milliseconds: 200),
            decoration: BoxDecoration(
              color: isSelected ? Brand.ink : Brand.surfaceTint,
              borderRadius: BorderRadius.circular(999),
            ),
            child: Material(
              color: Colors.transparent,
              child: InkWell(
                borderRadius: BorderRadius.circular(999),
                onTap: () => onSelect(option),
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 11),
                  child: Text(
                    labelOf(option),
                    style: TextStyle(
                      color: isSelected ? Colors.white : Brand.text,
                      fontWeight: FontWeight.w500,
                      fontSize: 14,
                    ),
                  ),
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}
