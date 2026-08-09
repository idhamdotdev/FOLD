# Getting started with FOLD

Install the Windows host and a matching Android client from an official FOLD
distribution channel.

## Before you start

- Windows 11 x64 PC for official support. Windows 10 22H2 x64 can try the same
  build as experimental/best-effort only.
- Android tablet running Android 8.0 or newer.
- A data-capable USB cable, or a trusted private local Wi-Fi network.
- Administrator approval when the installer configures the Virtual Display
  Driver and input helper.

FOLD creates an **extended virtual monitor**. It is not a screen mirror. Move
an app window onto that monitor to show it on the tablet.

## 1. Install the Windows host

1. Download `FOLD_setup.exe` from the official release page.
2. Verify its published SHA-256 value before running it. See
   [Verifying downloads](VERIFYING-DOWNLOADS.md).
3. Run the installer and approve the administrator prompt.
4. Start FOLD from the Start menu or desktop shortcut.

Windows should expose an additional display under **Settings > System >
Display**. Choose **Extend these displays** if Windows did not do so
automatically.

## 2. Install the Android client

1. Install the matching Android app from the official Google Play listing when
   available, or download `FOLD.apk` from the same repository release.
2. For a direct APK, verify its SHA-256 value and signing fingerprint as
   described in [Verifying downloads](VERIFYING-DOWNLOADS.md), then allow
   installation from that source when Android asks.
3. Open FOLD on the tablet.

## 3. Connect over USB (recommended)

1. Enable Android **Developer options** and **USB debugging**.
2. Connect the tablet with a data-capable USB cable.
3. Accept the PC fingerprint on Android after verifying it.
4. In Windows FOLD, select **USB (ADB)**.
5. If multiple ADB devices are present, choose the intended device from the
   dropdown. Opening the dropdown refreshes the device list.
6. Press **START** on Windows.
7. Select **USB** in Android FOLD and tap **CONNECT**.

The Windows host creates ADB port forwarding only for the selected device.
Leave the cable connected during the session.

## 4. Connect over Wi-Fi

Wi-Fi is convenient but does not add FOLD-level encryption or authentication.
Use only a private network you control.

1. Put both devices on the same trusted local network.
2. Select **Wi-Fi** in Windows FOLD and copy **Your PC IP**.
3. Press **START** on Windows.
4. Select **Wi-Fi** on Android, enter the exact PC IP, and tap **CONNECT**.
5. If Windows Firewall asks, allow access on **Private networks** only.

Never enter a public internet address or port-forward FOLD ports.

## Windows controls

| Control | Effect |
| --- | --- |
| **START / STOP** | Starts or ends the host session. Start the host before Android connects. |
| **Limit stream to 30 FPS** | Lowers frame rate to reduce GPU/power use. |
| **Touch Screen Injection** | Allows Android touch to control the virtual monitor. |
| **Debug Mode** | Shows diagnostics; leave off for normal use. |
| **USB device dropdown** | Selects the exact ADB device used for forwarding and app launch. |

Minimizing hides FOLD in the tray. Use the tray menu to show FOLD, disconnect
an active session, or exit.

For problems, see [Troubleshooting](TROUBLESHOOTING.md). For network risk, see
[Security](../SECURITY.md).
