import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/flame_mark.dart';
import '../../../core/widgets/surfaces.dart';
import '../../claims/data/claim_models.dart';
import '../../claims/data/claim_repository.dart';
import '../../feed/data/feed_repository.dart';
import '../../reports/data/report_models.dart';
import '../../reports/data/report_repository.dart';
import '../data/intake_repository.dart';

final _openReportsProvider = FutureProvider.autoDispose<List<LostReportListItemModel>>(
  (ref) async => (await ref.watch(reportRepositoryProvider).getMyReports(status: 'Active', pageSize: 50)).items,
);

class _Turn {
  const _Turn(this.fromMe, this.text);
  final bool fromMe;
  final String text;
}

/// "Ask FoundU": say what you lost, answer what the agent still needs, and it checks what has
/// been found. It suggests - you confirm every report or claim, and the desk still decides.
class AskFoundUPage extends ConsumerStatefulWidget {
  const AskFoundUPage({super.key, this.initialQuestion});

  /// A first sentence typed somewhere else. Sent on arrival rather than left in the box: the
  /// person already pressed a button to get here.
  final String? initialQuestion;

  @override
  ConsumerState<AskFoundUPage> createState() => _AskFoundUPageState();
}

class _AskFoundUPageState extends ConsumerState<AskFoundUPage> {
  final _input = TextEditingController();
  final _scroll = ScrollController();
  final _turns = <_Turn>[
    const _Turn(false, 'What did you lose? Tell me what it is, its colour, or where you last saw it.'),
  ];
  IntakeResponse? _last;
  bool _thinking = false;

  @override
  void initState() {
    super.initState();
    final first = widget.initialQuestion?.trim();
    if (first != null && first.isNotEmpty) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _send(first));
    }
  }

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _send(String text) async {
    final message = text.trim();
    if (message.isEmpty || _thinking) return;

    setState(() {
      _turns.add(_Turn(true, message));
      _thinking = true;
    });
    _input.clear();
    _toBottom();

    try {
      final response = await ref.read(intakeRepositoryProvider).ask(message, _last?.slots);
      if (!mounted) return;
      setState(() {
        _last = response;
        _turns.add(_Turn(false, response.reply));
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      // The person's words stay in the transcript; they can send again or use the form.
      setState(() => _turns.add(_Turn(false, '${error.message} You can try again, or post a report yourself.')));
    } finally {
      if (mounted) setState(() => _thinking = false);
      _toBottom();
    }
  }

  void _toBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scroll.hasClients) {
        _scroll.animateTo(_scroll.position.maxScrollExtent,
            duration: const Duration(milliseconds: 260), curve: Curves.easeOutCubic);
      }
    });
  }

  void _restart() {
    setState(() {
      _turns
        ..clear()
        ..add(const _Turn(false, 'What did you lose? Tell me what it is, its colour, or where you last saw it.'));
      _last = null;
    });
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final last = _last;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Ask FoundU'),
        actions: [
          TextButton(onPressed: _thinking ? null : _restart, child: const Text('Start again')),
        ],
      ),
      body: Column(
        children: [
          Expanded(
            child: ListView(
              controller: _scroll,
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
              children: [
                for (final turn in _turns) _Bubble(turn: turn),
                if (_thinking)
                  Padding(
                    padding: const EdgeInsets.only(left: 44, top: 4),
                    child: Row(
                      children: [
                        const SizedBox.square(dimension: 14, child: CircularProgressIndicator(strokeWidth: 2)),
                        const SizedBox(width: 8),
                        Text('Checking what has been found…', style: text.bodySmall?.copyWith(color: Brand.muted)),
                      ],
                    ),
                  ),
                if (last != null && !_thinking) ...[
                  if (last.match != null) ...[
                    const SizedBox(height: 12),
                    _MatchCard(match: last.match!),
                  ],
                  if (last.phase != 'collecting') ...[
                    const SizedBox(height: 12),
                    _DraftCard(draft: last.draft, hasMatch: last.match != null),
                  ],
                ],
              ],
            ),
          ),
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
              child: TextField(
                controller: _input,
                minLines: 1,
                maxLines: 4,
                maxLength: 1000,
                textInputAction: TextInputAction.send,
                onSubmitted: _send,
                decoration: InputDecoration(
                  counterText: '',
                  hintText: last == null ? 'I lost a black backpack near the library' : 'Add or correct a detail',
                  suffixIcon: IconButton(
                    tooltip: 'Send',
                    onPressed: _thinking ? null : () => _send(_input.text),
                    icon: const Icon(Icons.send_rounded, color: Brand.forest),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Bubble extends StatelessWidget {
  const _Bubble({required this.turn});
  final _Turn turn;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;

    if (turn.fromMe) {
      return Align(
        alignment: Alignment.centerRight,
        child: Container(
          margin: const EdgeInsets.only(left: 48, bottom: 10),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
          decoration: const BoxDecoration(
            color: Brand.forest,
            borderRadius: BorderRadius.only(
              topLeft: Radius.circular(18),
              topRight: Radius.circular(18),
              bottomLeft: Radius.circular(18),
              bottomRight: Radius.circular(6),
            ),
          ),
          child: Text(turn.text, style: text.bodyMedium?.copyWith(color: Colors.white, height: 1.4)),
        ),
      );
    }

    return Padding(
      padding: const EdgeInsets.only(right: 32, bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 34,
            height: 34,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: Brand.mist, borderRadius: BorderRadius.circular(12)),
            child: const FlameMark(size: 28),
          ),
          const SizedBox(width: 10),
          Flexible(
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              decoration: const BoxDecoration(
                color: Brand.surfaceTint,
                borderRadius: BorderRadius.only(
                  topLeft: Radius.circular(6),
                  topRight: Radius.circular(18),
                  bottomLeft: Radius.circular(18),
                  bottomRight: Radius.circular(18),
                ),
              ),
              child: Text(turn.text, style: text.bodyMedium?.copyWith(height: 1.45)),
            ),
          ),
        ],
      ),
    );
  }
}

