import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/brand.dart';
import '../data/handover_repository.dart';
import 'handover_choice.dart';

/// The owner's half of a handover: somebody is bringing their item in, and this is the code
/// they quote to collect it.
///
/// On the report itself rather than only in a notification, because a code you need at a
/// desk has to be somewhere you can find it again.
class HandoverNotice extends ConsumerWidget {
  const HandoverNotice({super.key, required this.reportId, this.bottomGap = 0});

  final String reportId;

  /// Space below the notice - only when there is a notice, so an empty one takes no room.
  final double bottomGap;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final handover = ref.watch(handoverProvider(reportId)).value;
    if (handover == null || !handover.isLive || handover.code == null) {
      return const SizedBox.shrink();
    }

    final text = Theme.of(context).textTheme;
    final atDesk = handover.isAtDesk;
    final finder = handover.finderName?.split(' ').first ?? 'Someone';

    return Padding(
      padding: EdgeInsets.only(bottom: bottomGap),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: Brand.mist,
          borderRadius: BorderRadius.circular(Brand.radiusControl),
          border: Border.all(color: Brand.green.withValues(alpha: .35)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(
                  atDesk
                      ? Icons.inventory_2_outlined
                      : Icons.local_shipping_outlined,
                  size: 18,
                  color: Brand.forest,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text.rich(
                    TextSpan(
                      children: [
                        TextSpan(
                          text: atDesk
                              ? 'Ready to collect${handover.storageLocationName == null ? '' : ' at ${handover.storageLocationName}'}'
                              : '$finder is taking it to a desk',
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                        TextSpan(
                          text: atDesk
                              ? ' - bring your student ID and quote this code.'
                              : ' - your notice is paused until they do. Quote this code when you collect it.',
                          style: const TextStyle(color: Brand.muted),
                        ),
                      ],
                    ),
                    style: text.bodyMedium?.copyWith(height: 1.4),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            CodeBanner(label: 'Your collection code', code: handover.code!),
            const SizedBox(height: 10),
            Text(
              'Only you and the finder can see this code. The desk checks your student ID against the '
              'name on this report before handing anything over.',
              style: text.bodySmall?.copyWith(color: Brand.muted, height: 1.4),
            ),
          ],
        ),
      ),
    );
  }
}
