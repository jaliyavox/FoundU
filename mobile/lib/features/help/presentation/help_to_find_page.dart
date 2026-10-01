import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';
import '../../feed/data/feed_models.dart';
import '../data/help_models.dart';
import '../data/help_repository.dart';

/// Everything one person has done for someone else, and what they earned for it.
///
/// It is also where a finder comes back for the six digits they need at the desk, so the
/// code is on the row until the item reaches one.
class HelpToFindPage extends ConsumerWidget {
  const HelpToFindPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final help = ref.watch(helpToFindProvider);
    final text = Theme.of(context).textTheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Help to find')),
      body: RefreshIndicator(
        color: Brand.forest,
        onRefresh: () => ref.refresh(helpToFindProvider.future),
        child: help.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ListView(
            padding: const EdgeInsets.all(20),
            children: [
              Panel(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Could not load your finding activity', style: text.titleMedium),
                    const SizedBox(height: 4),
                    Text(
                      // Only messages the API meant for people. Anything else - a transport
                      // failure, a parse error - says nothing about how the app is built.
                      error is ApiException ? error.message : 'Check your connection and try again.',
                      style: text.bodyMedium?.copyWith(color: Brand.muted),
                    ),
                    const SizedBox(height: 12),
                    InkButton(
                      label: 'Try again',
                      onPressed: () => ref.invalidate(helpToFindProvider),
                    ),
                  ],
                ),
              ),
            ],
          ),
          data: (data) => ListView(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 40),
            children: [
              Text('What you have done for other people', style: text.headlineSmall),
              const SizedBox(height: 6),
              Text(
                'Points arrive when an item actually gets home - not for pressing a button.',
                style: text.bodyMedium?.copyWith(color: Brand.muted),
              ),
              const SizedBox(height: 16),
              Panel(
                color: Brand.ink,
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('${data.honorPoints}',
                              style: text.displaySmall?.copyWith(color: Colors.white, fontWeight: FontWeight.w600)),
                          Text('honor points', style: text.bodySmall?.copyWith(color: Colors.white70)),
                        ],
                      ),
                    ),
                    _Stat(value: data.itemsReturned, label: 'got home'),
                    const SizedBox(width: 18),
                    _Stat(value: data.handIns, label: 'hand-ins'),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              if (data.activity.isEmpty)
                Panel(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Nothing yet', style: text.titleMedium),
                      const SizedBox(height: 4),
                      Text(
                        'When you press "I found this" on someone\'s report, or post something '
                        'you picked up, it shows here with the code to quote at the desk.',
                        style: text.bodyMedium?.copyWith(color: Brand.muted),
                      ),
                    ],
                  ),
                )
              else
                ...data.activity.map((entry) => Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: _ActivityCard(entry: entry),
                    )),
            ],
          ),
        ),
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.value, required this.label});

  final int value;
  final String label;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text('$value', style: text.titleLarge?.copyWith(color: Colors.white)),
        Text(label, style: text.bodySmall?.copyWith(color: Colors.white54)),
      ],
    );
  }
}

class _ActivityCard extends StatelessWidget {
  const _ActivityCard({required this.entry});

  final HelpActivity entry;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;

    return Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(entry.itemTypeName, style: text.titleMedium)),
              if (entry.pointsEarned > 0)
                Text('+${entry.pointsEarned}',
                    style: text.titleSmall?.copyWith(color: Brand.forest, fontWeight: FontWeight.w700)),
            ],
          ),
          const SizedBox(height: 2),
          Text(
            entry.isPost ? 'You posted this' : "${entry.ownerName ?? 'Someone'}'s report",
            style: text.bodySmall?.copyWith(color: Brand.muted),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              const Icon(Icons.place_outlined, size: 16, color: Brand.faint),
              const SizedBox(width: 4),
              Expanded(child: Text(entry.locationName, style: text.bodySmall?.copyWith(color: Brand.muted))),
              StatusChip(entry.status, tone: entry.status == 'Resolved' || entry.status == 'Returned' ? ChipTone.good : ChipTone.neutral),
            ],
          ),
          if (entry.handInCode != null) ...[
            const SizedBox(height: 12),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(color: Brand.forest, borderRadius: BorderRadius.circular(Brand.radiusControl)),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Quote this code at the desk', style: text.bodySmall?.copyWith(color: Colors.white70)),
                  const SizedBox(height: 2),
                  Text(
                    displayCode(entry.handInCode!),
                    style: text.headlineSmall?.copyWith(
                      color: Colors.white,
                      fontFeatures: const [FontFeature.tabularFigures()],
                      letterSpacing: 4,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}
