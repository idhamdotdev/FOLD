# Troubleshooting

These steps target FOLD 1.0 on Windows 11 x64. Windows 10 is not supported;
there is no Windows 10 compatibility troubleshooting path. The Windows host
must show an active session before the Android client presses **CONNECT**.

| Problem | What to check |
| --- | --- |
| USB device is missing | Unlock Android, enable USB debugging, use a data-capable cable, accept the fingerprint, then reopen the FOLD device dropdown. `adb devices` should show `device`, not `unauthorized`. |
| Multiple ADB devices are connected | Choose the intended serial in the dropdown before **START**. If a device has no FOLD app or is unauthorized, select the correct one and retry. |
| Windows reports that Android activity does not exist | Install the matching FOLD APK on the selected device. A mismatched or unrelated APK can use a different activity/package contract. |
| Wi-Fi cannot connect | Put both devices on one trusted private LAN, use the exact **Your PC IP**, start Windows first, and allow FOLD on Private networks. Guest isolation, VPN routing, or a changed IP can block it. |
| Connected but black/frozen | Stop both sides, reconnect once, and enable the 30 FPS limit. Confirm Windows is extending the virtual display and an app window is actually placed on it. |
| Touch does not work | Enable **Touch Screen Injection**, reconnect, and confirm the input helper is registered. Disable touch if the network is not trusted. |
| No virtual display | Restart Windows once after installation, check **Settings > System > Display**, select **Extend**, then reinstall the driver only if the display is still absent. |
| High CPU/GPU or stutter | Enable 30 FPS, use 1080p before testing higher resolutions, close competing capture/encode workloads, and prefer USB. Hardware H.264 is used only when a viable hardware Media Foundation encoder is available; otherwise Windows' inbox software encoder is expected to use more CPU. |
| SmartScreen or signature warning | Download only from the official release, verify its SHA-256, and read the release note's signing status. Do not assume a self-signed build proves publisher identity. |

## Before reporting

Include FOLD version, Windows build, Android model/version, USB or Wi-Fi mode,
resolution/FPS, and the exact failure point. Redact ADB keys, signing material,
screen content, private logs, and public IP information.

Report security issues privately as described in [SECURITY.md](../SECURITY.md).
