import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../data/report_models.dart';
import 'providers/report_providers.dart';

class PossibleMatchesPage extends ConsumerWidget {
  final String reportId;

  const PossibleMatchesPage({super.key, required this.reportId});

  void _showClaimInfoDialog(BuildContext context, FoundReportSummaryModel item) {
    showDialog(
      context: context,
      builder: (dialogCtx) {
        return AlertDialog(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
          title: const Row(
            children: [
              Icon(Icons.verified, color: Color(0xFF2E7D32)),
              SizedBox(width: 8),
              Text('Claim at Desk'),
            ],
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'This item matching your lost report is securely stored at campus security / desk.',
                style: const TextStyle(fontSize: 14),
              ),
              const SizedBox(height: 12),
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: const Color(0xFFE8F5E9),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Item: ${item.itemTypeName}', style: const TextStyle(fontWeight: FontWeight.bold)),
                    const SizedBox(height: 4),
                    Text('Found Location: ${item.foundLocationName}'),
                    const SizedBox(height: 4),
                    Text('Date Found: ${DateFormat('MMM d, yyyy').format(item.foundAt.toLocal())}'),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              const Text(
                'To claim this item, please visit the Lost & Found desk during office hours with your Student ID card. Desk staff will verify ownership questions.',
                style: TextStyle(fontSize: 12, color: Colors.grey),
              ),
            ],
          ),
          actions: [
            ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF2E7D32),
                foregroundColor: Colors.white,
              ),
              onPressed: () => Navigator.of(dialogCtx).pop(),
              child: const Text('Understood'),
            ),
          ],
        );
      },
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final matchesAsync = ref.watch(possibleMatchesProvider(reportId));
    final dateFormat = DateFormat('MMM d, yyyy  h:mm a');

    return Scaffold(
      appBar: AppBar(
        title: const Text('Possible Matches', style: TextStyle(fontWeight: FontWeight.bold)),
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.refresh(possibleMatchesProvider(reportId)),
          ),
        ],
      ),
      body: matchesAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: Color(0xFF2E7D32)),
        ),
        error: (err, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline, size: 48, color: Colors.red),
                const SizedBox(height: 12),
                Text('Failed to load possible matches', style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 8),
                Text(err.toString(), style: const TextStyle(color: Colors.grey, fontSize: 12)),
                const SizedBox(height: 16),
                ElevatedButton(
                  onPressed: () => ref.refresh(possibleMatchesProvider(reportId)),
                  child: const Text('Retry'),
                )
              ],
            ),
          ),
        ),
        data: (matches) {
          if (matches.isEmpty) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(32),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.auto_awesome, size: 64, color: Colors.amber[400]),
                    const SizedBox(height: 16),
                    Text(
                      'No matches found yet',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.bold,
                          ),
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'Our matching system automatically compares reported lost items against turned-in found items. Check back later!',
                      textAlign: TextAlign.center,
                      style: TextStyle(color: Colors.grey, fontSize: 13),
                    ),
                  ],
                ),
              ),
            );
          }

          return Column(
            children: [
              // Notice banner header
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(14),
                color: const Color(0xFFE8F5E9),
                child: Row(
                  children: [
                    const Icon(Icons.lightbulb_outline, color: Color(0xFF1E5631)),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        'Found ${matches.length} candidate item${matches.length > 1 ? 's' : ''} that match your lost report criteria.',
                        style: const TextStyle(
                          fontSize: 13,
                          color: Color(0xFF1E5631),
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ],
                ),
              ),

              // Match list
              Expanded(
                child: ListView.builder(
                  padding: const EdgeInsets.all(16),
                  itemCount: matches.length,
                  itemBuilder: (context, index) {
                    final match = matches[index];
                    final item = match.foundItem;
                    final hasScore = match.isAgentGenerated && match.matchScore != null;
                    final scorePercentage = hasScore ? (match.matchScore! * 100).round() : null;

                    return Card(
                      margin: const EdgeInsets.only(bottom: 16),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                      elevation: 3,
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            // Card Top: Match Score Badge & Status
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                if (hasScore)
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: const Color(0xFF2E7D32),
                                      borderRadius: BorderRadius.circular(12),
                                    ),
                                    child: Row(
                                      children: [
                                        const Icon(Icons.auto_awesome, size: 14, color: Colors.white),
                                        const SizedBox(width: 4),
                                        Text(
                                          '$scorePercentage% MATCH',
                                          style: const TextStyle(
                                            color: Colors.white,
                                            fontSize: 11,
                                            fontWeight: FontWeight.bold,
                                          ),
                                        ),
                                      ],
                                    ),
                                  )
                                else
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: Colors.blue[100],
                                      borderRadius: BorderRadius.circular(12),
                                    ),
                                    child: Text(
                                      'DESK MATCH',
                                      style: TextStyle(
                                        color: Colors.blue[900],
                                        fontSize: 11,
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                  ),
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                                  decoration: BoxDecoration(
                                    color: Colors.grey[200],
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Text(
                                    item.categoryName,
                                    style: const TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: Colors.black87),
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 12),

                            // Item Name & Location
                            Text(
                              item.itemTypeName.isNotEmpty ? item.itemTypeName : item.categoryName,
                              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                                    fontWeight: FontWeight.bold,
                                  ),
                            ),
                            const SizedBox(height: 6),

                            Row(
                              children: [
                                const Icon(Icons.place_outlined, size: 16, color: Color(0xFF2E7D32)),
                                const SizedBox(width: 4),
                                Expanded(
                                  child: Text(
                                    'Found at: ${item.foundLocationName}',
                                    style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 4),
                            Row(
                              children: [
                                const Icon(Icons.event, size: 16, color: Colors.grey),
                                const SizedBox(width: 4),
                                Text(
                                  'Found on: ${dateFormat.format(item.foundAt.toLocal())}',
                                  style: const TextStyle(fontSize: 12, color: Colors.grey),
                                ),
                              ],
                            ),
                            const SizedBox(height: 10),

                            // General Description
                            Text(
                              item.generalDescription,
                              style: TextStyle(fontSize: 13, color: Colors.grey[800]),
                              maxLines: 3,
                              overflow: TextOverflow.ellipsis,
                            ),

                            if (match.note != null && match.note!.isNotEmpty) ...[
                              const SizedBox(height: 8),
                              Text(
                                'Note: ${match.note}',
                                style: const TextStyle(fontSize: 12, fontStyle: FontStyle.italic, color: Colors.teal),
                              ),
                            ],

                            const SizedBox(height: 14),

                            // Action footer
                            Row(
                              mainAxisAlignment: MainAxisAlignment.end,
                              children: [
                                ElevatedButton.icon(
                                  onPressed: () => _showClaimInfoDialog(context, item),
                                  style: ElevatedButton.styleFrom(
                                    backgroundColor: const Color(0xFF2E7D32),
                                    foregroundColor: Colors.white,
                                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                                  ),
                                  icon: const Icon(Icons.verified, size: 18),
                                  label: const Text('Claim at Campus Desk'),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
