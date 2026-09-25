import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../data/claim_models.dart';
import 'providers/claim_providers.dart';

Widget claimSubmissionRoutePage(Uri uri) {
  final lostReportId = uri.queryParameters['lostReportId']?.trim();
  final foundReportId = uri.queryParameters['foundReportId']?.trim();
  if (lostReportId == null ||
      lostReportId.isEmpty ||
      foundReportId == null ||
      foundReportId.isEmpty) {
    return const InvalidClaimSubmissionRoutePage();
  }
  return ClaimSubmissionPage(
    lostReportId: lostReportId,
    foundReportId: foundReportId,
  );
}

class InvalidClaimSubmissionRoutePage extends StatelessWidget {
  const InvalidClaimSubmissionRoutePage({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Submit claim')),
        body: const Center(
          child: Padding(
            padding: EdgeInsets.all(24),
            child: Text(
              'This claim link is incomplete. Return to the possible match and try again.',
              textAlign: TextAlign.center,
            ),
          ),
        ),
      );
}

class ClaimSubmissionPage extends ConsumerWidget {
  const ClaimSubmissionPage(
      {super.key, required this.lostReportId, required this.foundReportId});
  final String lostReportId;
  final String foundReportId;
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final busy = ref.watch(claimControllerProvider).isLoading;
    return Scaffold(
        appBar: AppBar(title: const Text('Submit claim')),
        body: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Text(
                      'Submit a claim for this possible match. Staff may ask ownership-verification questions before making a decision.'),
                  const SizedBox(height: 20),
                  FilledButton(
                      onPressed: busy
                          ? null
                          : () async {
                              try {
                                final claim = await ref
                                    .read(claimControllerProvider.notifier)
                                    .create(CreateClaimRequest(
                                        lostReportId: lostReportId,
                                        foundReportId: foundReportId));
                                if (context.mounted) {
                                  context.go('/claims/${claim.id}');
                                }
                              } catch (_) {
                                if (context.mounted) {
                                  ScaffoldMessenger.of(context).showSnackBar(
                                      const SnackBar(
                                          content: Text(
                                              'Could not submit claim. Please try again.')));
                                }
                              }
                            },
                      child: busy
                          ? const CircularProgressIndicator()
                          : const Text('Submit claim'))
                ])));
  }
}
