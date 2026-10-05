# FoundU: start the whole system

Terminal 01 - Database

```bash
cd ~/UNI/my/Github/FoundU && docker compose up -d postgres
```

Terminal 02 - AI service

```bash
cd ~/UNI/my/Github/FoundU && testing/start-ai.sh
```

Terminal 03 - API

```bash
cd ~/UNI/my/Github/FoundU/api && AiService__ServiceKey=$(cat ~/.foundu-ai-key) dotnet run --project src/FoundU.Api --launch-profile http
```

Terminal 04 - Web app

```bash
cd ~/UNI/my/Github/FoundU/web && npm run dev
```

Terminal 05 - Emulator

```bash
~/Library/Android/sdk/emulator/emulator -avd Pixel_10_Pro
```

Terminal 06 - Mobile app

```bash
cd ~/UNI/my/Github/FoundU/mobile && flutter run -d emulator-5554 --dart-define=FOUND_U_API_BASE_URL=http://10.0.2.2:5292
```
