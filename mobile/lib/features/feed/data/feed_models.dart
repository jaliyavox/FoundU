/// Mirrors LostReportFeedItemDto: the public projection of a lost report. It carries the
/// poster's display name and nothing else identifying - no email, no student number, no
/// user id. `isMine` is computed by the API so the client can hide "I found this" on your
/// own post without the payload saying who "you" are.
class FeedItem {
  const FeedItem({
    required this.id,
    required this.handInCode,
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
  /// Six digits a finder quotes at the desk. Routing, not proof - which is why it is public.
  final String handInCode;
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
        handInCode: json['handInCode'] as String? ?? '',
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

/// "483921" reads as "483 921" on screen.
String displayCode(String code) => code.length == 6 ? '${code.substring(0, 3)} ${code.substring(3)}' : code;

/// Mirrors FoundPostFeedItemDto - a finder's post before it reaches a desk. A teaser: what,
/// where, when, in the finder's words, and nothing that proves ownership.
class FoundPost {
  const FoundPost({
    required this.id,
    required this.postedByName,
    required this.isMine,
    required this.categoryName,
    required this.itemTypeName,
    required this.foundLocationName,
    required this.description,
    required this.primaryColor,
    required this.foundAt,
    required this.status,
    required this.handInCode,
    required this.createdAt,
  });

  final String id;
  final String postedByName;
  final bool isMine;
  final String categoryName;
  final String itemTypeName;
  final String foundLocationName;
  final String description;
  final String? primaryColor;
  final DateTime foundAt;
  final String status;
  /// Only on your own post - what you quote at the desk.
  final String? handInCode;
  final DateTime createdAt;

  factory FoundPost.fromJson(Map<String, dynamic> json) => FoundPost(
        id: json['id'] as String,
        postedByName: json['postedByName'] as String,
        isMine: json['isMine'] as bool? ?? false,
        categoryName: json['categoryName'] as String,
        itemTypeName: json['itemTypeName'] as String,
        foundLocationName: json['foundLocationName'] as String,
        description: json['description'] as String,
        primaryColor: json['primaryColor'] as String?,
        foundAt: DateTime.parse(json['foundAt'] as String),
        status: json['status'] as String,
        handInCode: json['handInCode'] as String?,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}

class FoundPostPage {
  const FoundPostPage({required this.items, required this.page, required this.totalCount, required this.hasNextPage});
  final List<FoundPost> items;
  final int page;
  final int totalCount;
  final bool hasNextPage;

  factory FoundPostPage.fromJson(Map<String, dynamic> json) => FoundPostPage(
        items: (json['items'] as List<dynamic>).map((e) => FoundPost.fromJson(e as Map<String, dynamic>)).toList(),
        page: json['page'] as int,
        totalCount: json['totalCount'] as int,
        hasNextPage: json['hasNextPage'] as bool,
      );
}
