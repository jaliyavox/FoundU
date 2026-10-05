import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/claims/data/claim_models.dart';
import 'package:foundu/features/claims/data/claim_repository.dart';
import 'package:foundu/features/claims/presentation/claim_detail_page.dart';
import 'package:foundu/features/claims/presentation/providers/claim_providers.dart';

void main() {
  testWidgets('shows safe item data and verification questions',
      (tester) async {
    await _pump(tester, _detail(status: 'WaitingForAnswer'));

    expect(find.text('Backpack'), findsOneWidget);
    expect(find.text('Found at Library'), findsOneWidget);
    expect(
        find.text('What identifying detail can you provide?'), findsOneWidget);
    expect(find.textContaining('private-secret'), findsNothing);
  });

  testWidgets('manual review and terminal statuses do not offer answers',
      (tester) async {
    await _pump(tester, _detail(status: 'ManualReviewRequired'));
    expect(
        find.textContaining('Your claim needs staff review'), findsOneWidget);
    expect(find.text('Submit answers'), findsNothing);

    await _pump(tester, _detail(status: 'Approved'));
    expect(find.text('Submit answers'), findsNothing);
    expect(find.text('Cancel claim'), findsNothing);
  });

  testWidgets('blank required answers cannot submit', (tester) async {
    final repository = _DetailRepository();
    await _pump(tester, _detail(status: 'WaitingForAnswer'), repository);

    await tester.tap(find.text('Submit answers'));
    await tester.pump();

    expect(find.text('Please answer every verification question.'),
        findsOneWidget);
    expect(repository.submissions, isEmpty);
  });

  testWidgets('successful and revision answer submission remain student review',
      (tester) async {
    final repository = _DetailRepository();
    await _pump(tester, _detail(status: 'RevisionRequested'), repository);

    expect(find.byType(TextField), findsOneWidget);
    await tester.enterText(find.byType(TextField), 'blue tag');
    await tester.tap(find.text('Submit answers'));
    await tester.pumpAndSettle();

    expect(repository.submissions.single.answerText, 'blue tag');
    expect(
        find.text('Your answers were submitted for review.'), findsOneWidget);
  });

  testWidgets('cancel action is limited to claimCanCancel statuses',
      (tester) async {
    await _pump(tester, _detail(status: 'Pending'));
    expect(find.text('Cancel claim'), findsOneWidget);

    await _pump(tester, _detail(status: 'Cancelled'));
    expect(find.text('Cancel claim'), findsNothing);
  });
}

Future<void> _pump(
  WidgetTester tester,
  ClaimDetail detail, [
  _DetailRepository? repository,
]) async {
  final repo = repository ?? _DetailRepository();
  await tester.pumpWidget(ProviderScope(
    key: ValueKey(detail.status),
    overrides: [
      claimRepositoryProvider.overrideWithValue(repo),
      claimDetailProvider('claim-1').overrideWith((ref) async => detail),
    ],
    child: const MaterialApp(home: ClaimDetailPage(claimId: 'claim-1')),
  ));
  await tester.pumpAndSettle();
}

ClaimDetail _detail({required String status}) => ClaimDetail(
      id: 'claim-1',
      status: status,
      lostReportId: 'lost-1',
      lostReportDescription: 'Lost backpack',
      foundItem: FoundItemSummary(
        id: 'found-1',
        categoryName: 'Bags',
        itemTypeName: 'Backpack',
        foundLocationName: 'Library',
        generalDescription: 'Blue backpack',
        foundAt: DateTime.utc(2026),
        status: 'Unclaimed',
      ),
      questions: const [
        ClaimQuestion(
          id: 'question-1',
          questionText: 'What identifying detail can you provide?',
        ),
      ],
      createdAt: DateTime.utc(2026),
      updatedAt: DateTime.utc(2026),
    );

class _DetailRepository extends ClaimRepository {
  _DetailRepository() : super(Dio());

  final List<ClaimAnswerInput> submissions = [];

  @override
  Future<ClaimDetail> submitAnswers(
      String claimId, List<ClaimAnswerInput> answers) async {
    submissions.addAll(answers);
    return _detail(status: 'UnderReview');
  }
}
