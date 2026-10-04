import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_session.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/item_illustration.dart';
import '../../../core/widgets/surfaces.dart';
import '../data/feed_models.dart';
import '../data/feed_repository.dart';
import 'feed_controller.dart';
import 'found_feed_controller.dart';
import 'message_thread.dart';
import '../../claims/data/claim_models.dart';
import '../../claims/presentation/providers/claim_providers.dart';
import '../../reports/data/report_models.dart';
import '../../reports/data/report_repository.dart';
import '../../reports/presentation/providers/report_providers.dart';

/// A found item, with exact matched-report claim actions once it reaches security.
Future<void> showFoundPostDetail(BuildContext context, FoundPost post) {
  return showModalBottomSheet<void>(
    context: context,
    // On the root navigator, so the sheet covers the floating nav. Opened from a tab it
    // would otherwise live inside that tab, underneath the nav, hiding its bottom.
    useRootNavigator: true,
    isScrollControlled: true,
    useSafeArea: true,
    backgroundColor: Brand.paper,
    builder: (_) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.86,
      minChildSize: 0.5,
      maxChildSize: 0.96,
      builder: (context, controller) => _FoundPostDetail(post: post, scrollController: controller),
    ),
  );
}

class _FoundPostDetail extends ConsumerStatefulWidget {
  const _FoundPostDetail({required this.post, required this.scrollController});
  final FoundPost post;
  final ScrollController scrollController;

  @override
  ConsumerState<_FoundPostDetail> createState() => _FoundPostDetailState();
}

class _FoundPostDetailState extends ConsumerState<_FoundPostDetail> {
  bool _busy = false;

  Future<void> _withdraw() async {
    setState(() => _busy = true);
    try {
      await ref.read(feedRepositoryProvider).withdrawFoundPost(widget.post.id);
      if (!mounted) return;
      ref.read(foundFeedControllerProvider.notifier).refresh();
      Navigator.of(context).pop();
    } on ApiException catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final latest = ref.watch(foundFeedControllerProvider).items.where((p) => p.id == widget.post.id);
    final post = latest.isEmpty ? widget.post : latest.first;
    final text = Theme.of(context).textTheme;
    final user = ref.watch(authControllerProvider).value;
    final canRecognise = user?.role == 'Student' && !post.isMine;

    return ListView(
      controller: widget.scrollController,
      padding: const EdgeInsets.fromLTRB(20, 0, 20, 32),
      children: [
        ClipRRect(
          borderRadius: BorderRadius.circular(Brand.radiusCard),
          child: AspectRatio(
            aspectRatio: 16 / 9,
            child: DecoratedBox(
              decoration: const BoxDecoration(
                gradient: LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [Color(0xFF1B2A1D), Brand.ink]),
              ),
              child: Center(child: ItemIllustration(itemType: post.itemTypeName, category: post.categoryName, size: 100)),
            ),
          ),
        ),
        const SizedBox(height: 20),
        Text(post.itemTypeName, style: text.headlineSmall),
        const SizedBox(height: 4),
        Text('Found by ${post.isMine ? 'you' : post.postedByName} · ${timeAgo(post.createdAt)}',
            style: text.bodyMedium?.copyWith(color: Brand.muted)),
        const SizedBox(height: 14),
        StatusChip(post.stageLabel, tone: post.isAtDesk ? ChipTone.good : ChipTone.action),
        const SizedBox(height: 10),
        Text(post.stageNote, style: text.bodySmall?.copyWith(color: Brand.muted, height: 1.4)),
        const SizedBox(height: 16),
        Text(post.description, style: text.bodyLarge?.copyWith(height: 1.5)),
        const SizedBox(height: 18),
        Panel(
          padding: const EdgeInsets.all(16),
          child: Column(
            children: [
              _Fact(icon: Icons.place_outlined, label: 'Found at', value: post.foundLocationName),
              if (post.storageLocationName != null) ...[
                const SizedBox(height: 12),
                _Fact(icon: Icons.security, label: 'Security desk', value: post.storageLocationName!),
              ],
              const SizedBox(height: 12),
              _Fact(icon: Icons.schedule_outlined, label: 'When', value: DateFormat('EEE, d MMM, h:mm a').format(post.foundAt.toLocal())),
            ],
          ),
        ),
        const SizedBox(height: 22),
        if (post.isMine) ...[
          if (post.handInCode != null) ...[
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(color: Brand.forest, borderRadius: BorderRadius.circular(Brand.radiusControl)),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Quote this when you hand it in', style: TextStyle(color: Colors.white70, fontSize: 12)),
                  Text(displayCode(post.handInCode!),
                      style: const TextStyle(color: Colors.white, fontSize: 28, fontWeight: FontWeight.w600, letterSpacing: 5)),
                ],
              ),
            ),
            const SizedBox(height: 12),
          ],
          Text('People asking about this', style: text.titleMedium),
          const SizedBox(height: 10),
          if (post.canMessageFinder && post.status == 'Posted') MessageThread(reportId: post.id, isAuthor: true, source: MessageSource.foundPost),
          const SizedBox(height: 18),
          // Once a desk has it, it is the desk's to deal with - not the finder's to take down.
          if (post.status == 'Posted')
            OutlinedButton.icon(
              onPressed: _busy ? null : _withdraw,
              icon: const Icon(Icons.delete_outline_rounded, size: 18),
              label: const Text('Take this post down'),
            ),
        ] else if (user == null)
          InkButton(label: 'Sign in if this is yours', onPressed: () => context.go('/login'))
        else if (!canRecognise)
          Text("Staff can pull this post up at the desk by the finder's code.",
              style: text.bodyMedium?.copyWith(color: Brand.muted))
        else ...[
          // Asking comes first and needs no report of your own: a question is not a claim,
          // and a detail only the owner would know settles it faster than a form.
          Text(post.isAtDesk ? 'This item is now held at ${post.storageLocationName ?? 'the security desk'}.' : 'Think it is yours?', style: text.titleMedium),
          const SizedBox(height: 4),
          if (post.canMessageFinder && post.status == 'Posted') Text(
            'Ask ${post.postedByName.split(' ').first} about it. Nothing is claimed by asking.',
            style: text.bodySmall?.copyWith(color: Brand.muted, height: 1.4),
          ),
          const SizedBox(height: 12),
          if (post.canMessageFinder && post.status == 'Posted')
            MessageThread(reportId: post.id, isAuthor: false, source: MessageSource.foundPost),
          const SizedBox(height: 18),
          KeyedSubtree(key: const ValueKey('found-item-match-actions'),
            child: _FoundItemMatchActions(key: ValueKey('${user.id}:${ref.watch(authSessionEpochProvider)}'), post: post)),
        ],
      ],
    );
  }
}

