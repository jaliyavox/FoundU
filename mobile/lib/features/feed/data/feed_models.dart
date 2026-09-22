/// Mirrors LostReportFeedItemDto: the public projection of a lost report. It carries the
/// poster's display name and nothing else identifying - no email, no student number, no
/// user id. `isMine` is computed by the API so the client can hide "I found this" on your
/// own post without the payload saying who "you" are.
class FeedItem {
  const FeedItem({
    required this.id,
    required this.postedByName,
    required this.isMine,
    required this.categoryName,
    required this.itemTypeName,
    required this.lastSeenLocationName,
    required this.description,
    required this.primaryColor,
    required this.estimatedLostFromAt,
    required this.estimatedLostToAt,
    required this.photoUrls,
    required this.createdAt,
  });

  final String id;
  final String postedByName;
  final bool isMine;
  final String categoryName;
  final String itemTypeName;
  final String lastSeenLocationName;
  final String description;
  final String? primaryColor;
  final DateTime estimatedLostFromAt;
  final DateTime estimatedLostToAt;
  final List<String> photoUrls;
  final DateTime createdAt;

  String? get photoUrl => photoUrls.isEmpty ? null : photoUrls.first;

  factory FeedItem.fromJson(Map<String, dynamic> json) => FeedItem(
        id: json['id'] as String,
        postedByName: json['postedByName'] as String,
        isMine: json['isMine'] as bool? ?? false,
        categoryName: json['categoryName'] as String,
        itemTypeName: json['itemTypeName'] as String,
        lastSeenLocationName: json['lastSeenLocationName'] as String,
        description: json['description'] as String,
        primaryColor: json['primaryColor'] as String?,
        estimatedLostFromAt: DateTime.parse(json['estimatedLostFromAt'] as String),
        estimatedLostToAt: DateTime.parse(json['estimatedLostToAt'] as String),
        photoUrls: (json['photoUrls'] as List<dynamic>? ?? const []).cast<String>(),
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}

class FeedPage {
  const FeedPage({required this.items, required this.page, required this.totalCount, required this.hasNextPage});

  final List<FeedItem> items;
  final int page;
  final int totalCount;
  final bool hasNextPage;

  factory FeedPage.fromJson(Map<String, dynamic> json) => FeedPage(
        items: (json['items'] as List<dynamic>).map((e) => FeedItem.fromJson(e as Map<String, dynamic>)).toList(),
        page: json['page'] as int,
        totalCount: json['totalCount'] as int,
        hasNextPage: json['hasNextPage'] as bool,
      );
}

/// Mirrors FoundU.Application.LostReports.Dtos.LostReportFoundClaimDto.
class FoundClaimResult {
  const FoundClaimResult({required this.totalFinders});
  final int totalFinders;

  factory FoundClaimResult.fromJson(Map<String, dynamic> json) =>
      FoundClaimResult(totalFinders: json['totalFinders'] as int);
}
