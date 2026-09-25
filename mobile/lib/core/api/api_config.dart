import 'package:flutter/foundation.dart';

class ApiConfig {
  const ApiConfig._();

  static String get baseUrl {
    const configured = String.fromEnvironment('FOUND_U_API_BASE_URL');
    if (configured.isNotEmpty) return configured;

    return kIsWeb ? 'http://localhost:5292' : 'http://10.0.2.2:5292';
  }
}
