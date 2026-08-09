# Verifying your FOLD download

Use official FOLD download channels only: the GitHub release page, `idham.dev`,
or the official Google Play listing when available. Do not install files from
mirrors, file-sharing links, modified-build sites, or forwarded messages.

## 1. Verify the SHA-256 checksum

Each GitHub release provides exactly two direct-download files:

- `FOLD_setup.exe`
- `FOLD.apk`

The official release notes publish the SHA-256 value for both files, and GitHub
also displays a digest next to each uploaded asset. In PowerShell, run the
matching command:

```powershell
Get-FileHash .\FOLD_setup.exe -Algorithm SHA256
Get-FileHash .\FOLD.apk -Algorithm SHA256
```

Compare the value with the matching SHA-256 in the release notes and with the
GitHub asset digest. If it does not match exactly, delete the file and download
it again from the official release page. Do not run or install it.

## 2. Understand Windows authenticity

Current FOLD Windows files are not Authenticode-signed. Windows may show an
unknown-publisher or SmartScreen warning. The published SHA-256 value is the
integrity check for a direct Windows download; FOLD does not ask you to trust a
self-signed certificate.

## 3. Verify a direct Android APK

The direct-download APK is signed with FOLD's permanent Android release key.
Compare the certificate fingerprint shown in the official release notes with
the APK before installing it. If Android build tools are available, use:

```powershell
apksigner verify --verbose --print-certs .\FOLD.apk
```

The certificate SHA-256 must exactly match the direct-APK fingerprint in that
release's notes. A debug-signed APK, an APK signed by a different certificate,
or an update that asks you to uninstall the official FOLD app is not an
official direct release.

Google Play uses a separate delivery trust boundary and may re-sign the APK
through Play App Signing. The private Play Console upload AAB is not a public
download.

## 4. Report a problem safely

For a checksum mismatch or suspicious official-release behavior, use the
private reporting channel described in [SECURITY.md](../SECURITY.md). Do not
post download files, signing keys, ADB keys, passwords, private IP addresses,
or sensitive screen content in a public issue.
