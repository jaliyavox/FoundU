class ClaimListItem {
  const ClaimListItem({
    required this.id,
    required this.status,
    required this.categoryName,
    required this.itemTypeName,
    required this.unansweredQuestionCount,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String status;
  final String categoryName;
  final String itemTypeName;
  final int unansweredQuestionCount;
  final DateTime createdAt;
  final DateTime updatedAt;

  factory ClaimListItem.fromJson(Map<String, dynamic> json) => ClaimListItem(
        id: json['id'] as String,
        status: json['status'] as String? ?? 'Pending',
        categoryName: json['categoryName'] as String? ?? '',
        itemTypeName: json['itemTypeName'] as String? ?? '',
        unansweredQuestionCount: json['unansweredQuestionCount'] as int? ?? 0,
        createdAt: DateTime.parse(json['createdAt'] as String),
        updatedAt: DateTime.parse(json['updatedAt'] as String),
      );
}

class FoundItemSummary {
  const FoundItemSummary({
    required this.id,
    required this.categoryName,
    required this.itemTypeName,
    required this.foundLocationName,
    required this.generalDescription,
    this.primaryColor,
    required this.foundAt,
    required this.status,
  });

  final String id;
  final String categoryName;
  final String itemTypeName;
  final String foundLocationName;
  final String generalDescription;
  final String? primaryColor;
  final DateTime foundAt;
  final String status;

  factory FoundItemSummary.fromJson(Map<String, dynamic> json) =>
      FoundItemSummary(
        id: json['id'] as String,
        categoryName: json['categoryName'] as String? ?? '',
        itemTypeName: json['itemTypeName'] as String? ?? '',
        foundLocationName: json['foundLocationName'] as String? ?? '',
        generalDescription: json['generalDescription'] as String? ?? '',
        primaryColor: json['primaryColor'] as String?,
        foundAt: DateTime.parse(json['foundAt'] as String),
        status: json['status'] as String? ?? '',
      );
}

class ClaimQuestion {
  const ClaimQuestion(
      {required this.id,
      required this.questionText,
      this.answerText,
      this.answeredAt});

  final String id;
  final String questionText;
  final String? answerText;
  final DateTime? answeredAt;

  factory ClaimQuestion.fromJson(Map<String, dynamic> json) => ClaimQuestion(
        id: json['id'] as String,
        questionText: json['questionText'] as String? ?? '',
        answerText: json['answerText'] as String?,
        answeredAt: json['answeredAt'] == null
            ? null
            : DateTime.parse(json['answeredAt'] as String),
      );
}

class ClaimDetail {
  const ClaimDetail({
    required this.id,
    required this.status,
    required this.lostReportId,
    required this.lostReportDescription,
    required this.foundItem,
    required this.questions,
    this.decision,
    this.decisionReason,
    this.decidedAt,
    this.collectionCode,
    this.collectedAt,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String status;

  /// Six digits the owner quotes at the desk. Only the owner ever receives it.
  final String? collectionCode;
  final DateTime? collectedAt;
  final String lostReportId;
  final String lostReportDescription;
  final FoundItemSummary foundItem;
  final List<ClaimQuestion> questions;
  final String? decision;
  final String? decisionReason;
  final DateTime? decidedAt;
  final DateTime createdAt;
  final DateTime updatedAt;

  factory ClaimDetail.fromJson(Map<String, dynamic> json) => ClaimDetail(
        id: json['id'] as String,
        status: json['status'] as String? ?? 'Pending',
        lostReportId: json['lostReportId'] as String? ?? '',
        lostReportDescription: json['lostReportDescription'] as String? ?? '',
        foundItem: FoundItemSummary.fromJson(
            json['foundItem'] as Map<String, dynamic>),
        questions: (json['questions'] as List<dynamic>? ?? [])
            .map((item) => ClaimQuestion.fromJson(item as Map<String, dynamic>))
            .toList(),
        decision: json['decision'] as String?,
        decisionReason: json['decisionReason'] as String?,
        decidedAt: json['decidedAt'] == null
            ? null
            : DateTime.parse(json['decidedAt'] as String),
        collectionCode: json['collectionCode'] as String?,
        collectedAt: json['collectedAt'] == null
            ? null
            : DateTime.parse(json['collectedAt'] as String),
        createdAt: DateTime.parse(json['createdAt'] as String),
        updatedAt: DateTime.parse(json['updatedAt'] as String),
      );
}

class PagedClaims {
  const PagedClaims(
      {required this.items,
      required this.page,
      required this.totalPages,
      required this.totalCount});
  final List<ClaimListItem> items;
  final int page;
  final int totalPages;
  final int totalCount;

  factory PagedClaims.fromJson(Map<String, dynamic> json) => PagedClaims(
        items: (json['items'] as List<dynamic>? ?? [])
            .map((item) => ClaimListItem.fromJson(item as Map<String, dynamic>))
            .toList(),
        page: json['page'] as int? ?? 1,
        totalPages: json['totalPages'] as int? ?? 1,
        totalCount: json['totalCount'] as int? ?? 0,
      );
}

class CreateClaimRequest {
  const CreateClaimRequest(
      {required this.lostReportId, required this.foundReportId});
  final String lostReportId;
  final String foundReportId;
  Map<String, dynamic> toJson() =>
      {'lostReportId': lostReportId, 'foundReportId': foundReportId};
}

class ClaimAnswerInput {
  const ClaimAnswerInput({required this.questionId, required this.answerText});
  final String questionId;
  final String answerText;
  Map<String, dynamic> toJson() =>
      {'questionId': questionId, 'answerText': answerText};
}

String claimStatusLabel(String status) => switch (status) {
      'Pending' => 'Claim submitted',
      'WaitingForAnswer' => 'Verification questions available',
      'UnderReview' => 'Under review',
      'RevisionRequested' => 'More information requested',
      'ManualReviewRequired' => 'Staff review required',
      'Approved' => 'Approved',
      'Rejected' => 'Not approved',
      'Cancelled' => 'Cancelled',
      _ => status,
    };

bool claimCanAnswer(String status) =>
    status == 'WaitingForAnswer' || status == 'RevisionRequested';
bool claimCanCancel(String status) => const {
      'Pending',
      'WaitingForAnswer',
      'UnderReview',
      'RevisionRequested',
      'ManualReviewRequired'
    }.contains(status);
