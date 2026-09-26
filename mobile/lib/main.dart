import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app/app.dart';

@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  // The payload is deliberately not trusted as business data. The app fetches any selected
  // claim/report through the authorized FoundU API when it is opened.
  await Firebase.initializeApp();
}

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  try {
    await Firebase.initializeApp();
    FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);
  } on FirebaseException {
    // Firebase configuration is optional for local development; FoundU's in-app notifications
    // continue to work while push registration remains unavailable.
  }
  runApp(const ProviderScope(child: FoundUApp()));
}
