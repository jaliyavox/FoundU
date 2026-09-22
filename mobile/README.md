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

## Running on macOS without Android Studio

What worked on a Mac with only the SDK folder (`~/Library/Android/sdk`), no Android Studio,
and no Xcode. Two things trip it up; both are one-off fixes.

**1. The JDK.** The Android Gradle plugin's `jlink` step fails on very new JDKs (26 did:
`Failed to transform core-for-system-modules.jar`). Give Flutter a JDK 21 of its own and
leave the system one alone:

```bash
brew install openjdk@21        # user-space, no sudo (the temurin cask needs sudo)
flutter config --jdk-dir /opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
```

**2. The SDK path.** `flutter doctor` wants `cmdline-tools`, but a build does not - it only
needs `platform-tools`, `platforms` and `build-tools`, and the licences already accepted in
`$ANDROID_HOME/licenses`. Export the path so `adb` and `emulator` resolve:

```bash
export ANDROID_HOME=$HOME/Library/Android/sdk
export PATH="$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$PATH"
```

Then, with the API running on 5292:

```bash
emulator -avd Pixel_10_Pro &                    # or: flutter emulators --launch Pixel_10_Pro
flutter run -d emulator-5554 --dart-define=FOUND_U_API_BASE_URL=http://10.0.2.2:5292
```

`10.0.2.2` is the emulator's address for the machine it runs on. The first Gradle build
takes several minutes; later ones are seconds. `r` hot-reloads, `q` quits.

**Chrome instead** - no emulator, no JDK, ready in seconds. Port 3000 is on the API's CORS
list already:

```bash
flutter run -d chrome --web-port=3000 --dart-define=FOUND_U_API_BASE_URL=http://localhost:5292
```

Dev accounts: `admin@foundu.com` / `Admin123`, `student@foundu.com` and
`student2@foundu.com` / `Student123`.
