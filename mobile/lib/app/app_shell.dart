import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../core/widgets/pill_nav.dart';

/// The signed-in frame: the current tab's page with the floating pill nav over it. Each tab
/// keeps its own navigation stack, so coming back to the feed lands where you left it.
class AppShell extends StatelessWidget {
  const AppShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  static const _items = [
    PillNavItem(icon: Icons.home_outlined, activeIcon: Icons.home_rounded, label: 'Feed'),
    PillNavItem(icon: Icons.assignment_outlined, activeIcon: Icons.assignment_rounded, label: 'My reports'),
    PillNavItem(icon: Icons.verified_user_outlined, activeIcon: Icons.verified_user_rounded, label: 'My claims'),
    PillNavItem(icon: Icons.person_outline_rounded, activeIcon: Icons.person_rounded, label: 'Profile'),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      extendBody: true,
      body: navigationShell,
      bottomNavigationBar: PillNav(
        items: _items,
        selectedIndex: navigationShell.currentIndex,
        onSelect: (index) => navigationShell.goBranch(
          index,
          // Tapping the tab you are on scrolls it back to the top of its stack.
          initialLocation: index == navigationShell.currentIndex,
        ),
      ),
    );
  }
}
