import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../data/report_models.dart';
import 'providers/report_providers.dart';

class MyReportsPage extends ConsumerStatefulWidget {
  const MyReportsPage({super.key});

  @override
  ConsumerState<MyReportsPage> createState() => _MyReportsPageState();
}

class _MyReportsPageState extends ConsumerState<MyReportsPage> {
  final TextEditingController _searchController = TextEditingController();
  String _searchQuery = '';

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  void _showWithdrawDialog(BuildContext context, LostReportListItemModel item) {
    final reasonController = TextEditingController();

    showDialog(
      context: context,
      builder: (dialogCtx) {
        return AlertDialog(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
          title: const Row(
            children: [
              Icon(Icons.warning_amber_rounded, color: Colors.amber, size: 24),
              SizedBox(width: 8),
              Text('Withdraw Report'),
            ],
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Are you sure you want to withdraw "${item.itemTypeName.isNotEmpty ? item.itemTypeName : item.categoryName}"? This indicates you no longer need assistance finding this item.',
                style: const TextStyle(fontSize: 14, height: 1.4),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: reasonController,
                decoration: InputDecoration(
                  labelText: 'Reason for withdrawal (Optional)',
                  hintText: 'e.g. Found it at home, item replaced...',
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                  contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                ),
                maxLines: 2,
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogCtx).pop(),
              child: const Text('Cancel'),
            ),
            ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.red[700],
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
              ),
              onPressed: () async {
                Navigator.of(dialogCtx).pop();
                try {
                  await ref.read(reportControllerProvider.notifier).withdrawReport(
                        reportId: item.id,
                        reason: reasonController.text.trim(),
                      );
                  ref.invalidate(myReportsProvider);
                  if (!context.mounted) return;
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(
                      content: Text('Report withdrawn. It no longer appears on the feed.'),
                      backgroundColor: Colors.black87,
                    ),
                  );
                } catch (e) {
                  if (!context.mounted) return;
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(content: Text('Failed to withdraw: $e')),
                  );
                }
              },
              child: const Text('Withdraw Report'),
            ),
          ],
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final statusFilter = ref.watch(selectedStatusFilterProvider);
    final reportsAsync = ref.watch(myReportsProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('My Lost Reports', style: TextStyle(fontWeight: FontWeight.bold)),
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.refresh(myReportsProvider),
            tooltip: 'Refresh',
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push('/reports/new'),
        backgroundColor: const Color(0xFF2E7D32),
        foregroundColor: Colors.white,
        icon: const Icon(Icons.add),
        label: const Text('Report Lost Item', style: TextStyle(fontWeight: FontWeight.w600)),
      ),
      body: Column(
        children: [
          // Filter & Search bar header
          Container(
            color: Theme.of(context).colorScheme.surface,
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
            child: Column(
              children: [
                // Search field
                TextField(
                  controller: _searchController,
                  decoration: InputDecoration(
                    hintText: 'Search by category, location, or description...',
                    prefixIcon: const Icon(Icons.search, size: 20),
                    suffixIcon: _searchQuery.isNotEmpty
                        ? IconButton(
                            icon: const Icon(Icons.clear, size: 18),
                            onPressed: () {
                              _searchController.clear();
                              setState(() => _searchQuery = '');
                            },
                          )
                        : null,
                    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                    filled: true,
                    fillColor: Theme.of(context).colorScheme.surfaceContainerHighest.withValues(alpha: 0.5),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(12),
                      borderSide: BorderSide.none,
                    ),
                  ),
                  onChanged: (val) {
                    setState(() => _searchQuery = val.trim().toLowerCase());
                  },
                ),
                const SizedBox(height: 12),
                // Status Filter Chips
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(
                    children: ['All', 'Active', 'Matched', 'Resolved', 'Withdrawn'].map((status) {
                      final isSelected = statusFilter == status;
                      return Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: ChoiceChip(
                          label: Text(status),
                          selected: isSelected,
                          selectedColor: const Color(0xFF2E7D32),
                          labelStyle: TextStyle(
                            color: isSelected ? Colors.white : Theme.of(context).colorScheme.onSurface,
                            fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                          ),
                          onSelected: (selected) {
                            if (selected) {
                              ref.read(selectedStatusFilterProvider.notifier).setStatus(status);
                            }
                          },
                        ),
                      );
                    }).toList(),
                  ),
                ),
              ],
            ),
          ),
          const Divider(height: 1),

          // Reports List
          Expanded(
            child: reportsAsync.when(
              loading: () => const Center(
                child: CircularProgressIndicator(color: Color(0xFF2E7D32)),
              ),
              error: (err, stack) => Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.error_outline, size: 48, color: Colors.redAccent),
                      const SizedBox(height: 16),
                      Text(
                        'Failed to load lost reports',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                      const SizedBox(height: 8),
                      Text(
                        err.toString(),
                        textAlign: TextAlign.center,
                        style: TextStyle(color: Colors.grey[600], fontSize: 13),
                      ),
                      const SizedBox(height: 16),
                      ElevatedButton.icon(
                        onPressed: () => ref.refresh(myReportsProvider),
                        icon: const Icon(Icons.refresh),
                        label: const Text('Retry'),
                      )
                    ],
                  ),
                ),
              ),
              data: (pagedResult) {
                var items = pagedResult.items;

                // Client-side search filter
                if (_searchQuery.isNotEmpty) {
                  items = items.where((item) {
                    return item.categoryName.toLowerCase().contains(_searchQuery) ||
                        item.itemTypeName.toLowerCase().contains(_searchQuery) ||
                        item.lastSeenLocationName.toLowerCase().contains(_searchQuery) ||
                        item.description.toLowerCase().contains(_searchQuery);
                  }).toList();
                }

                if (items.isEmpty) {
                  return Center(
                    child: Padding(
                      padding: const EdgeInsets.all(32),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(
                            Icons.find_in_page_outlined,
                            size: 64,
                            color: Colors.grey[400],
                          ),
                          const SizedBox(height: 16),
                          Text(
                            _searchQuery.isNotEmpty
                                ? 'No reports match your search'
                                : statusFilter != 'All'
                                    ? 'No $statusFilter reports found'
                                    : 'You haven\'t reported any lost items yet',
                            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                                  color: Colors.grey[700],
                                  fontWeight: FontWeight.w600,
                                ),
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: 8),
                          Text(
                            'Tap the button below to post a lost item report.',
                            textAlign: TextAlign.center,
                            style: TextStyle(color: Colors.grey[600], fontSize: 13),
                          ),
                        ],
                      ),
                    ),
                  );
                }

                return RefreshIndicator(
                  onRefresh: () async => ref.refresh(myReportsProvider),
                  color: const Color(0xFF2E7D32),
                  child: ListView.builder(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 80),
                    itemCount: items.length,
                    itemBuilder: (context, index) {
                      final item = items[index];
                      return _ReportCard(
                        item: item,
                        onWithdraw: () => _showWithdrawDialog(context, item),
                      );
                    },
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _ReportCard extends StatelessWidget {
  final LostReportListItemModel item;
  final VoidCallback onWithdraw;

  const _ReportCard({
    required this.item,
    required this.onWithdraw,
  });

  Color _getStatusColor(String status) {
    switch (status.toLowerCase()) {
      case 'active':
        return const Color(0xFF2E7D32);
      case 'matched':
        return Colors.blue[700]!;
      case 'resolved':
        return Colors.purple[700]!;
      case 'withdrawn':
        return Colors.grey[600]!;
      default:
        return Colors.teal;
    }
  }

  String _formatElapsed(DateTime createdAt) {
    final diff = DateTime.now().difference(createdAt);
    if (diff.inDays > 0) {
      return '${diff.inDays} ${diff.inDays == 1 ? 'day' : 'days'} on the feed';
    } else if (diff.inHours > 0) {
      return '${diff.inHours} ${diff.inHours == 1 ? 'hour' : 'hours'} on the feed';
    } else {
      return 'Just posted on the feed';
    }
  }

  int _getStageIndex(String status) {
    switch (status.toLowerCase()) {
      case 'active':
        return 0; // Reported
      case 'matched':
        return 1; // Matched
      case 'claimed':
        return 2; // Claimed
      case 'resolved':
        return 3; // Handover / Resolved
      default:
        return 0;
    }
  }

  @override
  Widget build(BuildContext context) {
    final dateFormat = DateFormat('MMM d, h:mm a');
    final statusColor = _getStatusColor(item.status);
    final isWithdrawn = item.status.toLowerCase() == 'withdrawn';
    final isActive = item.status.toLowerCase() == 'active';
    final stageIndex = _getStageIndex(item.status);

    return Card(
      margin: const EdgeInsets.only(bottom: 16),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(
          color: isWithdrawn
              ? Colors.grey.withValues(alpha: 0.2)
              : Colors.grey.withValues(alpha: 0.25),
        ),
      ),
      elevation: isWithdrawn ? 0 : 2,
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () => context.push('/reports/${item.id}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Header Row: Photo/Icon + Item Title & Status Badge
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Photo or Category Avatar
                  Container(
                    width: 48,
                    height: 48,
                    decoration: BoxDecoration(
                      color: isWithdrawn
                          ? Colors.grey[200]
                          : const Color(0xFFE8F5E9),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    clipBehavior: Clip.antiAlias,
                    child: item.photoUrls.isNotEmpty
                        ? Image.network(
                            item.photoUrls.first,
                            fit: BoxFit.cover,
                            errorBuilder: (_, __, ___) => Icon(
                              Icons.inventory_2_outlined,
                              color: isWithdrawn ? Colors.grey : const Color(0xFF2E7D32),
                            ),
                          )
                        : Icon(
                            Icons.inventory_2_outlined,
                            size: 26,
                            color: isWithdrawn ? Colors.grey : const Color(0xFF2E7D32),
                          ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: Text(
                                item.itemTypeName.isNotEmpty
                                    ? item.itemTypeName
                                    : item.categoryName,
                                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                                      fontWeight: FontWeight.bold,
                                      fontSize: 16,
                                      color: isWithdrawn ? Colors.grey[700] : null,
                                    ),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                            const SizedBox(width: 6),
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                              decoration: BoxDecoration(
                                color: statusColor.withValues(alpha: 0.12),
                                borderRadius: BorderRadius.circular(10),
                                border: Border.all(color: statusColor.withValues(alpha: 0.4)),
                              ),
                              child: Text(
                                item.status.toUpperCase(),
                                style: TextStyle(
                                  color: statusColor,
                                  fontSize: 10,
                                  fontWeight: FontWeight.bold,
                                  letterSpacing: 0.5,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 2),
                        Text(
                          '${item.categoryName} · ${item.lastSeenLocationName}',
                          style: TextStyle(fontSize: 12, color: Colors.grey[600]),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),

              // Found notice banner (matching Web UI)
              if (!isWithdrawn && item.foundClaimCount > 0) ...[
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                  decoration: BoxDecoration(
                    color: const Color(0xFFE8F5E9),
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: const Color(0xFFA5D6A7)),
                  ),
                  child: Row(
                    children: [
                      const Icon(Icons.notifications_active, size: 18, color: Color(0xFF2E7D32)),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          item.foundClaimCount == 1
                              ? 'Someone reported finding this item!'
                              : '${item.foundClaimCount} people reported finding this item!',
                          style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                            color: Color(0xFF1E5631),
                          ),
                        ),
                      ),
                      if (item.messageCount > 0) ...[
                        const SizedBox(width: 6),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                          decoration: BoxDecoration(
                            color: Colors.blue[100],
                            borderRadius: BorderRadius.circular(6),
                          ),
                          child: Text(
                            '${item.messageCount} msg',
                            style: TextStyle(
                              fontSize: 10,
                              fontWeight: FontWeight.bold,
                              color: Colors.blue[900],
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(height: 10),
              ],

              // Feed duration metric text
              Text(
                isWithdrawn
                    ? 'Withdrawn and off the feed'
                    : _formatElapsed(item.createdAt),
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.bold,
                  color: isWithdrawn ? Colors.grey[600] : const Color(0xFF1E5631),
                ),
              ),
              const SizedBox(height: 10),

              // Visual Lifecycle Progress Track (matching Web UI stage track)
              if (!isWithdrawn) ...[
                _buildStageTracker(context, stageIndex),
                const SizedBox(height: 12),
              ],

              // Description snippet
              Text(
                item.description,
                style: TextStyle(fontSize: 13, color: Colors.grey[800], height: 1.3),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 6),

              // Lost time subtitle
              Row(
                children: [
                  const Icon(Icons.access_time, size: 14, color: Colors.grey),
                  const SizedBox(width: 4),
                  Text(
                    'Lost: ${dateFormat.format(item.estimatedLostFromAt.toLocal())}',
                    style: TextStyle(fontSize: 11, color: Colors.grey[600]),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              const Divider(height: 1),
              const SizedBox(height: 8),

              // Action Buttons Row (Web-like: Details, Edit, Withdraw, Matches)
              Row(
                children: [
                  // Details Link Button
                  InkWell(
                    onTap: () => context.push('/reports/${item.id}'),
                    borderRadius: BorderRadius.circular(8),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                      child: Row(
                        children: [
                          Icon(Icons.visibility_outlined, size: 16, color: Colors.grey[700]),
                          const SizedBox(width: 4),
                          Text(
                            'Details',
                            style: TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w600,
                              color: Colors.grey[800],
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),

                  if (isActive) ...[
                    const SizedBox(width: 8),
                    // Edit Button
                    OutlinedButton.icon(
                      style: OutlinedButton.styleFrom(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        visualDensity: VisualDensity.compact,
                        side: BorderSide(color: Colors.grey[400]!),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                      ),
                      onPressed: () => context.push('/reports/${item.id}/edit'),
                      icon: const Icon(Icons.edit_outlined, size: 15, color: Colors.black87),
                      label: const Text(
                        'Edit',
                        style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: Colors.black87),
                      ),
                    ),
                    const SizedBox(width: 6),
                    // Withdraw Button
                    OutlinedButton.icon(
                      style: OutlinedButton.styleFrom(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        visualDensity: VisualDensity.compact,
                        side: BorderSide(color: Colors.red[300]!),
                        backgroundColor: Colors.red[50]?.withValues(alpha: 0.5),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                      ),
                      onPressed: onWithdraw,
                      icon: const Icon(Icons.undo, size: 15, color: Colors.redAccent),
                      label: const Text(
                        'Withdraw',
                        style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: Colors.redAccent),
                      ),
                    ),
                  ],

                  const Spacer(),

                  if (item.status.toLowerCase() == 'active' || item.status.toLowerCase() == 'matched')
                    TextButton.icon(
                      style: TextButton.styleFrom(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        visualDensity: VisualDensity.compact,
                      ),
                      onPressed: () => context.push('/reports/${item.id}/matches'),
                      icon: const Icon(Icons.auto_awesome, size: 15, color: Color(0xFF2E7D32)),
                      label: const Text(
                        'Matches',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.bold,
                          color: Color(0xFF2E7D32),
                        ),
                      ),
                    ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildStageTracker(BuildContext context, int currentStage) {
    final stages = ['Reported', 'Matched', 'Claimed', 'Resolved'];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: List.generate(stages.length, (index) {
            final isReached = index <= currentStage;
            final isCurrent = index == currentStage;

            return Expanded(
              child: Row(
                children: [
                  // Dot indicator
                  Container(
                    width: isCurrent ? 12 : 8,
                    height: isCurrent ? 12 : 8,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: isReached
                          ? const Color(0xFF2E7D32)
                          : Colors.grey[300],
                      border: isCurrent
                          ? Border.all(color: const Color(0xFF1E5631), width: 2)
                          : null,
                    ),
                  ),
                  // Connecting line
                  if (index < stages.length - 1)
                    Expanded(
                      child: Container(
                        height: 2,
                        color: index < currentStage
                            ? const Color(0xFF2E7D32)
                            : Colors.grey[300],
                      ),
                    ),
                ],
              ),
            );
          }),
        ),
        const SizedBox(height: 4),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text('Reported', style: TextStyle(fontSize: 10, color: currentStage >= 0 ? const Color(0xFF2E7D32) : Colors.grey)),
            Text('Matched', style: TextStyle(fontSize: 10, color: currentStage >= 1 ? const Color(0xFF2E7D32) : Colors.grey)),
            Text('Claimed', style: TextStyle(fontSize: 10, color: currentStage >= 2 ? const Color(0xFF2E7D32) : Colors.grey)),
            Text('Resolved', style: TextStyle(fontSize: 10, color: currentStage >= 3 ? const Color(0xFF2E7D32) : Colors.grey)),
          ],
        ),
      ],
    );
  }
}
