import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';
import '../../feed/data/feed_models.dart';
import '../../feed/presentation/feed_controller.dart';
import '../../feed/presentation/message_thread.dart';
import '../data/handover_repository.dart';

/// What a finder does after saying they have somebody's item.
///
/// Two ways forward, and not the same weight: writing to the owner costs nothing and changes
/// nothing, while taking it to a desk mints the code both of them will quote and takes the
/// notice off the feed. So the code does not exist until they commit to the walk.
class HandoverChoice extends ConsumerStatefulWidget {
  const HandoverChoice(
      {super.key, required this.reportId, required this.firstName});

  final String reportId;
  final String firstName;

  @override
  ConsumerState<HandoverChoice> createState() => _HandoverChoiceState();
}

class _HandoverChoiceState extends ConsumerState<HandoverChoice> {
  bool _busy = false;

  Future<void> _run(
      Future<Handover> Function(HandoverRepository) action, String done) async {
    setState(() => _busy = true);
    try {
      await action(ref.read(handoverRepositoryProvider));
      ref.invalidate(handoverProvider(widget.reportId));
      // The notice leaves (or returns to) the feed with this.
      ref.invalidate(feedControllerProvider);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(done)));
    } on ApiException catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final handover = ref.watch(handoverProvider(widget.reportId));
    final name = widget.firstName;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        handover.when(
          loading: () => const LinearProgressIndicator(),
          error: (_, __) => Text(
            'Could not check whether this is on its way already. Pull down to try again.',
            style: text.bodySmall?.copyWith(color: Brand.danger),
          ),
          data: (current) =>
              current != null && current.isLive && current.code != null
                  ? _CodeCard(
                      handover: current,
                      firstName: name,
                      busy: _busy,
                      onCancel: () => _run((r) => r.cancel(widget.reportId),
                          'Called off. The notice is back on the feed.'),
                    )
                  : Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text(
                            'Thank you. How do you want to get it back to $name?',
                            style: text.titleMedium),
                        const SizedBox(height: 12),
                        InkButton(
                          label: 'Hand it to security',
                          icon: Icons.verified_user_outlined,
                          busy: _busy,
                          onPressed: () => _run((r) => r.start(widget.reportId),
                              'Code ready. Take it to any campus desk.'),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'Generates a code for you and $name. The notice pauses while you walk it '
                          'over, and comes back by itself if you do not.',
                          style: text.bodySmall
                              ?.copyWith(color: Brand.muted, height: 1.4),
                        ),
                      ],
                    ),
        ),
        const SizedBox(height: 18),
        const Divider(),
        const SizedBox(height: 14),
        Row(
          children: [
            const Icon(Icons.chat_bubble_outline_rounded,
                size: 18, color: Brand.muted),
            const SizedBox(width: 8),
            Text('Or write to $name', style: text.titleSmall),
          ],
        ),
        const SizedBox(height: 8),
        MessageThread(reportId: widget.reportId, isAuthor: false),
      ],
    );
  }
}

class _CodeCard extends StatelessWidget {
  const _CodeCard(
      {required this.handover,
      required this.firstName,
      required this.busy,
      required this.onCancel});

  final Handover handover;
  final String firstName;
  final bool busy;
  final VoidCallback onCancel;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final atDesk = handover.isAtDesk;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        CodeBanner(
          label:
              atDesk ? 'At the desk under this code' : 'Quote this at the desk',
          code: handover.code!,
        ),
        const SizedBox(height: 12),
        Text(
          atDesk
              ? 'The desk has it${handover.storageLocationName == null ? '' : ' at ${handover.storageLocationName}'}. '
                  '$firstName has the same code and collects it with their student ID. Nothing more for you to do - thank you.'
              : '$firstName has this code too. Take the item to any campus desk and quote it; they collect it '
                  'there with their student ID. The notice is off the feed while you do.',
          style: text.bodyMedium?.copyWith(color: Brand.muted, height: 1.45),
        ),
        if (!atDesk) ...[
          const SizedBox(height: 4),
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton(
              onPressed: busy ? null : onCancel,
              child: const Text('I cannot take it after all'),
            ),
          ),
        ],
      ],
    );
  }
}

/// Six digits, grouped for reading aloud at a desk. Shared by the finder and the owner.
class CodeBanner extends StatelessWidget {
  const CodeBanner({super.key, required this.label, required this.code});

  final String label;
  final String code;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
          color: Brand.forest,
          borderRadius: BorderRadius.circular(Brand.radiusControl)),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label,
                    style:
                        const TextStyle(color: Colors.white70, fontSize: 12)),
                const SizedBox(height: 2),
                Text(
                  displayCode(code),
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 26,
                    fontWeight: FontWeight.w600,
                    letterSpacing: 4,
                    fontFeatures: [FontFeature.tabularFigures()],
                  ),
                ),
              ],
            ),
          ),
          const Icon(Icons.tag_rounded, color: Colors.white60),
        ],
      ),
    );
  }
}
