# FOLD Security Policy

## Supported platforms

Security fixes target publicly released FOLD builds. Windows 11 is the
supported Windows platform; Windows 10 22H2 use is experimental and has no
compatibility guarantee.

## Important network limitations

FOLD is designed for a trusted local connection. Its Wi-Fi transport does not
provide application-level encryption, peer authentication, or access tokens.
The host listens on TCP `8765` for video and TCP `8766` for touch control.

- Use Wi-Fi only on a trusted private network.
- Never expose or port-forward these ports to the internet.
- Do not use Wi-Fi mode on public, guest, hotel, campus, or shared networks.
- Allow FOLD through Windows Firewall only on Private networks.
- Disable touch injection when view-only behavior is sufficient.
- Prefer USB when the local network cannot be trusted.

USB mode relies on Android Debug Bridge. Verify the computer fingerprint on
the Android prompt and revoke USB-debugging authorizations for computers you
no longer trust.

## Reporting a vulnerability

Do not include exploits, ADB keys, signing keys, passwords, private IP
details, or sensitive screen content in a public issue. Use GitHub's private
**Report a vulnerability** channel on the official FOLD repository. Public
release is blocked until that private reporting channel is enabled and tested.

Include the FOLD version, Windows and Android versions, connection mode,
reproduction steps, and impact. Receipt and resolution times are not promised.

## Authenticity

Download FOLD only from the official repository release page, `idham.dev`, or
the official Google Play listing when available. Verify published SHA-256
values before running or installing a direct download.

The current FOLD Windows executables and installer are not Authenticode-signed.
Windows may show an unknown-publisher or SmartScreen warning. The release
builder does not generate, export, trust, or use a self-signed certificate.
Until a publicly trusted code-signing certificate is approved and provisioned,
SHA-256 values from the official release page are the Windows artifact
integrity check.

The direct-download Android APK is a separate trust boundary and must be signed
with FOLD's permanent Android release key. A debug-signed or differently
signed direct APK is not an official release.

The AAB is a private Google Play Console upload artifact and is not a public
download. Google Play may re-sign delivered APKs with its Play App Signing
certificate, so the Play-installed certificate can differ from the direct APK
certificate. The official repository must publish the direct-APK fingerprint
and, after Play App Signing enrollment, the Google Play app-signing
fingerprint as separate identities.
