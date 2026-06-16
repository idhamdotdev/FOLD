# F.O.L.D. — Fast Operational Link Display

[![GitHub stars](https://img.shields.io/github/stars/idhamdotdev/FOLD?style=flat-square)](https://github.com/idhamdotdev/FOLD/stargazers)
[![GitHub license](https://img.shields.io/github/license/idhamdotdev/FOLD?style=flat-square)](https://github.com/idhamdotdev/FOLD/blob/main/LICENSE.md)
[![GitHub followers](https://img.shields.io/github/followers/idhamdotdev?label=followers&style=flat-square)](https://github.com/idhamdotdev)

> **Expand your workspace anywhere, instantly.** F.O.L.D. is a high-performance, lightweight utility designed to seamlessly transform any Android tablet (such as the Redmi Pad Pro) into a fully functional, ultra-portable second screen for your Windows PC with touch control.

---

```
                       ┌─────────────────────────┐
                       │   Windows PC (Server)   │
                       └───────────┬─────────────┘
                                   │
                WGC / GDI Screen Capture + H.264 Encoder
                                   │
                   (WiFi: Port 8765 / USB: ADB Forward)
                                   │
                                   ▼
                       ┌─────────────────────────┐
                       │  Android Tablet (Client)│
                       └───────────┬─────────────┘
                                   │
                H.264 Decoder + Fullscreen Render Loop
                                   │
                   (WiFi: Port 8766 / USB: ADB Forward)
                                   │
                                   ▼
                       ┌─────────────────────────┐
                       │  Windows Mouse Control  │
                       │     (Touch Input)       │
                       └─────────────────────────┘
```

---

## Why F.O.L.D.?

*   **Fast**: Zero complex setup. The Virtual Display Driver is downloaded, installed, and configured directly from the application's user interface with one click.
*   **Operational**: Instantly double your screen space for maximum multitasking. It supports true extended desktop mode, not just simple mirroring.
*   **Link**: High-performance connectivity. Stream fluidly over local 5GHz Wi-Fi or use zero-latency USB Mode (via automatic ADB reverse port forwarding).
*   **Display**: Harnesses hardware-accelerated H.264 video streaming to deliver a crisp, responsive visual experience directly on your tablet's high-resolution screen.

---

## How It Works

1.  **Display Extension**: F.O.L.D. installs an indirect display driver (VDD) on Windows to create a virtual monitor.
2.  **Low-Overhead Capture**: The Windows application captures the virtual screen using the modern Windows Graphics Capture (WGC) API or GDI fallback.
3.  **H.264 Encoding**: Captured frames are encoded into a raw H.264 annexB stream in real-time using `Sdcb.FFmpeg` hardware-accelerated encoding.
4.  **Low-Latency Streaming**: The stream is sent over TCP to the Android client, which decodes and renders it using Android's hardware `MediaCodec`.
5.  **Touch Injection**: Touch gestures on the tablet are sent back to the PC and injected as native Windows mouse/touch events mapped to the virtual monitor.

---

## Quick Start

### 1. Windows PC Setup
Download the standalone `FOLD.exe` executable. 

*   **Launch**: Run `FOLD.exe` as Administrator (required to register display drivers, set up network sockets, and inject touch inputs).
*   **Install Virtual Display Driver**:
    1. Go to the **Advanced** tab in the GUI.
    2. Click **INSTALL** on the *Virtual Display Driver* card.
    3. Accept the Windows UAC prompt. The app will download the driver, register the device node, write the display configuration, and automatically extend your Windows desktop.
*   **Firewall Configuration** (Run once in PowerShell as Administrator if Windows Defender blocks connection):
    ```powershell
    New-NetFirewallRule -DisplayName "FOLD Stream" -Direction Inbound -Protocol TCP -LocalPort 8765 -Action Allow
    New-NetFirewallRule -DisplayName "FOLD Touch"  -Direction Inbound -Protocol TCP -LocalPort 8766 -Action Allow
    ```

### 2. Android Tablet Setup
Copy and install `FOLD.apk` onto your tablet. No root permissions are required.

### 3. Connect

#### Option A: Wi-Fi Mode (Wireless Convenience)
1. Ensure both your PC and Android tablet are connected to the same **5GHz Wi-Fi network**.
2. Run F.O.L.D. on your PC and click **START**. Note the local IP address displayed.
3. Open F.O.L.D. on your tablet, enter the PC's IP address, and tap **Connect**.

#### Option B: USB Mode (Zero Latency & Charging)
1. Enable **USB Debugging** in the Developer Options of your Android device.
2. Connect the tablet to the PC with a USB cable.
3. Place `adb.exe` in the same directory as `FOLD.exe` (or ensure it is on your system's `PATH`).
4. Click **ENABLE** under the *USB Mode (ADB)* card in the PC app's **Advanced** tab.
5. Launch the Android app and tap **USB Mode** to connect instantly via localhost port forwarding.

---

## Project Structure

*   `/windows/FOLD` - C# Windows Forms application (.NET 8.0).
*   `/android` - Kotlin Android tablet client application.
*   `/release` - Pre-compiled executable packages, portable distributions, and client APKs.
*   `/tools` - Automation scripts for managing builds and release packaging.

---

## Compilation and Packaging

The workspace includes a release automation script to compile optimized builds:

```powershell
# Run the release compiler script (requires .NET 8.0 SDK and Android SDK)
powershell -ExecutionPolicy Bypass -File tools/MakeRelease.ps1
```

### Build Outputs (in `/release`):
*   `FOLD.exe` (~137.6 MB) - Completely self-contained, single-file Windows executable (no .NET installation needed).
*   `FOLD_portable.zip` (~125.8 MB) - Zipped portable Windows folder.
*   `FOLD.apk` (~2.1 MB) - Optimized Android Release client (minified and resource-shrunk using R8).

---

## License

This project is licensed under the MIT License. See the [LICENSE.md](LICENSE.md) file for details.
