import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';
import '../../feed/presentation/feed_controller.dart';
import '../data/support_repository.dart';

/// One support conversation. A closed ticket shows its history and no box, because the API
/// would refuse a reply and a box that fails on send is worse than none.
class TicketPage extends ConsumerStatefulWidget {
  const TicketPage({super.key, required this.ticketId});

  final String ticketId;

  @override
  ConsumerState<TicketPage> createState() => _TicketPageState();
}

class _TicketPageState extends ConsumerState<TicketPage> {
  final _reply = TextEditingController();
  bool _sending = false;

  @override
  void dispose() {
    _reply.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final body = _reply.text.trim();
    if (body.isEmpty || _sending) return;
    setState(() => _sending = true);
    try {
      await ref.read(supportRepositoryProvider).reply(widget.ticketId, body);
      _reply.clear();
      ref.invalidate(ticketProvider(widget.ticketId));
      ref.invalidate(myTicketsProvider);
    } on ApiException catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final ticket = ref.watch(ticketProvider(widget.ticketId));
    final text = Theme.of(context).textTheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Ticket')),
      body: ticket.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Panel(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Could not load this ticket', style: text.titleMedium),
                  const SizedBox(height: 12),
                  InkButton(label: 'Try again', onPressed: () => ref.invalidate(ticketProvider(widget.ticketId))),
                ],
              ),
            ),
          ],
        ),
        data: (data) => Column(
          children: [
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 8, 20, 16),
                children: [
                  Text(data.subject, style: text.titleLarge),
                  const SizedBox(height: 4),
                  Text(
                    '${ticketCategories[data.category] ?? data.category} · '
                    '${ticketStatusLabels[data.status] ?? data.status} · opened ${timeAgo(data.createdAt)}',
                    style: text.bodySmall?.copyWith(color: Brand.muted),
                  ),
                  const SizedBox(height: 18),
                  for (final message in data.messages) _MessageBubble(message: message),
                ],
              ),
            ),
            if (data.isClosed)
              SafeArea(
                top: false,
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(20, 8, 20, 16),
                  child: Text(
                    'This ticket is closed. Open a new one if you need anything else.',
                    textAlign: TextAlign.center,
                    style: text.bodySmall?.copyWith(color: Brand.muted),
                  ),
                ),
              )
            else
              SafeArea(
                top: false,
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
                  child: TextField(
                    controller: _reply,
                    minLines: 1,
                    maxLines: 4,
                    maxLength: 4000,
                    decoration: InputDecoration(
                      counterText: '',
                      hintText: 'Add to this ticket…',
                      suffixIcon: IconButton(
                        tooltip: 'Send',
                        onPressed: _sending ? null : _send,
                        icon: _sending
                            ? const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2))
                            : const Icon(Icons.send_rounded, color: Brand.forest),
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({required this.message});
  final TicketMessage message;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final mine = message.isMine;

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(
        crossAxisAlignment: mine ? CrossAxisAlignment.end : CrossAxisAlignment.start,
        children: [
          Container(
            constraints: BoxConstraints(maxWidth: MediaQuery.sizeOf(context).width * .8),
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
            decoration: BoxDecoration(
              color: mine ? Brand.forest : Brand.surfaceTint,
              borderRadius: BorderRadius.only(
                topLeft: const Radius.circular(18),
                topRight: const Radius.circular(18),
                bottomLeft: Radius.circular(mine ? 18 : 6),
                bottomRight: Radius.circular(mine ? 6 : 18),
              ),
            ),
            child: Text(
              message.body,
              style: text.bodyMedium?.copyWith(color: mine ? Colors.white : Brand.text, height: 1.45),
            ),
          ),
          const SizedBox(height: 4),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (message.isStaffReply && !mine) ...[
                const Icon(Icons.verified_user_outlined, size: 12, color: Brand.muted),
                const SizedBox(width: 4),
              ],
              Text(
                '${mine ? 'You' : message.senderName.split(' ').first}'
                '${message.isStaffReply ? ' · FoundU' : ''} · ${timeAgo(message.createdAt)}',
                style: text.labelSmall?.copyWith(color: Brand.muted),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
