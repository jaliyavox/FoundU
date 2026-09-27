import 'package:flutter/material.dart';

import '../theme/brand.dart';

/// The floating dark pill at the bottom of every main screen. The active tab lifts into a
/// white disc, the way the reference design does it, so which screen you are on reads from
/// shape as well as position.
class PillNav extends StatelessWidget {
  const PillNav({
    super.key,
    required this.items,
    required this.selectedIndex,
    required this.onSelect,
  });

  final List<PillNavItem> items;
  final int selectedIndex;
  final ValueChanged<int> onSelect;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      minimum: const EdgeInsets.fromLTRB(20, 0, 20, 16),
      child: Container(
        height: 72,
        padding: const EdgeInsets.all(8),
        decoration: BoxDecoration(
          color: Brand.ink,
          borderRadius: BorderRadius.circular(36),
          boxShadow: [
            BoxShadow(
              color: Brand.ink.withValues(alpha: 0.28),
              blurRadius: 28,
              offset: const Offset(0, 12),
            ),
          ],
        ),
        child: Row(
          children: [
            for (var i = 0; i < items.length; i++)
              Expanded(
                child: _PillNavButton(
                  item: items[i],
                  selected: i == selectedIndex,
                  onTap: () => onSelect(i),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class PillNavItem {
  const PillNavItem({required this.icon, required this.activeIcon, required this.label});
  final IconData icon;
  final IconData activeIcon;
  final String label;
}

class _PillNavButton extends StatelessWidget {
  const _PillNavButton({required this.item, required this.selected, required this.onTap});
  final PillNavItem item;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      selected: selected,
      label: item.label,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(28),
        child: Center(
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 260),
            curve: Curves.easeOutCubic,
            width: selected ? 56 : 44,
            height: 56,
            decoration: BoxDecoration(
              color: selected ? Colors.white : Colors.transparent,
              shape: BoxShape.circle,
            ),
            child: Icon(
              selected ? item.activeIcon : item.icon,
              size: 22,
              color: selected ? Brand.ink : Colors.white.withValues(alpha: 0.75),
            ),
          ),
        ),
      ),
    );
  }
}

/// Lifts a tab page's floating action button clear of the floating nav.
///
/// Each tab is its own Scaffold inside the shell's Scaffold, so it cannot see the nav and
/// would park its button in the default bottom corner - exactly where the pill floats. This
/// takes the default spot and raises it by the nav's footprint.
class AboveNavFabLocation extends FloatingActionButtonLocation {
  const AboveNavFabLocation();

  /// The nav's height, the margin under it, and a gap so the two do not touch.
  static const double clearance = 72 + 16 + 12;

  /// Space to leave at the end of a scrolling list on a page that has a raised button, so the
  /// last item can scroll clear of both the nav and the button above it.
  static const double listEndPadding = clearance + 56 + 16;

  @override
  Offset getOffset(ScaffoldPrelayoutGeometry scaffoldGeometry) {
    final base = FloatingActionButtonLocation.endFloat.getOffset(scaffoldGeometry);
    return Offset(base.dx, base.dy - clearance);
  }
}
