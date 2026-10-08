import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/claims/data/claim_models.dart';

void main() {
  final safeDetail = <String, dynamic>{
    'id': 'claim-1',
    'status': 'WaitingForAnswer',
    'lostReportId': 'lost-1',
    'lostReportDescription': 'Blue bag lost near library.',
    'foundItem': {
      'id': 'found-1',
      'categoryName': 'Bags',
      'itemTypeName': 'Backpack',
      'foundLocationName': 'Library',
      'generalDescription': 'Blue backpack',
      'primaryColor': 'Blue',
      'foundAt': '2026-09-20T10:00:00Z',
      'status': 'Unclaimed'
    },
    'questions': [
      {
        'id': 'question-1',
        'questionText': 'What identifying detail can you provide?',
        'answerText': null,
        'answeredAt': null
      }
    ],
    'decision': null,
    'decisionReason': null,
    'decidedAt': null,
    'createdAt': '2026-09-20T10:00:00Z',
    'updatedAt': '2026-09-20T10:00:00Z',
  };

  test('claim detail parses safe DTO fields and ignores unknown private fields',
      () {
    final json = {
      ...safeDetail,
      'privateVerificationDetails': 'secret',
      'expectedAnswer': 'secret'
    };
    final detail = ClaimDetail.fromJson(json);
    expect(detail.status, 'WaitingForAnswer');
    expect(
        detail.questions.single.questionText, contains('identifying detail'));
    expect(detail.foundItem.itemTypeName, 'Backpack');
  });

  test('status labels and available actions match supported backend statuses',
      () {
    expect(claimStatusLabel('ManualReviewRequired'), 'Staff review required');
    expect(claimCanAnswer('RevisionRequested'), isTrue);
    expect(claimCanAnswer('Approved'), isFalse);
    expect(claimCanCancel('Cancelled'), isFalse);
  });

  test('create and answer request serialization matches Claims API', () {
    expect(
        const CreateClaimRequest(
                lostReportId: 'lost-1', foundReportId: 'found-1')
            .toJson(),
        {'lostReportId': 'lost-1', 'foundReportId': 'found-1'});
    expect(
        const ClaimAnswerInput(
                questionId: 'question-1', answerText: 'blue keychain')
            .toJson(),
        {'questionId': 'question-1', 'answerText': 'blue keychain'});
  });
}
