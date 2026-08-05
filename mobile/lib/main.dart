import 'package:flutter/material.dart';

void main() => runApp(const FoundUApp());

/// Placeholder root widget for the FoundU mobile app.
///
/// The real app shell (go_router, Riverpod, Dio, secure storage, login) is
/// built in the mobile phase (Track B / Step 4b of the build plan).
class FoundUApp extends StatelessWidget {
  const FoundUApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'FoundU',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF2E7D32)),
        useMaterial3: true,
      ),
      home: const _PlaceholderHome(),
    );
  }
}

class _PlaceholderHome extends StatelessWidget {
  const _PlaceholderHome();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('FoundU')),
      body: const Center(
        child: Text('FoundU mobile — skeleton'),
      ),
    );
  }
}
