# FoundU — Mobile (Flutter)

The Flutter student app uses Dio, Riverpod, secure token storage, and `go_router` to submit lost
reports, view possible matches, and create/respond to claims through the ASP.NET API.

## Local run

```bash
cd mobile
flutter pub get
flutter analyze
flutter test
flutter run --dart-define=FOUND_U_API_BASE_URL=http://10.0.2.2:5292
```

`10.0.2.2` is the Android emulator's route to the API running on the development machine. The
app calls ASP.NET only; it never receives the internal FastAPI service key.
