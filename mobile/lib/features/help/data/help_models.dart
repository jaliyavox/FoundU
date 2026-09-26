/// One thing a person did to help someone else get their things back.
///
/// `handInCode` is the six digits to quote at a desk - the owner's own public code, or the
/// code on their own post - and it is null once the item has reached one. No private
/// verification detail and no collection code ever appears here.
class HelpActivity {
  const HelpActivity({
    required this.kind,
    required this.reportId,
    required this.itemTypeName,
    required this.locationName,
    required this.status,
    required this.handInCode,
    required this.ownerName,
    required this.pointsEarned,
    required this.createdAt,
  });

  /// "found-claim" - they said they found someone's item; "found-post" - they posted one.
  final String kind;
  final String reportId;
  final String itemTypeName;
  final String locationName;
  final String status;
  final String? handInCode;
  final String? ownerName;
  final int pointsEarned;
  final DateTime createdAt;

  bool get isPost => kind == 'found-post';

  factory HelpActivity.fromJson(Map<String, dynamic> json) => HelpActivity(
        kind: json['kind'] as String,
        reportId: json['reportId'] as String,
        itemTypeName: json['itemTypeName'] as String,
        locationName: json['locationName'] as String,
        status: json['status'] as String,
        handInCode: json['handInCode'] as String?,
        ownerName: json['ownerName'] as String?,
        pointsEarned: json['pointsEarned'] as int? ?? 0,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}

class HelpToFind {
  const HelpToFind({
    required this.honorPoints,
    required this.itemsReturned,
    required this.handIns,
    required this.openHelpOffers,
    required this.activity,
  });

  final int honorPoints;
  final int itemsReturned;
  final int handIns;
  final int openHelpOffers;
  final List<HelpActivity> activity;

  factory HelpToFind.fromJson(Map<String, dynamic> json) => HelpToFind(
        honorPoints: json['honorPoints'] as int? ?? 0,
        itemsReturned: json['itemsReturned'] as int? ?? 0,
        handIns: json['handIns'] as int? ?? 0,
        openHelpOffers: json['openHelpOffers'] as int? ?? 0,
        activity: ((json['activity'] as List<dynamic>?) ?? const [])
            .map((e) => HelpActivity.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}
