import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';
import '../../feed/presentation/feed_controller.dart';
import '../data/support_repository.dart';

/// Help from the people who run FoundU. A ticket is a conversation with the institution
/// rather than with another student, so it outlives the item it was about.
class SupportPage extends ConsumerWidget {
  const SupportPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tickets = ref.watch(myTicketsProvider);
    final text = Theme.of(context).textTheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Help & support')),
      floatingActionButton: FloatingActionButton.extended(
        heroTag: 'new-ticket',
        backgroundColor: Brand.forest,
        foregroundColor: Colors.white,
        elevation: 0,
        shape: const StadiumBorder(),
        onPressed: () async {
          final opened = await showNewTicketSheet(context);
          if (opened != null && context.mounted) context.push('/profile/support/$opened');
        },
        icon: const Icon(Icons.edit_outlined),
        label: const Text('Write a ticket'),
      ),
      body: RefreshIndicator(
        color: Brand.forest,
        onRefresh: () => ref.refresh(myTicketsProvider.future),
        child: tickets.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ListView(
            padding: const EdgeInsets.all(20),
            children: [
              Panel(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Could not load your tickets', style: text.titleMedium),
                    const SizedBox(height: 4),
                    Text(
                      error is ApiException ? error.message : 'Check your connection and try again.',
                      style: text.bodyMedium?.copyWith(color: Brand.muted),
                    ),
                    const SizedBox(height: 12),
                    InkButton(label: 'Try again', onPressed: () => ref.invalidate(myTicketsProvider)),
                  ],
                ),
              ),
            ],
          ),
          data: (items) => ListView(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 100),
            children: [
              Text('Ask us for help', style: text.headlineSmall),
              const SizedBox(height: 6),
              Text(
                'Something stuck, a code that will not work, a claim that went the wrong way - ask the '
                'assistant first. If it cannot sort it, it writes the ticket for you.',
                style: text.bodyMedium?.copyWith(color: Brand.muted, height: 1.45),
              ),
              const SizedBox(height: 16),
              const _AskAssistantCard(),
              const SizedBox(height: 18),
              if (items.isEmpty)
                Panel(
                  child: Text(
                    'You have not asked us anything yet. When you do, the conversation lives here.',
                    style: text.bodyMedium?.copyWith(color: Brand.muted, height: 1.45),
                  ),
                )
              else
                for (final ticket in items)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: Panel(
                      padding: EdgeInsets.zero,
                      child: ListTile(
                        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                        onTap: () => context.push('/profile/support/${ticket.id}'),
                        title: Text(ticket.subject, maxLines: 2, overflow: TextOverflow.ellipsis),
                        subtitle: Text(
                          '${ticketCategories[ticket.category] ?? ticket.category} · '
                          '${ticketStatusLabels[ticket.status] ?? ticket.status} · ${timeAgo(ticket.lastActivityAt)}',
                        ),
                        trailing: ticket.unreadCount > 0
                            ? Container(
                                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                                decoration: BoxDecoration(color: Brand.forest, borderRadius: BorderRadius.circular(999)),
                                child: Text(
                                  '${ticket.unreadCount} new',
                                  style: text.labelSmall?.copyWith(color: Colors.white, fontWeight: FontWeight.w600),
                                ),
                              )
                            : const Icon(Icons.chevron_right_rounded, color: Brand.faint),
                      ),
                    ),
                  ),
            ],
          ),
        ),
      ),
    );
  }
}

class _AskAssistantCard extends StatelessWidget {
  const _AskAssistantCard();

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Material(
      color: Brand.forest,
      borderRadius: BorderRadius.circular(Brand.radiusCard),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/profile/support/assistant'),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 14, 12, 14),
          child: Row(
            children: [
              Container(
                width: 40,
                height: 40,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: .10),
                  borderRadius: BorderRadius.circular(14),
                ),
                child: const Icon(Icons.auto_awesome_outlined, color: Colors.white),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Ask the assistant first', style: text.titleMedium?.copyWith(color: Colors.white)),
                    const SizedBox(height: 2),
                    Text(
                      'Most problems are sorted in a minute. If not, it writes the ticket.',
                      style: text.bodySmall?.copyWith(color: Colors.white70),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.arrow_forward_rounded, color: Colors.white70),
            ],
          ),
        ),
      ),
    );
  }
}

/// Opens a ticket and returns its id, or null if the person backed out.
Future<String?> showNewTicketSheet(BuildContext context) {
  return showModalBottomSheet<String>(
    context: context,
    // On the root navigator, so the sheet covers the floating nav. Opened from a tab it
    // would otherwise live inside that tab, underneath the nav, hiding its bottom.
    useRootNavigator: true,
    isScrollControlled: true,
    useSafeArea: true,
    backgroundColor: Brand.paper,
    builder: (_) => const _NewTicket(),
  );
}

class _NewTicket extends ConsumerStatefulWidget {
  const _NewTicket();

  @override
  ConsumerState<_NewTicket> createState() => _NewTicketState();
}

class _NewTicketState extends ConsumerState<_NewTicket> {
  final _subject = TextEditingController();
  final _body = TextEditingController();
  String _category = 'Other';
  Map<String, List<String>> _errors = const {};
  bool _busy = false;

  @override
  void dispose() {
    _subject.dispose();
    _body.dispose();
    super.dispose();
  }

  Future<void> _open() async {
    setState(() {
      _busy = true;
      _errors = const {};
    });
    try {
      final ticket = await ref.read(supportRepositoryProvider).open(
            subject: _subject.text.trim(),
            category: _category,
            body: _body.text.trim(),
          );
      ref.invalidate(myTicketsProvider);
      if (!mounted) return;
      Navigator.of(context).pop(ticket.id);
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() => _errors = error.fieldErrors);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    String? errorFor(String field) => (_errors[field] ?? const []).isEmpty ? null : _errors[field]!.join('\n');

    return Padding(
      padding: EdgeInsets.fromLTRB(20, 4, 20, 20 + MediaQuery.viewInsetsOf(context).bottom),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('What do you need?', style: text.titleLarge),
            const SizedBox(height: 16),
            TextField(
              controller: _subject,
              maxLength: 200,
              decoration: InputDecoration(
                labelText: 'Subject',
                hintText: 'My collection code will not work',
                errorText: errorFor('Subject'),
                counterText: '',
              ),
            ),
            const SizedBox(height: 14),
            DropdownButtonFormField<String>(
              initialValue: _category,
              decoration: const InputDecoration(labelText: 'What is it about?'),
              items: [
                for (final entry in ticketCategories.entries)
                  DropdownMenuItem(value: entry.key, child: Text(entry.value)),
              ],
              onChanged: (value) => setState(() => _category = value ?? 'Other'),
            ),
            const SizedBox(height: 14),
            TextField(
              controller: _body,
              minLines: 4,
              maxLines: 8,
              maxLength: 4000,
              decoration: InputDecoration(
                labelText: 'What happened?',
                alignLabelWithHint: true,
                errorText: errorFor('Body'),
                helperText: 'Leave out passwords. Never send the answer to a verification question here.',
                helperMaxLines: 2,
                counterText: '',
              ),
            ),
            const SizedBox(height: 18),
            InkButton(label: 'Open ticket', busy: _busy, onPressed: _open),
          ],
        ),
      ),
    );
  }
}
