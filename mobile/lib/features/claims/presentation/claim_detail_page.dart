import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../data/claim_models.dart';
import 'providers/claim_providers.dart';

class ClaimDetailPage extends ConsumerStatefulWidget {
  const ClaimDetailPage({super.key, required this.claimId});
  final String claimId;
  @override
  ConsumerState<ClaimDetailPage> createState() => _ClaimDetailPageState();
}

class _ClaimDetailPageState extends ConsumerState<ClaimDetailPage> {
  final _answerControllers = <String, TextEditingController>{};
  @override
  void dispose() {
    for (final controller in _answerControllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _submit(ClaimDetail claim) async {
    final unanswered = claim.questions
        .where((question) =>
            question.answerText == null || claim.status == 'RevisionRequested')
        .toList();
    if (unanswered.any((question) =>
        (_answerControllers[question.id]?.text.trim().isEmpty ?? true))) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('Please answer every verification question.')));
      return;
    }
    try {
      await ref.read(claimControllerProvider.notifier).submitAnswers(
          claim.id,
          unanswered
              .map((question) => ClaimAnswerInput(
                  questionId: question.id,
                  answerText: _answerControllers[question.id]!.text.trim()))
              .toList());
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
            content: Text('Your answers were submitted for review.')));
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
            content: Text('Could not submit answers. Please try again.')));
      }
    }
  }

  Future<void> _cancel(ClaimDetail claim) async {
    final reason = TextEditingController();
    final confirmed = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
              title: const Text('Cancel claim?'),
              content: TextField(
                  controller: reason,
                  decoration:
                      const InputDecoration(labelText: 'Reason (optional)')),
              actions: [
                TextButton(
                    onPressed: () => Navigator.pop(context, false),
                    child: const Text('Keep claim')),
                FilledButton(
                    onPressed: () => Navigator.pop(context, true),
                    child: const Text('Cancel claim'))
              ],
            ));
    if (confirmed != true) {
      reason.dispose();
      return;
    }
    try {
      await ref.read(claimControllerProvider.notifier).cancel(
          claim.id, reason.text.trim().isEmpty ? null : reason.text.trim());
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Could not cancel this claim.')));
      }
    }
    reason.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final detail = ref.watch(claimDetailProvider(widget.claimId));
    final busy = ref.watch(claimControllerProvider).isLoading;
    return Scaffold(
        appBar: AppBar(title: const Text('Claim details')),
        body: detail.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => Center(
              child: ElevatedButton(
                  onPressed: () =>
                      ref.invalidate(claimDetailProvider(widget.claimId)),
                  child: const Text('Retry'))),
          data: (claim) {
            final canAnswer =
                claimCanAnswer(claim.status) && claim.questions.isNotEmpty;
            final outstanding = claim.questions
                .where((q) =>
                    q.answerText == null || claim.status == 'RevisionRequested')
                .toList();
            for (final question in outstanding) {
              _answerControllers.putIfAbsent(question.id,
                  () => TextEditingController(text: question.answerText));
            }
            return SingleChildScrollView(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            claim.foundItem.itemTypeName.isEmpty
                                ? claim.foundItem.categoryName
                                : claim.foundItem.itemTypeName,
                            style: Theme.of(context).textTheme.titleLarge,
                          ),
                          const SizedBox(height: 8),
                          Text('Found at ${claim.foundItem.foundLocationName}'),
                          if (claim
                              .foundItem.generalDescription.isNotEmpty) ...[
                            const SizedBox(height: 8),
                            Text(claim.foundItem.generalDescription),
                          ],
                          const SizedBox(height: 12),
                          Chip(label: Text(claimStatusLabel(claim.status))),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                  Text('Your lost report',
                      style: Theme.of(context).textTheme.titleMedium),
                  Text(claim.lostReportDescription),
                  if (claim.status == 'ManualReviewRequired')
                    const Padding(
                      padding: EdgeInsets.only(top: 16),
                      child: _Notice(
                          'Your claim needs staff review. No further action is required unless staff asks for more information.'),
                    ),
                  if (claim.decisionReason?.isNotEmpty == true)
                    Padding(
                        padding: const EdgeInsets.only(top: 16),
                        child: _Notice(claim.decisionReason!)),
                  if (claim.questions.isNotEmpty) ...[
                    const SizedBox(height: 20),
                    Text('Ownership verification',
                        style: Theme.of(context).textTheme.titleMedium),
                    const SizedBox(height: 6),
                    const Text(
                        'Answer these questions from memory. Your answers will be reviewed to help verify ownership.'),
                    const SizedBox(height: 12),
                    ...claim.questions.map((question) {
                      final editable = canAnswer &&
                          outstanding.any((item) => item.id == question.id);
                      return Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: editable
                            ? TextField(
                                controller: _answerControllers[question.id],
                                enabled: !busy,
                                maxLines: 2,
                                decoration: InputDecoration(
                                    labelText: question.questionText,
                                    border: const OutlineInputBorder()),
                              )
                            : ListTile(
                                contentPadding: EdgeInsets.zero,
                                title: Text(question.questionText),
                                subtitle: question.answerText == null
                                    ? const Text('Awaiting your answer')
                                    : Text(
                                        'Your answer: ${question.answerText}'),
                              ),
                      );
                    }),
                    if (canAnswer)
                      FilledButton(
                        onPressed: busy ? null : () => _submit(claim),
                        child: busy
                            ? const CircularProgressIndicator()
                            : const Text('Submit answers'),
                      ),
                  ],
                  if (claimCanCancel(claim.status)) ...[
                    const SizedBox(height: 12),
                    OutlinedButton.icon(
                      onPressed: busy ? null : () => _cancel(claim),
                      icon: const Icon(Icons.cancel_outlined),
                      label: const Text('Cancel claim'),
                    ),
                  ],
                  const SizedBox(height: 16),
                  Text(
                      'Submitted ${DateFormat('MMM d, y').format(claim.createdAt.toLocal())}',
                      style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            );
          },
        ));
  }
}

class _Notice extends StatelessWidget {
  const _Notice(this.text);
  final String text;
  @override
  Widget build(BuildContext context) => Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
          color: const Color(0xFFFFF3E0),
          borderRadius: BorderRadius.circular(8)),
      child: Text(text));
}
