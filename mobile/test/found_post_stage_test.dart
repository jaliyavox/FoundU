import 'package:flutter_test/flutter_test.dart';
import 'package:foundu/features/feed/data/feed_models.dart';

FoundPost post(String status, {bool mine = false}) => FoundPost(
      id: 'p1',
      postedByName: 'Dev Fernando',
      isMine: mine,
      categoryName: 'Keys',
      itemTypeName: 'House Keys',
      foundLocationName: 'Cafeteria',
      description: 'Keys on a blue lanyard',
      primaryColor: 'Blue',
      foundAt: DateTime(2026, 10, 2),
      status: status,
      handInCode: null,
      createdAt: DateTime(2026, 10, 2),
    );

void main() {
  test('a found post says where it has got to, until the owner collects it', () {
    expect(post('Posted').stageLabel, 'Not at a desk yet');
    expect(post('Posted', mine: true).stageLabel, 'Your post');
    expect(post('Unclaimed').stageLabel, 'At the security desk');
    expect(post('Unclaimed', mine: true).stageLabel, 'At the security desk');
    expect(post('Claimed').stageLabel, 'Owner on the way');
    expect(post('Claimed').isSpokenFor, isTrue);
  });
}
