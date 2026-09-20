class ItemTypeModel {
  final String id;
  final String categoryId;
  final String name;

  const ItemTypeModel({
    required this.id,
    required this.categoryId,
    required this.name,
  });

  factory ItemTypeModel.fromJson(Map<String, dynamic> json) {
    return ItemTypeModel(
      id: json['id'] as String,
      categoryId: json['categoryId'] as String? ?? '',
      name: json['name'] as String,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'categoryId': categoryId,
        'name': name,
      };
}

class CategoryModel {
  final String id;
  final String name;
  final String? description;
  final bool isHighlighted;
  final List<ItemTypeModel> itemTypes;

  const CategoryModel({
    required this.id,
    required this.name,
    this.description,
    required this.isHighlighted,
    required this.itemTypes,
  });

  factory CategoryModel.fromJson(Map<String, dynamic> json) {
    final typesJson = json['itemTypes'] as List<dynamic>? ?? [];
    return CategoryModel(
      id: json['id'] as String,
      name: json['name'] as String,
      description: json['description'] as String?,
      isHighlighted: json['isHighlighted'] as bool? ?? false,
      itemTypes: typesJson
          .map((e) => ItemTypeModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'name': name,
        'description': description,
        'isHighlighted': isHighlighted,
        'itemTypes': itemTypes.map((e) => e.toJson()).toList(),
      };
}

class CampusLocationModel {
  final String id;
  final String name;
  final String? building;
  final String? description;

  const CampusLocationModel({
    required this.id,
    required this.name,
    this.building,
    this.description,
  });

  factory CampusLocationModel.fromJson(Map<String, dynamic> json) {
    return CampusLocationModel(
      id: json['id'] as String,
      name: json['name'] as String,
      building: json['building'] as String?,
      description: json['description'] as String?,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'name': name,
        'building': building,
        'description': description,
      };
}
