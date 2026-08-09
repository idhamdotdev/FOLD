# Project status

## Product

FOLD is proprietary freeware with private source code. Users receive the
Windows application, the Android app through an official channel,
documentation, applicable third-party license notices, and published SHA-256
values for direct downloads. The Play Console AAB is not a public download.

## Platform policy

- Windows 11 x64 is officially supported and tested.
- Windows 10 22H2 x64 is experimental/best-effort and has no compatibility
  guarantee.
- Android 8.0 or newer is required.

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

This status is not a promise of a release date, feature roadmap, or Windows 10
compatibility.

## User documentation

- [Getting started](GETTING-STARTED.md)
- [Verifying downloads](VERIFYING-DOWNLOADS.md)
- [Troubleshooting](TROUBLESHOOTING.md)
- [Privacy](../PRIVACY.md)
- [Security](../SECURITY.md)
- [EULA](../EULA.md)
