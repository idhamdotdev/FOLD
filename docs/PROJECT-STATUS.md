# Project status

## Product

FOLD is proprietary freeware with private source code. Users receive the
Windows application, the Android app through an official channel,
documentation, applicable third-party license notices, and published SHA-256
values for direct downloads. The Play Console AAB is not a public download.

## Platform policy

- Windows 11 x64 is the only supported Windows platform.
- Windows 10 is not supported; compatibility testing and troubleshooting are
  outside the support scope.
- Linux, macOS, and iPadOS are not supported.
- Android 8.0 or newer is required.

## Maintenance update — 24 October 2026

The Windows maintenance update fixes an Intel Quick Sync startup/output issue
reported on a ProBook, while preserving hardware preference and automatic
Microsoft inbox software recovery. The Android APK remains version `1.0.0`,
version code `4`, with no binary change in this update. Download hashes are
published in the official GitHub release notes.

## Release gate

A public release is ready only after all of the following are true:

- Windows and Android artifacts are built from the accepted candidate;
- the no-copyleft runtime gate passes on the packaged Windows artifact;
- EULA, privacy, security, and third-party notices match the package;
- the Android signing certificate is verified;
- the Play Console AAB is kept private and its upload certificate is verified;
- unsigned Windows status and SHA-256 values are published accurately;
- private vulnerability reporting is enabled; and
- the public showroom contains no private source, secrets, build tools, or
  internal Git history.

This status is not a promise of a release date, feature roadmap, or universal
hardware compatibility. A successful launch on an unsupported OS does not
change the platform policy.

## User documentation

- [Getting started](GETTING-STARTED.md)
- [Verifying downloads](VERIFYING-DOWNLOADS.md)
- [Troubleshooting](TROUBLESHOOTING.md)
- [Privacy](../PRIVACY.md)
- [Security](../SECURITY.md)
- [EULA](../EULA.md)
