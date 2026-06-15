# FOLD — Portable Monitor over Wi-Fi

> Stream your Windows PC screen to your Redmi Pad Pro (or any Android tablet) over Wi-Fi. Touch input is forwarded back to control your PC mouse.

---

## How It Works

```
PC (Windows EXE)                              Android Tablet (APK)
─────────────────                             ────────────────────
GdiCapture  →  MjpegServer  ──HTTP 8765──►  MjpegView (renders frames)
TouchReceiver  ◄─HTTP 8766──               TouchSender (sends taps)
TouchInjector (SendInput)
```

## Quick Start

### 1 — Windows PC

```powershell
# Build & run (requires .NET 8 SDK)
cd windows\FOLD
dotnet run

# The tray icon appears. Right-click → "Copy PC IP"
```

To open firewall ports (run once as Administrator):
```powershell
New-NetFirewallRule -DisplayName "FOLD Stream" -Direction Inbound -Protocol TCP -LocalPort 8765 -Action Allow
New-NetFirewallRule -DisplayName "FOLD Touch"  -Direction Inbound -Protocol TCP -LocalPort 8766 -Action Allow
```

### 2 — Android Tablet

```bash
# Build debug APK (requires Android Studio or JDK 17 + Android SDK)
cd android
./gradlew assembleDebug

# Install via USB
adb install app/build/outputs/apk/debug/app-debug.apk

# Or copy the APK to the tablet and install manually:
# Settings → Apps → Special app access → Install unknown apps → enable for your file manager
```

### 3 — Connect

1. Make sure both devices are on the **same Wi-Fi network**
2. Run **FOLD** on PC → right-click tray icon → **Copy PC IP**
3. Open **FOLD** on tablet → paste the IP → tap **Connect**
4. Your PC screen streams to the tablet in landscape fullscreen!
5. Tap / drag the tablet screen → controls your PC mouse

---

## Performance Tuning

| Setting | File | Default | Notes |
|---------|------|---------|-------|
| JPEG Quality | `TrayApp.cs` → `Quality` | 75 | 60 = faster, 90 = sharper |
| Target FPS | `TrayApp.cs` → `TargetFps` | 30 | 20 = slow Wi-Fi, 60 = fast LAN |
| Resolution scale | `GdiCapture.cs` constructor | 1.0 | 0.75 = ~44% less bandwidth |
| Wi-Fi band | Router | — | Use 5GHz for lowest latency |

### Expected Latency on 5GHz Wi-Fi

| Resolution | Quality | FPS | Latency |
|------------|---------|-----|---------|
| 1920×1080  | 75 | 30 | ~40ms |
| 1920×1080  | 60 | 30 | ~25ms |
| 1440×810   | 75 | 60 | ~30ms |
| 1280×720   | 75 | 60 | ~20ms |

---

## Build Release

### Windows — single portable EXE

```powershell
cd windows\FOLD
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\publish

# Output: publish\FOLD.exe  (~25 MB, no install needed)
```

### Android — release APK

```bash
cd android
./gradlew assembleRelease
# app/build/outputs/apk/release/app-release-unsigned.apk
# Sign with your keystore before distributing
```

---

## Notes

- **WGC GPU capture:** `WgcCapture.cs` is stubbed. The app auto-falls-back to `GdiCapture`. To enable WGC, add `SharpDX.Direct3D11` NuGet and implement `CreateD3DDevice()` + `SurfaceToBitmap()`.
- **Multi-monitor:** `GdiCapture` captures the primary monitor. Add a monitor picker in the tray menu using `Screen.AllScreens[]`.
- **USB tethering:** The HTTP server works over USB tethering too — connect via USB, enable "USB tethering" on the PC, and use `192.168.137.1` as the PC IP.
- **Security:** No authentication. Use only on trusted home/private networks.
