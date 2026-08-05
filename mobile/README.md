# FoundU — Mobile (Flutter)

Placeholder Flutter project. Flutter was not installed on the machine that created the
skeleton, so this is a minimal hand-written project valid enough for `flutter analyze`.

## When you pick up the mobile phase (Track B)

Install Flutter, then either keep this structure or regenerate platform folders:

```bash
flutter --version                 # confirm install
cd mobile
flutter create . --project-name foundu   # generates android/ ios/ etc.
flutter pub get
flutter analyze
flutter test
```

The real app shell (go_router, Riverpod, Dio auth interceptor, flutter_secure_storage,
login screen routing by role) is Step 4b of the build plan.
