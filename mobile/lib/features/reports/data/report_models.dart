class LostReportListItemModel {
  final String id;
  final String categoryName;
  final String itemTypeName;
  final String lastSeenLocationName;
  final String description;
  final String? primaryColor;
  final DateTime estimatedLostFromAt;
  final DateTime estimatedLostToAt;
  final String status;
  final List<String> photoUrls;
  final int messageCount;
  final int foundClaimCount;
  final DateTime? lastFoundClaimAt;
  final bool isFlagged;
  final String? flagReason;
  final DateTime? flaggedAt;
  final String? flaggedByName;
  final DateTime createdAt;

  const LostReportListItemModel({
    required this.id,
    required this.categoryName,
    required this.itemTypeName,
    required this.lastSeenLocationName,
    required this.description,
    this.primaryColor,
    required this.estimatedLostFromAt,
    required this.estimatedLostToAt,
    required this.status,
    required this.photoUrls,
    required this.messageCount,
    required this.foundClaimCount,
    this.lastFoundClaimAt,
    required this.isFlagged,
    this.flagReason,
    this.flaggedAt,
    this.flaggedByName,
    required this.createdAt,
  });

  factory LostReportListItemModel.fromJson(Map<String, dynamic> json) {
    return LostReportListItemModel(
      id: json['id'] as String,
      categoryName: json['categoryName'] as String? ?? '',
      itemTypeName: json['itemTypeName'] as String? ?? '',
      lastSeenLocationName: json['lastSeenLocationName'] as String? ?? '',
      description: json['description'] as String? ?? '',
      primaryColor: json['primaryColor'] as String?,
      estimatedLostFromAt: DateTime.parse(json['estimatedLostFromAt'] as String),
      estimatedLostToAt: DateTime.parse(json['estimatedLostToAt'] as String),
      status: json['status'] as String? ?? 'Active',
      photoUrls: (json['photoUrls'] as List<dynamic>?)?.map((e) => e.toString()).toList() ?? [],
      messageCount: json['messageCount'] as int? ?? 0,
      foundClaimCount: json['foundClaimCount'] as int? ?? 0,
      lastFoundClaimAt: json['lastFoundClaimAt'] != null
          ? DateTime.parse(json['lastFoundClaimAt'] as String)
          : null,
      isFlagged: json['isFlagged'] as bool? ?? false,
      flagReason: json['flagReason'] as String?,
      flaggedAt: json['flaggedAt'] != null
          ? DateTime.parse(json['flaggedAt'] as String)
          : null,
      flaggedByName: json['flaggedByName'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}

class LostReportPhotoModel {
  final String id;
  final String url;

  const LostReportPhotoModel({required this.id, required this.url});

  factory LostReportPhotoModel.fromJson(Map<String, dynamic> json) {
    return LostReportPhotoModel(
      id: json['id'] as String,
      url: json['url'] as String,
    );
  }
}

class LostReportDetailModel {
  final String id;
  final String categoryId;
  final String categoryName;
  final String itemTypeId;
  final String itemTypeName;
  final String lastSeenLocationId;
  final String lastSeenLocationName;
  final String description;
  final String? primaryColor;
  final String? secondaryColor;
  final DateTime estimatedLostFromAt;
  final DateTime estimatedLostToAt;
  final String status;
  final String? withdrawReason;
  final DateTime? withdrawnAt;
  final String studentId;
  final String studentName;
  final String? parsedAttributesJson;
  final List<LostReportPhotoModel> photos;
  final bool isFlagged;
  final String? flagReason;
  final DateTime createdAt;
  final DateTime updatedAt;

  const LostReportDetailModel({
    required this.id,
    required this.categoryId,
    required this.categoryName,
    required this.itemTypeId,
    required this.itemTypeName,
    required this.lastSeenLocationId,
    required this.lastSeenLocationName,
    required this.description,
    this.primaryColor,
    this.secondaryColor,
    required this.estimatedLostFromAt,
    required this.estimatedLostToAt,
    required this.status,
    this.withdrawReason,
    this.withdrawnAt,
    required this.studentId,
    required this.studentName,
    this.parsedAttributesJson,
    required this.photos,
    required this.isFlagged,
    this.flagReason,
    required this.createdAt,
    required this.updatedAt,
  });

  factory LostReportDetailModel.fromJson(Map<String, dynamic> json) {
    final photosList = json['photos'] as List<dynamic>? ?? [];
    return LostReportDetailModel(
      id: json['id'] as String,
      categoryId: json['categoryId'] as String? ?? '',
      categoryName: json['categoryName'] as String? ?? '',
      itemTypeId: json['itemTypeId'] as String? ?? '',
      itemTypeName: json['itemTypeName'] as String? ?? '',
      lastSeenLocationId: json['lastSeenLocationId'] as String? ?? '',
      lastSeenLocationName: json['lastSeenLocationName'] as String? ?? '',
      description: json['description'] as String? ?? '',
      primaryColor: json['primaryColor'] as String?,
      secondaryColor: json['secondaryColor'] as String?,
      estimatedLostFromAt: DateTime.parse(json['estimatedLostFromAt'] as String),
      estimatedLostToAt: DateTime.parse(json['estimatedLostToAt'] as String),
      status: json['status'] as String? ?? 'Active',
      withdrawReason: json['withdrawReason'] as String?,
      withdrawnAt: json['withdrawnAt'] != null
          ? DateTime.parse(json['withdrawnAt'] as String)
          : null,
      studentId: json['studentId'] as String? ?? '',
      studentName: json['studentName'] as String? ?? '',
      parsedAttributesJson: json['parsedAttributesJson'] as String?,
      photos: photosList
          .map((e) => LostReportPhotoModel.fromJson(e as Map<String, dynamic>))
          .toList(),
      isFlagged: json['isFlagged'] as bool? ?? false,
      flagReason: json['flagReason'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: DateTime.parse(json['updatedAt'] as String),
    );
  }
}

class CreateLostReportRequest {
  final String categoryId;
  final String itemTypeId;
  final String lastSeenLocationId;
  final String description;
  final String? primaryColor;
  final String? secondaryColor;
  final DateTime estimatedLostFromAt;
  final DateTime estimatedLostToAt;

  const CreateLostReportRequest({
    required this.categoryId,
    required this.itemTypeId,
    required this.lastSeenLocationId,
    required this.description,
    this.primaryColor,
    this.secondaryColor,
    required this.estimatedLostFromAt,
    required this.estimatedLostToAt,
  });

  Map<String, dynamic> toJson() => {
        'categoryId': categoryId,
        'itemTypeId': itemTypeId,
        'lastSeenLocationId': lastSeenLocationId,
        'description': description,
        'primaryColor': primaryColor,
        'secondaryColor': secondaryColor,
        'estimatedLostFromAt': estimatedLostFromAt.toUtc().toIso8601String(),
        'estimatedLostToAt': estimatedLostToAt.toUtc().toIso8601String(),
      };
}

class UpdateLostReportRequest {
  final String categoryId;
  final String itemTypeId;
  final String lastSeenLocationId;
  final String description;
  final String? primaryColor;
  final String? secondaryColor;
  final DateTime estimatedLostFromAt;
  final DateTime estimatedLostToAt;

  const UpdateLostReportRequest({
    required this.categoryId,
    required this.itemTypeId,
    required this.lastSeenLocationId,
    required this.description,
    this.primaryColor,
    this.secondaryColor,
    required this.estimatedLostFromAt,
    required this.estimatedLostToAt,
  });

  Map<String, dynamic> toJson() => {
        'categoryId': categoryId,
        'itemTypeId': itemTypeId,
        'lastSeenLocationId': lastSeenLocationId,
        'description': description,
        'primaryColor': primaryColor,
        'secondaryColor': secondaryColor,
        'estimatedLostFromAt': estimatedLostFromAt.toUtc().toIso8601String(),
        'estimatedLostToAt': estimatedLostToAt.toUtc().toIso8601String(),
      };
}

class FoundReportSummaryModel {
  final String id;
  final String categoryName;
  final String itemTypeName;
  final String foundLocationName;
  final String generalDescription;
  final String? primaryColor;
  final DateTime foundAt;
  final String status;

  const FoundReportSummaryModel({
    required this.id,
    required this.categoryName,
    required this.itemTypeName,
    required this.foundLocationName,
    required this.generalDescription,
    this.primaryColor,
    required this.foundAt,
    required this.status,
  });

  factory FoundReportSummaryModel.fromJson(Map<String, dynamic> json) {
    return FoundReportSummaryModel(
      id: json['id'] as String,
      categoryName: json['categoryName'] as String? ?? '',
      itemTypeName: json['itemTypeName'] as String? ?? '',
      foundLocationName: json['foundLocationName'] as String? ?? '',
      generalDescription: json['generalDescription'] as String? ?? '',
      primaryColor: json['primaryColor'] as String?,
      foundAt: DateTime.parse(json['foundAt'] as String),
      status: json['status'] as String? ?? 'Unclaimed',
    );
  }
}

class MatchSuggestionModel {
  final String id;
  final String lostReportId;
  final String lostReportDescription;
  final FoundReportSummaryModel foundItem;
  final String status;
  final String? note;
  final bool isAgentGenerated;
  final double? matchScore;
  final String? claimId;
  final DateTime createdAt;

  const MatchSuggestionModel({
    required this.id,
    required this.lostReportId,
    required this.lostReportDescription,
    required this.foundItem,
    required this.status,
    this.note,
    required this.isAgentGenerated,
    this.matchScore,
    this.claimId,
    required this.createdAt,
  });

  factory MatchSuggestionModel.fromJson(Map<String, dynamic> json) {
    return MatchSuggestionModel(
      id: json['id'] as String,
      lostReportId: json['lostReportId'] as String? ?? '',
      lostReportDescription: json['lostReportDescription'] as String? ?? '',
      foundItem: FoundReportSummaryModel.fromJson(
          json['foundItem'] as Map<String, dynamic>),
      status: json['status'] as String? ?? 'Pending',
      note: json['note'] as String?,
      isAgentGenerated: json['isAgentGenerated'] as bool? ?? false,
      matchScore: (json['matchScore'] as num?)?.toDouble(),
      claimId: json['claimId'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}

class PagedResult<T> {
  final List<T> items;
  final int totalCount;
  final int page;
  final int pageSize;
  final int totalPages;
  final bool hasPreviousPage;
  final bool hasNextPage;

  const PagedResult({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
    required this.totalPages,
    required this.hasPreviousPage,
    required this.hasNextPage,
  });

  factory PagedResult.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) fromJsonT,
  ) {
    final itemsList = json['items'] as List<dynamic>? ?? [];
    return PagedResult(
      items: itemsList.map((e) => fromJsonT(e as Map<String, dynamic>)).toList(),
      totalCount: json['totalCount'] as int? ?? 0,
      page: json['page'] as int? ?? 1,
      pageSize: json['pageSize'] as int? ?? 10,
      totalPages: json['totalPages'] as int? ?? 1,
      hasPreviousPage: json['hasPreviousPage'] as bool? ?? false,
      hasNextPage: json['hasNextPage'] as bool? ?? false,
    );
  }
}
