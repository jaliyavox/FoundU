import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';
import '../data/support_repository.dart';

const _greeting = 'Hi - what is going wrong? Tell me in a sentence or two, and I will either sort it or pass it to the desk.';

/// Ask before opening a ticket. Answers come from FoundU's help guide, about the person's own
/// claims and reports where that helps. When it cannot help it drafts a ticket - which the
/// person reads, changes if they like, and sends. Nothing is sent for them.
class SupportAssistantPage extends ConsumerStatefulWidget {
  const SupportAssistantPage({super.key});

  @override
  ConsumerState<SupportAssistantPage> createState() => _SupportAssistantPageState();
}

class _SupportAssistantPageState extends ConsumerState<SupportAssistantPage> {
  final _input = TextEditingController();
  final _scroll = ScrollController();
  final _turns = <AssistantTurn>[const AssistantTurn(fromMe: false, text: _greeting)];
  AssistantResponse? _last;
  bool _thinking = false;
  bool _solved = false;

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _send(String text) async {
    final message = text.trim();
    if (message.isEmpty || _thinking) return;

    // What was said before this message; the greeting is the page's, not the agent's.
    final history = _turns.skip(1).toList();
    setState(() {
      _turns.add(AssistantTurn(fromMe: true, text: message));
      _thinking = true;
      _solved = false;
    });
    _input.clear();
    _toBottom();

    try {
      final response = await ref.read(supportRepositoryProvider).ask(message, history, _last?.topic);
      if (!mounted) return;
      setState(() {
        _last = response;
        _turns.add(AssistantTurn(fromMe: false, text: response.reply));
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() => _turns.add(AssistantTurn(fromMe: false, text: '${error.message} You can try again, or write a ticket yourself.')));
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
        ..add(const AssistantTurn(fromMe: false, text: _greeting));
      _last = null;
      _solved = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final last = _last;
    final draft = !_thinking ? last?.ticket : null;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Support assistant'),
        actions: [
          if (_turns.length > 1)
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
                        Text('Looking into it…', style: text.bodySmall?.copyWith(color: Brand.muted)),
                      ],
                    ),
                  ),
                if (last?.phase == 'answered' && !_thinking && !_solved)
                  Padding(
                    padding: const EdgeInsets.only(left: 44, bottom: 10),
                    child: Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        OutlinedButton.icon(
                          onPressed: () => setState(() => _solved = true),
                          icon: const Icon(Icons.check_rounded),
                          label: const Text('Yes, that sorted it'),
                        ),
                        OutlinedButton(
                          onPressed: () => _send("That didn't solve it - I still need help."),
                          child: const Text('No, I still need help'),
                        ),
                      ],
                    ),
                  ),
                if (_solved)
                  Padding(
                    padding: const EdgeInsets.only(left: 44, bottom: 10),
                    child: Text('Glad that is sorted. Ask again any time.',
                        style: text.bodyMedium?.copyWith(color: Brand.forest)),
                  ),
                if (draft != null) ...[
                  const SizedBox(height: 8),
                  _DraftTicket(key: ValueKey(draft), draft: draft),
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
                  hintText: 'My collection code is not accepted',
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
  final AssistantTurn turn;

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
            child: const Icon(Icons.auto_awesome_outlined, size: 18, color: Brand.forest),
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

/// The ticket the assistant prepared. Every field can be changed before it goes.
class _DraftTicket extends ConsumerStatefulWidget {
  const _DraftTicket({super.key, required this.draft});
  final TicketDraft draft;

  @override
  ConsumerState<_DraftTicket> createState() => _DraftTicketState();
}

class _DraftTicketState extends ConsumerState<_DraftTicket> {
  late final _subject = TextEditingController(text: widget.draft.subject);
  late final _body = TextEditingController(text: widget.draft.body);
  late String _category = ticketCategories.containsKey(widget.draft.category) ? widget.draft.category : 'Other';
  bool _busy = false;

  @override
  void dispose() {
    _subject.dispose();
    _body.dispose();
    super.dispose();
  }

  Future<void> _sendTicket() async {
    setState(() => _busy = true);
    try {
      final ticket = await ref.read(supportRepositoryProvider).open(
            subject: _subject.text.trim(),
            category: _category,
            body: _body.text.trim(),
            viaAssistant: true,
          );
      ref.invalidate(myTicketsProvider);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Sent to the support team. They will answer on the ticket.')),
      );
      context.pushReplacement('/profile/support/${ticket.id}');
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
    return Panel(
      color: Brand.mist,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('A ticket for the support team', style: text.titleMedium),
          const SizedBox(height: 4),
          Text(
            'I wrote this from what you told me. Check it, change anything, then send it.',
            style: text.bodySmall?.copyWith(color: Brand.muted, height: 1.4),
          ),
          const SizedBox(height: 14),
          TextField(
            controller: _subject,
            maxLength: 200,
            decoration: const InputDecoration(labelText: 'Subject', counterText: ''),
          ),
          const SizedBox(height: 12),
          DropdownButtonFormField<String>(
            initialValue: _category,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'What is it about?'),
            items: [
              for (final entry in ticketCategories.entries) DropdownMenuItem(value: entry.key, child: Text(entry.value)),
            ],
            onChanged: (value) => setState(() => _category = value ?? 'Other'),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _body,
            minLines: 4,
            maxLines: 10,
            maxLength: 4000,
            decoration: const InputDecoration(labelText: 'Message', alignLabelWithHint: true, counterText: ''),
          ),
          const SizedBox(height: 14),
          InkButton(label: 'Send to the support team', icon: Icons.send_rounded, busy: _busy, onPressed: _sendTicket),
        ],
      ),
    );
  }
}
