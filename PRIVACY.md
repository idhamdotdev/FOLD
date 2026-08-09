# FOLD Privacy Notice

Effective date: 9 August 2026

## Summary

FOLD does not require an account and does not contain product analytics,
advertising, telemetry, or a cloud relay. A normal session sends display video
from the Windows host directly to the selected Android device and can send
touch coordinates back to the Windows host.

## Data transmitted during a session

- **Video:** pixels captured from the selected virtual display are encoded as
  H.264 and sent to the connected Android device.
- **Touch:** when touch injection is enabled, touch coordinates and basic
  display configuration travel from Android to the Windows host.
- **Connection metadata:** local IP addresses, ports, and the selected ADB
  device identifier are used to establish the connection.

USB mode uses ADB port forwarding. Wi-Fi mode uses direct cleartext TCP/HTTP
on the local network. FOLD does not upload this session data to idham.dev, but
another party with access to an untrusted network may be able to observe or
interfere with unencrypted Wi-Fi traffic. See [SECURITY.md](SECURITY.md).

## Data stored locally

The Windows host stores the last selected ADB device serial in
`%APPDATA%\FOLD\settings.json`. It also writes operational encoder and
connection messages to `h264_server.log` beside the running host files. These
logs can include timestamps, resolution, encoder errors, and connection
events, but are not transmitted by FOLD.

The Android client stores the last entered PC IP address and selected
connection mode in the app's private preferences. Android system backup may
handle app preferences according to the device's backup settings. FOLD does
not store screen recordings or touch history as a product feature.

## Retention and deletion

Local preferences and logs remain until the user, operating system, app
update, or uninstaller removes them. You can delete the Windows settings and
log files while FOLD is closed. Clearing the Android app's storage removes its
saved IP and mode.

## Third-party platform services

Windows, Android, device vendors, networks, and download platforms may process
data independently under their own policies. Their behavior is outside
FOLD's control.

## Changes

Material changes to this notice will be published with an updated effective
date. Release-specific documentation controls when behavior differs between
versions.
