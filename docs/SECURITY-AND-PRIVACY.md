# Security and privacy guide

The authoritative product documents are [PRIVACY.md](../PRIVACY.md) and
[SECURITY.md](../SECURITY.md). This page is the short operating guide.

## USB mode

USB mode uses ADB forwarding. Android USB debugging gives an authorized PC
powerful debugging access, so verify the computer fingerprint, use only a PC
you trust, and revoke old debugging authorizations from Android Developer
options.

When more than one ADB device is connected, select the intended serial in the
Windows dropdown. FOLD scopes app launch and forwarding to that device.

## Wi-Fi mode

FOLD 1.0 uses direct cleartext TCP/HTTP on the local network and does not add
peer authentication or encryption. Treat Wi-Fi as trusted-private-network
only.

- Never use public, guest, hotel, campus, or unknown Wi-Fi.
- Never expose or port-forward TCP `8765` or `8766`.
- Allow Windows Firewall access only on Private networks.
- Disable touch injection for view-only use.
- Prefer USB if other network users are not trusted.

## Local files

Windows stores the selected ADB serial in `%APPDATA%\FOLD\settings.json` and
writes bounded operational logs to `%LOCALAPPDATA%\FOLD\logs\h264_server.log`.
The active log rotates at approximately 1 MiB and retains up to two backups.
Android stores the last PC IP and connection mode in private app preferences.
FOLD does not upload these files, and Android cloud backup/device transfer is
disabled for FOLD data. Users should remove sensitive troubleshooting logs
before sharing them.

Never share signing keys, `.jks`, `.pfx`, ADB private keys, passwords, private
screen content, or a public IP address in an issue.
