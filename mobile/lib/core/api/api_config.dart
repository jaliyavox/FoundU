class ApiConfig {
  const ApiConfig._();

  static const baseUrl = String.fromEnvironment(
    'FOUND_U_API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5292',
  );
}
