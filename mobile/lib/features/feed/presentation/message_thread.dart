import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/brand.dart';
import '../data/feed_models.dart';
import '../data/feed_repository.dart';
import 'feed_controller.dart';

/// One conversation on a lost report, as a chat: the reader's messages on the right.
///
/// Used on both sides. A finder sees their own thread with the author; the author sees every
/// thread and picks which finder to reply to. The API refuses a reply to anyone who has not
/// written first, so the author can never open contact with a stranger.
class MessageThread extends ConsumerStatefulWidget {
  const MessageThread({super.key, required this.reportId, required this.isAuthor});

  final String reportId;
  final bool isAuthor;

  @override
  ConsumerState<MessageThread> createState() => _MessageThreadState();
}

class _MessageThreadState extends ConsumerState<MessageThread> {
  final _body = TextEditingController();
  List<ReportMessage>? _messages;
  String? _error;
  String? _replyTo;
  bool _sending = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _body.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final messages = await ref.read(feedRepositoryProvider).getMessages(widget.reportId);
      if (!mounted) return;
      setState(() {
        _messages = messages;
        _error = null;
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() => _error = error.message);
    }
  }

  Map<String, List<ReportMessage>> get _threads {
    final threads = <String, List<ReportMessage>>{};
    for (final m in _messages ?? const <ReportMessage>[]) {
      threads.putIfAbsent(m.counterpartId, () => []).add(m);
    }
    return threads;
  }

  String? get _active {
    final threads = _threads;
    if (threads.isEmpty) return null;
    return widget.isAuthor ? (_replyTo ?? threads.keys.first) : threads.keys.first;
  }

  Future<void> _send() async {
    final text = _body.text.trim();
    if (text.isEmpty) return;
    setState(() => _sending = true);
    try {
      await ref.read(feedRepositoryProvider).sendMessage(widget.reportId, text, recipientId: widget.isAuthor ? _active : null);
      _body.clear();
      await _load();
    } on ApiException catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    if (_error != null) return Text(_error!, style: text.bodySmall?.copyWith(color: Brand.danger));
    if (_messages == null) return const LinearProgressIndicator();

    final threads = _threads;
    final active = _active;
    final shown = active == null ? const <ReportMessage>[] : threads[active]!;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (widget.isAuthor && threads.isEmpty)
          Text('Nobody has written about this yet. When a finder does, you can reply here.',
              style: text.bodySmall?.copyWith(color: Brand.muted, height: 1.4)),
        if (widget.isAuthor && threads.length > 1) ...[
          Wrap(
            spacing: 8,
            children: [
              for (final entry in threads.entries)
                ChoiceChip(
                  label: Text(entry.value.first.counterpartName),
                  selected: entry.key == active,
                  onSelected: (_) => setState(() => _replyTo = entry.key),
                ),
            ],
          ),
          const SizedBox(height: 10),
        ],
        for (final m in shown)
          Align(
            alignment: m.isMine ? Alignment.centerRight : Alignment.centerLeft,
            child: Column(
              crossAxisAlignment: m.isMine ? CrossAxisAlignment.end : CrossAxisAlignment.start,
              children: [
                Container(
                  constraints: const BoxConstraints(maxWidth: 300),
                  margin: const EdgeInsets.only(bottom: 2),
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 9),
                  decoration: BoxDecoration(
                    color: m.isMine ? Brand.forest : Brand.surfaceTint,
                    borderRadius: BorderRadius.only(
                      topLeft: const Radius.circular(18),
                      topRight: const Radius.circular(18),
                      bottomLeft: Radius.circular(m.isMine ? 18 : 6),
                      bottomRight: Radius.circular(m.isMine ? 6 : 18),
                    ),
                  ),
                  child: Text(m.body, style: TextStyle(color: m.isMine ? Colors.white : Brand.text, height: 1.4)),
                ),
                Padding(
                  padding: const EdgeInsets.only(bottom: 10, left: 4, right: 4),
                  child: Text('${m.isMine ? 'You' : m.senderName.split(' ').first} · ${timeAgo(m.createdAt)}',
                      style: text.bodySmall?.copyWith(color: Brand.faint, fontSize: 11)),
                ),
              ],
            ),
          ),
        // The author only gets a box once a thread exists; a finder always has one.
        if (!widget.isAuthor || active != null) ...[
          TextField(
            controller: _body,
            minLines: 1,
            maxLines: 4,
            decoration: InputDecoration(
              hintText: widget.isAuthor
                  ? 'Reply to ${threads[active]!.first.counterpartName.split(' ').first}'
                  : 'I found this and handed it in at the library desk.',
              suffixIcon: IconButton(
                onPressed: _sending ? null : _send,
                icon: _sending
                    ? const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Icon(Icons.send_rounded),
                color: Brand.forest,
                tooltip: 'Send',
              ),
            ),
            onSubmitted: (_) => _send(),
          ),
        ],
      ],
    );
  }
}
