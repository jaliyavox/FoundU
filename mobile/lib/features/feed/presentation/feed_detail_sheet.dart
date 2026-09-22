import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';
import '../data/feed_models.dart';
import '../data/feed_repository.dart';
import 'feed_card.dart';
import 'feed_controller.dart';
import 'message_thread.dart';

/// Opens a post from the feed. A bottom sheet rather than a page: the feed stays where it
/// was underneath, so the reading position survives.
Future<void> showFeedDetail(BuildContext context, FeedItem item) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    backgroundColor: Brand.paper,
    builder: (_) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.88,
      minChildSize: 0.5,
      maxChildSize: 0.96,
      builder: (context, controller) => _FeedDetail(item: item, scrollController: controller),
    ),
  );
}

enum _Stage { reading, confirming, handingIn }

class _FeedDetail extends ConsumerStatefulWidget {
  const _FeedDetail({required this.item, required this.scrollController});
  final FeedItem item;
  final ScrollController scrollController;

  @override
  ConsumerState<_FeedDetail> createState() => _FeedDetailState();
}

class _FeedDetailState extends ConsumerState<_FeedDetail> {
  _Stage _stage = _Stage.reading;
  bool _busy = false;

  Future<void> _confirmFound() async {
    setState(() => _busy = true);
    try {
      // Recorded before the steps appear: the author's card should update the moment a
      // finder commits, not only if they go on to write a message.
      await ref.read(feedRepositoryProvider).registerFoundClaim(widget.item.id);
      if (!mounted) return;
      setState(() => _stage = _Stage.handingIn);
    } on ApiException catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final item = widget.item;
    final text = Theme.of(context).textTheme;
    final firstName = item.postedByName.split(' ').first;

    return ListView(
      controller: widget.scrollController,
      padding: const EdgeInsets.fromLTRB(20, 0, 20, 32),
      children: [
        ClipRRect(
          borderRadius: BorderRadius.circular(Brand.radiusCard),
          child: AspectRatio(aspectRatio: 16 / 10, child: ItemMedia(item: item, illustrationSize: 110)),
        ),
        const SizedBox(height: 20),
        Text(item.itemTypeName, style: text.headlineSmall),
        const SizedBox(height: 4),
        Text(
          'Reported by ${item.isMine ? 'you' : item.postedByName} · ${timeAgo(item.createdAt)}',
          style: text.bodyMedium?.copyWith(color: Brand.muted),
        ),
        const SizedBox(height: 14),
        Wrap(
          spacing: 8,
          children: [
            StatusChip(item.categoryName),
            if (item.primaryColor != null) StatusChip(item.primaryColor!),
          ],
        ),
        const SizedBox(height: 16),
        Text(item.description, style: text.bodyLarge?.copyWith(height: 1.5)),
        const SizedBox(height: 18),
        Panel(
          padding: const EdgeInsets.all(16),
          child: Column(
            children: [
              _Fact(icon: Icons.place_outlined, label: 'Last seen', value: item.lastSeenLocationName),
              const SizedBox(height: 12),
              _Fact(
                icon: Icons.schedule_outlined,
                label: 'Lost between',
                value: _window(item.estimatedLostFromAt, item.estimatedLostToAt),
              ),
            ],
          ),
        ),
        const SizedBox(height: 22),
        AnimatedSwitcher(
          duration: const Duration(milliseconds: 260),
          switchInCurve: Curves.easeOutCubic,
          child: item.isMine
              ? const _OwnPost(key: ValueKey('own'))
              : switch (_stage) {
                  _Stage.reading => InkButton(
                      key: const ValueKey('found'),
                      label: 'I found this',
                      icon: Icons.volunteer_activism_outlined,
                      onPressed: () => setState(() => _stage = _Stage.confirming),
                    ),
                  _Stage.confirming => _Confirm(
                      key: const ValueKey('confirm'),
                      item: item,
                      firstName: firstName,
                      busy: _busy,
                      onYes: _confirmFound,
                      onNo: () => setState(() => _stage = _Stage.reading),
                    ),
                  _Stage.handingIn => _HandIn(
                      key: const ValueKey('handin'),
                      firstName: firstName,
                      handInCode: item.handInCode,
                      reportId: item.id,
                    ),
                },
        ),
      ],
    );
  }

  static String _window(DateTime from, DateTime to) {
    final day = DateFormat('EEE, d MMM').format(from.toLocal());
    final t = DateFormat('h:mm a');
    return '$day, ${t.format(from.toLocal())} - ${t.format(to.toLocal())}';
  }
}

