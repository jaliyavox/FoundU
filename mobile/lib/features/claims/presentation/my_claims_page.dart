import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../data/claim_models.dart';
import 'providers/claim_providers.dart';

class MyClaimsPage extends ConsumerWidget {
  const MyClaimsPage({super.key});
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final claims = ref.watch(myClaimsProvider);
    final pager = ref.read(myClaimsProvider.notifier);
    return Scaffold(
      appBar: AppBar(title: const Text('My Claims'), actions: [
        IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: claims.isInitialLoading ? null : pager.refresh)
      ]),
      body: claims.isInitialLoading
          ? const Center(child: CircularProgressIndicator())
          : claims.initialError != null
              ? _MessageState(
                  icon: Icons.error_outline,
                  title: 'Could not load claims',
                  message: 'Please try again.',
                  action: pager.refresh)
              : claims.items.isEmpty
                  ? const _MessageState(
                      icon: Icons.assignment_outlined,
                      title: 'No claims yet',
                      message:
                          'Claims you submit for possible matches will appear here.')
                  : RefreshIndicator(
                      onRefresh: pager.refresh,
                      child: ListView.builder(
                        padding: const EdgeInsets.all(16),
                        itemCount:
                            claims.items.length + (claims.hasMore ? 1 : 0),
                        itemBuilder: (_, index) {
                          if (index < claims.items.length) {
                            return _ClaimCard(claim: claims.items[index]);
                          }
                          return _LoadMore(
                            isLoading: claims.isLoadingMore,
                            hasError: claims.loadMoreError != null,
                            onPressed: pager.loadMore,
                          );
                        },
                      ),
                    ),
    );
  }
}

class _LoadMore extends StatelessWidget {
  const _LoadMore({
    required this.isLoading,
    required this.hasError,
    required this.onPressed,
  });

  final bool isLoading;
  final bool hasError;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 12),
        child: Column(
          children: [
            if (hasError)
              const Padding(
                padding: EdgeInsets.only(bottom: 8),
                child: Text('Could not load more claims. Please try again.'),
              ),
            FilledButton.tonal(
              onPressed: isLoading ? null : onPressed,
              child: isLoading
                  ? const SizedBox(
                      height: 18,
                      width: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Load more'),
            ),
          ],
        ),
      );
}

class _ClaimCard extends StatelessWidget {
  const _ClaimCard({required this.claim});
  final ClaimListItem claim;
  @override
  Widget build(BuildContext context) => Card(
        child: ListTile(
          onTap: () => context.push('/claims/${claim.id}'),
          title: Text(
              claim.itemTypeName.isEmpty
                  ? claim.categoryName
                  : claim.itemTypeName,
              style: const TextStyle(fontWeight: FontWeight.bold)),
          subtitle: Text(
              '${claimStatusLabel(claim.status)}\nUpdated ${DateFormat('MMM d, y').format(claim.updatedAt.toLocal())}'),
          isThreeLine: true,
          trailing: claim.unansweredQuestionCount > 0
              ? Chip(label: Text('${claim.unansweredQuestionCount} to answer'))
              : const Icon(Icons.chevron_right),
        ),
      );
}

class _MessageState extends StatelessWidget {
  const _MessageState(
      {required this.icon,
      required this.title,
      required this.message,
      this.action});
  final IconData icon;
  final String title;
  final String message;
  final VoidCallback? action;
  @override
  Widget build(BuildContext context) => Center(
      child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Icon(icon, size: 52, color: Colors.grey),
            const SizedBox(height: 12),
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 6),
            Text(message, textAlign: TextAlign.center),
            if (action != null) ...[
              const SizedBox(height: 12),
              ElevatedButton(onPressed: action, child: const Text('Retry'))
            ]
          ])));
}
