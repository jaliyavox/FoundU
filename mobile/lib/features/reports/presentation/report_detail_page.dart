import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import 'providers/report_providers.dart';

class LostReportDetailPage extends ConsumerStatefulWidget {
  final String reportId;

  const LostReportDetailPage({super.key, required this.reportId});

  @override
  ConsumerState<LostReportDetailPage> createState() => _LostReportDetailPageState();
}

class _LostReportDetailPageState extends ConsumerState<LostReportDetailPage> {
  void _showWithdrawDialog(BuildContext context) {
    final reasonController = TextEditingController();

    showDialog(
      context: context,
      builder: (dialogCtx) {
        return AlertDialog(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
          title: const Row(
            children: [
              Icon(Icons.warning_amber_rounded, color: Colors.amber),
              SizedBox(width: 8),
              Text('Withdraw Report'),
            ],
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Are you sure you want to withdraw this lost report? This indicates you no longer need assistance finding this item.',
                style: TextStyle(fontSize: 14),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: reasonController,
                decoration: InputDecoration(
                  labelText: 'Reason for withdrawal (Optional)',
                  hintText: 'e.g. Found it at home, item replaced...',
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
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
              ),
              onPressed: () async {
                Navigator.of(dialogCtx).pop();
                try {
                  await ref
                      .read(reportControllerProvider.notifier)
                      .withdrawReport(
                        reportId: widget.reportId,
                        reason: reasonController.text.trim(),
                      );
                  if (mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Report withdrawn successfully.')),
                    );
                  }
                } catch (e) {
                  if (mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(content: Text('Failed to withdraw: $e')),
                    );
                  }
                }
              },
              child: const Text('Withdraw'),
            ),
          ],
        );
      },
    );
  }

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

  @override
  Widget build(BuildContext context) {
    final detailAsync = ref.watch(reportDetailProvider(widget.reportId));
    final controllerState = ref.watch(reportControllerProvider);
    final dateFormat = DateFormat('EEE, MMM d, yyyy  h:mm a');

    return Scaffold(
      appBar: AppBar(
        title: const Text('Report Details', style: TextStyle(fontWeight: FontWeight.bold)),
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.refresh(reportDetailProvider(widget.reportId)),
          ),
        ],
      ),
      body: detailAsync.when(
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
                Text('Failed to load report details', style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 8),
                Text(err.toString(), style: const TextStyle(color: Colors.grey, fontSize: 12)),
                const SizedBox(height: 16),
                ElevatedButton(
                  onPressed: () => ref.refresh(reportDetailProvider(widget.reportId)),
                  child: const Text('Retry'),
                )
              ],
            ),
          ),
        ),
        data: (report) {
          final statusColor = _getStatusColor(report.status);
          final isActive = report.status.toLowerCase() == 'active';
          final isWithdrawn = report.status.toLowerCase() == 'withdrawn';

          return Stack(
            children: [
              SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 100),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    // Photo Gallery / Carousel Header
                    if (report.photos.isNotEmpty)
                      SizedBox(
                        height: 220,
                        child: PageView.builder(
                          itemCount: report.photos.length,
                          itemBuilder: (context, index) {
                            final photo = report.photos[index];
                            return Container(
                              margin: const EdgeInsets.only(right: 8),
                              decoration: BoxDecoration(
                                borderRadius: BorderRadius.circular(16),
                                color: Colors.grey[200],
                              ),
                              clipBehavior: Clip.antiAlias,
                              child: Image.network(
                                photo.url,
                                fit: BoxFit.cover,
                                errorBuilder: (_, __, ___) => const Center(
                                  child: Icon(Icons.image_not_supported, size: 48, color: Colors.grey),
                                ),
                              ),
                            );
                          },
                        ),
                      )
                    else
                      Container(
                        height: 140,
                        decoration: BoxDecoration(
                          color: const Color(0xFFE8F5E9),
                          borderRadius: BorderRadius.circular(16),
                          border: Border.all(color: const Color(0xFFA5D6A7)),
                        ),
                        child: const Center(
                          child: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              Icon(Icons.inventory_2_outlined, size: 48, color: Color(0xFF2E7D32)),
                              SizedBox(height: 8),
                              Text('No photos attached', style: TextStyle(color: Color(0xFF1E5631), fontWeight: FontWeight.bold)),
                            ],
                          ),
                        ),
                      ),
                    const SizedBox(height: 20),

                    // Status & Category Row
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                          decoration: BoxDecoration(
                            color: const Color(0xFFE8F5E9),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text(
                            report.categoryName,
                            style: const TextStyle(
                              color: Color(0xFF1E5631),
                              fontWeight: FontWeight.bold,
                              fontSize: 13,
                            ),
                          ),
                        ),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                          decoration: BoxDecoration(
                            color: statusColor.withOpacity(0.12),
                            borderRadius: BorderRadius.circular(14),
                            border: Border.all(color: statusColor.withOpacity(0.5)),
                          ),
                          child: Text(
                            report.status.toUpperCase(),
                            style: TextStyle(
                              color: statusColor,
                              fontWeight: FontWeight.bold,
                              fontSize: 12,
                              letterSpacing: 0.5,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 16),

                    // Item Title
                    Text(
                      report.itemTypeName.isNotEmpty ? report.itemTypeName : report.categoryName,
                      style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                            fontWeight: FontWeight.bold,
                          ),
                    ),
                    const SizedBox(height: 12),

                    // Location Card
                    Card(
                      elevation: 0,
                      color: Theme.of(context).colorScheme.surfaceContainerHighest.withOpacity(0.4),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Row(
                          children: [
                            const CircleAvatar(
                              backgroundColor: Color(0xFF2E7D32),
                              radius: 18,
                              child: Icon(Icons.location_on, color: Colors.white, size: 20),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  const Text('Last-Seen Location', style: TextStyle(fontSize: 11, color: Colors.grey)),
                                  Text(
                                    report.lastSeenLocationName,
                                    style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(height: 12),

                    // Time Window Card
                    Card(
                      elevation: 0,
                      color: Theme.of(context).colorScheme.surfaceContainerHighest.withOpacity(0.4),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Row(
                              children: [
                                Icon(Icons.access_time_filled, color: Color(0xFF2E7D32), size: 18),
                                SizedBox(width: 8),
                                Text('Estimated Lost Time Range', style: TextStyle(fontSize: 12, color: Colors.grey, fontWeight: FontWeight.w600)),
                              ],
                            ),
                            const SizedBox(height: 8),
                            Text(
                              'From: ${dateFormat.format(report.estimatedLostFromAt.toLocal())}',
                              style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
                            ),
                            const SizedBox(height: 2),
                            Text(
                              'To:     ${dateFormat.format(report.estimatedLostToAt.toLocal())}',
                              style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
                            ),
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(height: 16),

                    // Description Section
                    const Text('Description', style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold)),
                    const SizedBox(height: 6),
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(14),
                      decoration: BoxDecoration(
                        color: Theme.of(context).colorScheme.surface,
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: Colors.grey[300]!),
                      ),
                      child: Text(
                        report.description,
                        style: const TextStyle(fontSize: 14, height: 1.4),
                      ),
                    ),
                    const SizedBox(height: 16),

                    // Color Tags
                    if (report.primaryColor != null || report.secondaryColor != null) ...[
                      const Text('Colors / Attributes', style: TextStyle(fontSize: 14, fontWeight: FontWeight.bold)),
                      const SizedBox(height: 8),
                      Wrap(
                        spacing: 8,
                        children: [
                          if (report.primaryColor != null && report.primaryColor!.isNotEmpty)
                            Chip(
                              avatar: const Icon(Icons.palette, size: 16, color: Color(0xFF2E7D32)),
                              label: Text('Primary: ${report.primaryColor}'),
                              backgroundColor: Colors.white,
                              side: BorderSide(color: Colors.grey[300]!),
                            ),
                          if (report.secondaryColor != null && report.secondaryColor!.isNotEmpty)
                            Chip(
                              avatar: const Icon(Icons.color_lens, size: 16, color: Colors.teal),
                              label: Text('Secondary: ${report.secondaryColor}'),
                              backgroundColor: Colors.white,
                              side: BorderSide(color: Colors.grey[300]!),
                            ),
                        ],
                      ),
                      const SizedBox(height: 16),
                    ],

                    // Withdrawal info if withdrawn
                    if (isWithdrawn) ...[
                      Container(
                        padding: const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          color: Colors.amber[50],
                          borderRadius: BorderRadius.circular(12),
                          border: Border.all(color: Colors.amber[300]!),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Icon(Icons.info_outline, color: Colors.amber[900], size: 20),
                                const SizedBox(width: 8),
                                Text(
                                  'This report has been withdrawn',
                                  style: TextStyle(fontWeight: FontWeight.bold, color: Colors.amber[900]),
                                ),
                              ],
                            ),
                            if (report.withdrawReason != null && report.withdrawReason!.isNotEmpty) ...[
                              const SizedBox(height: 6),
                              Text(
                                'Reason: ${report.withdrawReason}',
                                style: TextStyle(fontSize: 13, color: Colors.amber[900]),
                              ),
                            ]
                          ],
                        ),
                      ),
                      const SizedBox(height: 16),
                    ],

                    // Post timestamp
                    Text(
                      'Posted on ${DateFormat('MMM d, yyyy').format(report.createdAt.toLocal())}',
                      style: const TextStyle(fontSize: 12, color: Colors.grey),
                    ),
                  ],
                ),
              ),

              // Bottom Action Bar
              Positioned(
                bottom: 0,
                left: 0,
                right: 0,
                child: Container(
                  padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
                  decoration: BoxDecoration(
                    color: Theme.of(context).colorScheme.surface,
                    boxShadow: [
                      BoxShadow(
                        color: Colors.black.withOpacity(0.08),
                        blurRadius: 10,
                        offset: const Offset(0, -3),
                      ),
                    ],
                  ),
                  child: Row(
                    children: [
                      // View Matches button
                      Expanded(
                        flex: 2,
                        child: ElevatedButton.icon(
                          onPressed: () => context.push('/reports/${report.id}/matches'),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: const Color(0xFF2E7D32),
                            foregroundColor: Colors.white,
                            padding: const EdgeInsets.symmetric(vertical: 14),
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                          ),
                          icon: const Icon(Icons.auto_awesome, size: 20),
                          label: const Text(
                            'Possible Matches',
                            style: TextStyle(fontWeight: FontWeight.bold),
                          ),
                        ),
                      ),
                      if (isActive) ...[
                        const SizedBox(width: 8),
                        IconButton.filledTonal(
                          onPressed: () => context.push('/reports/${report.id}/edit'),
                          icon: const Icon(Icons.edit_outlined),
                          tooltip: 'Edit Report',
                        ),
                        const SizedBox(width: 4),
                        IconButton.filledTonal(
                          style: IconButton.styleFrom(
                            foregroundColor: Colors.red[700],
                            backgroundColor: Colors.red[50],
                          ),
                          onPressed: controllerState.isLoading ? null : () => _showWithdrawDialog(context),
                          icon: const Icon(Icons.remove_circle_outline),
                          tooltip: 'Withdraw Report',
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