/// The item the agent pointed at, and the one thing to do about it. A possible match is not
/// proof - the desk still decides - so the card says so.
class _MatchCard extends ConsumerStatefulWidget {
  const _MatchCard({required this.match});
  final IntakeMatch match;

  @override
  ConsumerState<_MatchCard> createState() => _MatchCardState();
}

class _MatchCardState extends ConsumerState<_MatchCard> {
  String? _reportId;
  bool _busy = false;
  bool _done = false;

  Future<void> _confirm() async {
    final reportId = _reportId;
    if (reportId == null) return;
    setState(() => _busy = true);
    try {
      if (widget.match.isAtDesk) {
        final claim = await ref.read(claimRepositoryProvider).createClaim(
              CreateClaimRequest(lostReportId: reportId, foundReportId: widget.match.id),
            );
        if (!mounted) return;
        context.push('/claims/${claim.id}');
      } else {
        await ref.read(feedRepositoryProvider).recogniseFoundPost(widget.match.id, reportId);
        if (!mounted) return;
        setState(() => _done = true);
      }
    } on ApiException catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final match = widget.match;
    final reports = ref.watch(_openReportsProvider);
    final title = [match.colour, match.itemType].where((part) => part != null && part.isNotEmpty).join(' ');

    return Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            match.isAtDesk ? 'Possible match · at a desk' : 'Possible match · awaiting hand-in',
            style: text.labelMedium?.copyWith(color: Brand.forest, fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 6),
          Text(title, style: text.titleLarge),
          const SizedBox(height: 4),
          Row(
            children: [
              const Icon(Icons.place_outlined, size: 16, color: Brand.muted),
              const SizedBox(width: 4),
              Text(match.location, style: text.bodySmall?.copyWith(color: Brand.muted)),
            ],
          ),
          const SizedBox(height: 10),
          Text(match.description, style: text.bodyMedium?.copyWith(height: 1.45)),
          const SizedBox(height: 14),
          const Divider(),
          const SizedBox(height: 10),
          if (_done)
            Text(
              'The finder has been asked to hand it in. You can claim it once it reaches a desk.',
              style: text.bodyMedium?.copyWith(color: Brand.forest, height: 1.45),
            )
          else
            reports.when(
              loading: () => const LinearProgressIndicator(),
              error: (_, __) => Text('Could not load your reports.', style: text.bodySmall?.copyWith(color: Brand.danger)),
              data: (items) => items.isEmpty
                  ? Text(
                      'You have no open report to link it to. Use the draft below to post one first.',
                      style: text.bodySmall?.copyWith(color: Brand.muted, height: 1.4),
                    )
                  : Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text('Recognise it? Pick the report it matches.', style: text.bodySmall?.copyWith(color: Brand.muted)),
                        const SizedBox(height: 8),
                        DropdownButtonFormField<String>(
                          initialValue: _reportId,
                          isExpanded: true,
                          hint: const Text('Choose a report'),
                          items: [
                            for (final report in items)
                              DropdownMenuItem(
                                value: report.id,
                                child: Text(
                                  '${report.itemTypeName} · ${report.description}',
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                          ],
                          onChanged: (value) => setState(() => _reportId = value),
                        ),
                        const SizedBox(height: 12),
                        InkButton(
                          label: match.isAtDesk ? 'This looks like mine · open a claim' : 'This looks like mine · tell the finder',
                          busy: _busy,
                          onPressed: _reportId == null ? null : _confirm,
                        ),
                      ],
                    ),
            ),
          const SizedBox(height: 8),
          Text(
            'A possible match is not proof of ownership. Staff make the final decision.',
            style: text.bodySmall?.copyWith(color: Brand.faint),
          ),
        ],
      ),
    );
  }
}

class _DraftCard extends StatelessWidget {
  const _DraftCard({required this.draft, required this.hasMatch});
  final IntakeDraft draft;
  final bool hasMatch;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Panel(
      color: Brand.mist,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(hasMatch ? 'Need a lost report first?' : 'Your report draft', style: text.titleMedium),
          const SizedBox(height: 6),
          Text(draft.description, style: text.bodyMedium?.copyWith(height: 1.45)),
          const SizedBox(height: 6),
          Text(
            'Nothing has been posted. Check the category, place and time before sharing it on the board.',
            style: text.bodySmall?.copyWith(color: Brand.muted, height: 1.4),
          ),
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: () => context.push('/reports/new', extra: draft),
            icon: const Icon(Icons.edit_note_rounded),
            label: const Text('Review report draft'),
          ),
        ],
      ),
    );
  }
}
