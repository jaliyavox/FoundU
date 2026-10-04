import 'package:flutter/material.dart';

/// Where a lost report sits in its life - the same four stages, read off the same data, as the
/// web's `report-stage.ts`. Withdrawn is off this path, so callers hide the track for it.
///
///  - Reported           the report exists and is Active
///  - Someone found it   a finder pressed "I found this" or wrote to the owner
///  - At the guard desk  the report is Matched: the item is in storage
///  - Returned           the report is Resolved
const reportStages = ['Reported', 'Someone found it', 'At the guard desk', 'Returned'];

int reportStageOf(String status, {int foundClaimCount = 0, int messageCount = 0}) {
  switch (status.toLowerCase()) {
    case 'resolved':
      return 3;
    case 'matched':
      return 2;
    default:
      return foundClaimCount > 0 || messageCount > 0 ? 1 : 0;
  }
}

/// Four dots joined by a line, each dot centred over its own label, so the last stage sits at
/// the end of the bar like the first sits at its start.
class ReportStageTrack extends StatelessWidget {
  const ReportStageTrack({super.key, required this.stage, this.compact = false});

  final int stage;
  final bool compact;

  static const _reached = Color(0xFF2E7D32);
  static const _current = Color(0xFF1E5631);

  @override
  Widget build(BuildContext context) {
    final dot = compact ? 8.0 : 10.0;
    final currentDot = compact ? 12.0 : 14.0;
    final pending = Colors.grey[300]!;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (var index = 0; index < reportStages.length; index++)
          Expanded(
            child: Column(
              children: [
                SizedBox(
                  height: currentDot,
                  child: Row(
                    children: [
                      // Half a line on each side of the dot; the outer halves of the first and
                      // last stage stay empty, so the line runs dot to dot.
                      Expanded(
                        child: Container(
                          height: 2,
                          color: index == 0 ? Colors.transparent : (index <= stage ? _reached : pending),
                        ),
                      ),
                      Container(
                        width: index == stage ? currentDot : dot,
                        height: index == stage ? currentDot : dot,
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          color: index <= stage ? _reached : pending,
                          border: index == stage ? Border.all(color: _current, width: 2) : null,
                        ),
                      ),
                      Expanded(
                        child: Container(
                          height: 2,
                          color: index == reportStages.length - 1
                              ? Colors.transparent
                              : (index < stage ? _reached : pending),
                        ),
                      ),
                    ],
                  ),
                ),
                SizedBox(height: compact ? 4 : 6),
                Text(
                  reportStages[index],
                  textAlign: TextAlign.center,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: compact ? 10 : 11,
                    height: 1.2,
                    fontWeight: index == stage ? FontWeight.bold : FontWeight.normal,
                    color: index <= stage ? _reached : Colors.grey,
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}