const foundMatchConfirmationThreshold = 0.75;

class _FoundItemMatchActions extends ConsumerStatefulWidget {
  const _FoundItemMatchActions({super.key, required this.post});
  final FoundPost post;

  @override
  ConsumerState<_FoundItemMatchActions> createState() => _FoundItemMatchActionsState();
}

class _FoundItemMatchActionsState extends ConsumerState<_FoundItemMatchActions> {
  bool _busy = false;
  String? _claimId;
  String? _error;
  final _dismissed = <String>{};

  Future<void> _answer(MatchSuggestionModel match, bool yes) async {
    if (_busy || _claimId != null) return;
    final epoch = ref.read(authSessionEpochProvider);
    setState(() { _busy = true; _error = null; });
    try {
      if (yes) {
        final claim = await ref.read(claimControllerProvider.notifier).create(
          CreateClaimRequest(lostReportId: match.lostReportId,
            foundReportId: widget.post.id, matchSuggestionId: match.id));
        if (!mounted || ref.read(authSessionEpochProvider) != epoch) return;
        setState(() => _claimId = claim.id);
      } else {
        await ref.read(reportRepositoryProvider).dismissMatch(match.id);
        if (!mounted || ref.read(authSessionEpochProvider) != epoch) return;
        setState(() => _dismissed.add(match.id));
        ref.invalidate(foundItemMatchesProvider(widget.post.id));
        ref.invalidate(possibleMatchesProvider(match.lostReportId));
        ref.invalidate(myReportsProvider);
        ref.invalidate(reportDetailProvider(match.lostReportId));
      }
    } catch (error) {
      if (!mounted || ref.read(authSessionEpochProvider) != epoch) return;
      setState(() => _error = error is ApiException ? error.message : 'Unable to save your response. Please try again.');
    } finally {
      if (mounted && ref.read(authSessionEpochProvider) == epoch) setState(() => _busy = false);
    }
  }

  Widget _viewClaim(String id) => InkButton(label: 'View claim', onPressed: () {
    final router = GoRouter.of(context);
    Navigator.of(context).pop();
    router.push('/claims/$id');
  });

  @override
  Widget build(BuildContext context) {
    final matches = ref.watch(foundItemMatchesProvider(widget.post.id));
    if (_claimId != null) { return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      const Text('Claim submitted — waiting for verification questions.'),
      const SizedBox(height: 12), _viewClaim(_claimId!),
    ]); }
    return matches.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (_, stack) => Column(children: [
        const Text('Unable to check your matches.'),
        TextButton(onPressed: () => ref.invalidate(foundItemMatchesProvider(widget.post.id)), child: const Text('Retry')),
      ]),
      data: (items) {
        final own = items.where((m) => m.foundItem.id == widget.post.id && !_dismissed.contains(m.id) && m.status != 'Dismissed').toList();
        final claimed = own.where((m) => m.claimId != null);
        if (claimed.isNotEmpty) { return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('You have already submitted a claim for this item.'),
          const SizedBox(height: 12), _viewClaim(claimed.first.claimId!),
        ]); }
        final eligible = own.where((m) => m.status == 'Suggested' &&
          (!m.isAgentGenerated || (m.matchScore ?? 0) >= foundMatchConfirmationThreshold)).toList();
        if (eligible.isEmpty) { return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(_dismissed.isEmpty ? 'There is no eligible match to your lost reports for this item.' : 'This match was dismissed. Your lost report remains active.'),
          const SizedBox(height: 12),
          InkButton(label: 'View my lost reports', onPressed: () {
            final router = GoRouter.of(context);
            Navigator.of(context).pop(); router.push('/reports');
          }),
        ]); }
        return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          if (_error != null) Text(_error!, style: const TextStyle(color: Colors.red)),
          if (!widget.post.isAtDesk) const Text('You can submit a claim after this item is handed to security.'),
          for (final match in eligible) ...[
            if (eligible.length > 1) ...[
              const SizedBox(height: 12),
              Text('Your lost report: ${match.lostReportDescription}'),
            ],
            const SizedBox(height: 12),
            Text('Is this your item?', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            FilledButton(onPressed: _busy || !widget.post.isAtDesk ? null : () => _answer(match, true),
              child: const Text('Yes, this is mine')),
            OutlinedButton(onPressed: _busy ? null : () => _answer(match, false),
              child: const Text('No, this is not mine')),
          ],
        ]);
      },
    );
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
            children: [Text(label, style: text.bodySmall?.copyWith(color: Brand.muted)), Text(value, style: text.bodyMedium)],
          ),
        ),
      ],
    );
  }
}
