import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/item_illustration.dart';
import '../../../core/widgets/surfaces.dart';
import '../../reports/data/report_models.dart';
import '../../reports/data/report_repository.dart';
import '../data/feed_models.dart';
import '../data/feed_repository.dart';
import 'feed_controller.dart';
import 'found_feed_controller.dart';

final _myOpenReportsProvider = FutureProvider.autoDispose<List<LostReportListItemModel>>(
  (ref) async => (await ref.watch(reportRepositoryProvider).getMyReports(status: 'Active', pageSize: 50)).items,
);

/// A finder's post, opened. Two things a person can do here: say it is theirs, or take
/// down their own post. Nobody can claim from here - that waits for a desk.
Future<void> showFoundPostDetail(BuildContext context, FoundPost post) {
  return showModalBottomSheet<void>(
    context: context,
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
  String? _reportId;
  bool _busy = false;
  bool _done = false;

  Future<void> _recognise() async {
    final reportId = _reportId;
    if (reportId == null) return;
    setState(() => _busy = true);
    try {
      await ref.read(feedRepositoryProvider).recogniseFoundPost(widget.post.id, reportId);
      if (!mounted) return;
      setState(() => _done = true);
    } on ApiException catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

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
    final post = widget.post;
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
        const StatusChip('Not at a desk yet', tone: ChipTone.action),
        const SizedBox(height: 16),
        Text(post.description, style: text.bodyLarge?.copyWith(height: 1.5)),
        const SizedBox(height: 18),
        Panel(
          padding: const EdgeInsets.all(16),
          child: Column(
            children: [
              _Fact(icon: Icons.place_outlined, label: 'Found at', value: post.foundLocationName),
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
        else if (_done)
          Panel(
            color: Brand.mist,
            child: Text(
              'Done. ${post.postedByName.split(' ').first} has been asked to hand it in, and it now shows on your report. '
              'You will be able to claim it once it reaches a desk.',
              style: text.bodyMedium?.copyWith(color: Brand.forest, height: 1.45),
            ),
          )
        else
          _RecognisePanel(
            reportId: _reportId,
            onChanged: (id) => setState(() => _reportId = id),
            busy: _busy,
            onConfirm: _recognise,
          ),
      ],
    );
  }
}

class _RecognisePanel extends ConsumerWidget {
  const _RecognisePanel({required this.reportId, required this.onChanged, required this.busy, required this.onConfirm});
  final String? reportId;
  final ValueChanged<String?> onChanged;
  final bool busy;
  final VoidCallback onConfirm;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final text = Theme.of(context).textTheme;
    final reports = ref.watch(_myOpenReportsProvider);

    return Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Is this yours?', style: text.titleMedium),
          const SizedBox(height: 4),
          Text('Pick the report it matches. The finder is asked to hand it in; the desk will still check it is yours.',
              style: text.bodySmall?.copyWith(color: Brand.muted, height: 1.4)),
          const SizedBox(height: 14),
          reports.when(
            loading: () => const LinearProgressIndicator(),
            error: (_, __) => Text('Could not load your reports.', style: text.bodySmall?.copyWith(color: Brand.danger)),
            data: (items) => items.isEmpty
                ? Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text('You have no open report to match it to.', style: text.bodySmall?.copyWith(color: Brand.muted)),
                      const SizedBox(height: 8),
                      OutlinedButton(onPressed: () { Navigator.of(context).pop(); context.push('/reports/new'); }, child: const Text('Post one first')),
                    ],
                  )
                : DropdownButtonFormField<String>(
                    initialValue: reportId,
                    decoration: const InputDecoration(labelText: 'Your report'),
                    items: [for (final r in items) DropdownMenuItem(value: r.id, child: Text('${r.itemTypeName} · ${r.lastSeenLocationName}', overflow: TextOverflow.ellipsis))],
                    onChanged: onChanged,
                  ),
          ),
          const SizedBox(height: 14),
          InkButton(label: 'That is mine', busy: busy, onPressed: reportId == null ? null : onConfirm),
        ],
      ),
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