class _Fact extends StatelessWidget {
  const _Fact({required this.icon, required this.label, required this.value});
  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 18, color: Brand.green),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label, style: text.bodySmall?.copyWith(color: Brand.muted)),
              Text(value, style: text.bodyMedium),
            ],
          ),
        ),
      ],
    );
  }
}

class _OwnPost extends StatelessWidget {
  const _OwnPost({super.key});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        InkButton(
          label: 'View status',
          icon: Icons.timeline_outlined,
          onPressed: () {
            Navigator.of(context).pop();
            context.push('/reports');
          },
        ),
        const SizedBox(height: 10),
        Text(
          'This is your report. Its progress, and anyone who says they found it, show on your reports.',
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(color: Brand.muted),
        ),
      ],
    );
  }
}

/// The check before "I found this" is recorded. Pressing it tells a real person their lost
/// thing has turned up, so a mis-tap costs them a false hope - this restates the details
/// and asks the finder to match them against what is in their hand.
class _Confirm extends StatelessWidget {
  const _Confirm({super.key, required this.item, required this.firstName, required this.busy, required this.onYes, required this.onNo});
  final FeedItem item;
  final String firstName;
  final bool busy;
  final VoidCallback onYes;
  final VoidCallback onNo;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Panel(
      color: Brand.mist,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Are you sure you found this?', style: text.titleMedium),
          const SizedBox(height: 6),
          Text(
            '$firstName is told straight away that their ${item.itemTypeName.toLowerCase()} has turned up. '
            'Check it against the details above first - if it only looks similar, stop here.',
            style: text.bodyMedium?.copyWith(color: Brand.forest, height: 1.45),
          ),
          const SizedBox(height: 16),
          InkButton(label: 'Yes, continue', busy: busy, onPressed: onYes),
          const SizedBox(height: 8),
          TextButton(onPressed: busy ? null : onNo, child: const Text('Not sure yet')),
        ],
      ),
    );
  }
}

/// The three steps, then the optional message. The item goes through a desk - never
/// directly between two strangers - because the desk holds the detail that proves ownership.
class _HandIn extends StatelessWidget {
  const _HandIn({super.key, required this.firstName, required this.handInCode, required this.reportId});
  final String firstName;
  final String handInCode;
  final String reportId;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final steps = [
      (Icons.pan_tool_alt_outlined, 'Hand it in', 'Take it to any security or library desk.'),
      (Icons.inventory_2_outlined, 'Staff log it', 'They record it and keep it safe.'),
      (Icons.verified_outlined, 'Owner proves it', '$firstName describes a detail only they would know.'),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('Thank you. Three steps from here.', style: text.titleMedium),
        const SizedBox(height: 12),
        // The code is what makes the desk step quick: staff type it and the item is linked
        // to this post on the spot.
        if (handInCode.isNotEmpty) ...[
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            decoration: BoxDecoration(color: Brand.forest, borderRadius: BorderRadius.circular(Brand.radiusControl)),
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Quote this code at the desk', style: TextStyle(color: Colors.white70, fontSize: 12)),
                      Text(
                        displayCode(handInCode),
                        style: const TextStyle(color: Colors.white, fontSize: 24, fontWeight: FontWeight.w600, letterSpacing: 4),
                      ),
                    ],
                  ),
                ),
                const Icon(Icons.tag_rounded, color: Colors.white60),
              ],
            ),
          ),
          const SizedBox(height: 12),
        ],
        for (final (i, step) in steps.indexed)
          TweenAnimationBuilder<double>(
            tween: Tween(begin: 0, end: 1),
            duration: Duration(milliseconds: 320 + i * 120),
            curve: Curves.easeOutCubic,
            builder: (context, t, child) => Opacity(
              opacity: t,
              child: Transform.translate(offset: Offset(0, (1 - t) * 10), child: child),
            ),
            child: Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: Row(
                children: [
                  Container(
                    width: 40,
                    height: 40,
                    decoration: const BoxDecoration(color: Brand.mist, shape: BoxShape.circle),
                    child: Icon(step.$1, size: 20, color: Brand.forest),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(step.$2, style: text.bodyMedium?.copyWith(fontWeight: FontWeight.w600)),
                        Text(step.$3, style: text.bodySmall?.copyWith(color: Brand.muted)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        const SizedBox(height: 8),
        const Divider(),
        const SizedBox(height: 14),
        Text('Tell $firstName where it went', style: text.titleSmall),
        const SizedBox(height: 8),
        MessageThread(reportId: reportId, isAuthor: false),
      ],
    );
  }
}
