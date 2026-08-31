# Third-Party Notices

This notice applies to FOLD release artifacts. FOLD's own code
is governed by [EULA.md](EULA.md). The components below retain their own
copyrights and license terms. License texts distributed with FOLD are in the
[LICENSES](LICENSES) directory.

## Virtual Display Driver

- Upstream: https://github.com/VirtualDrivers/Virtual-Display-Driver
- Copyright (c) 2024 Virtual Display and project contributors
- Version used by FOLD: installer product version `25.05.03`
- License: MIT
- Use: creates the virtual extended monitor; its installer is embedded in the
  Windows host and copied into the Windows installer.

FOLD applies a FOLD-owned custom EDID through the driver's supported settings
to show the monitor name `FOLD Display`. The upstream driver binary remains
unmodified and remains attributed to its authors under the MIT License.

## Android Debug Bridge (ADB)

- Upstream: https://android.googlesource.com/platform/packages/modules/adb/
- Copyright: Google LLC and Android Open Source Project contributors
- Binary source: Android SDK Platform-Tools `36.0.0`
- License: Apache License 2.0
- Use: `adb.exe`, `AdbWinApi.dll`, and `AdbWinUsbApi.dll` provide USB device
  discovery, app launch, and port forwarding.

The exact notice bundle shipped with Android SDK Platform-Tools `36.0.0` is
preserved at `LICENSES/Android-Platform-Tools-36.0.0-NOTICE.txt`.

## SharpDX

- Upstream: https://github.com/sharpdx/SharpDX
- Copyright (c) 2010-2014 SharpDX - Alexandre Mutel
- Packages: `SharpDX.Direct3D11` `4.2.0` and `SharpDX.DXGI` `4.2.0`
- License: MIT
- Use: Direct3D 11 and DXGI interop for Windows screen capture.

## Microsoft .NET

- Upstream: https://github.com/dotnet/runtime
- Copyright: .NET Foundation and contributors
- Use: FOLD's self-contained Windows artifacts include the .NET 8 runtime,
  Windows Forms, and framework libraries. FOLD also references the Microsoft
  `System.Net.Http` `4.3.4` and `System.Text.RegularExpressions` `4.3.1`
  packages.

The exact Windows runtime packs used by FOLD are version `8.0.27`. Microsoft
documents mixed licensing for a .NET Windows distribution:

- most .NET runtime and library files, including the referenced `System.*`
  packages, are provided under the MIT License;
- `coreclr.dll`, `Microsoft.DiaSymReader.Native.amd64.dll`,
  `PresentationNative_cor3.dll`, `vcruntime140_cor3.dll`, and
  `wpfgfx_cor3.dll` are governed by the Microsoft .NET Library License; and
- `D3DCompiler_47_cor3.dll` is governed by the Microsoft Windows SDK License.

See `LICENSES/Microsoft-.NET-Windows-License-Information.md` and the exact
runtime notice file
`LICENSES/Microsoft-.NET-8.0.27-THIRD-PARTY-NOTICES.txt`. The official .NET
Windows classification is maintained at
https://github.com/dotnet/core/blob/main/license-information-windows.md.

The Microsoft Media Foundation H.264 encoder is a Windows system component.
FOLD calls it through Windows APIs and does not redistribute it as a separate
encoder package.

## Android application libraries

The Android client directly uses:

- AndroidX Core KTX `1.13.1`
- AndroidX AppCompat `1.7.0`
- AndroidX ConstraintLayout `2.1.4`
- Material Components for Android `1.12.0`
- OkHttp `4.12.0`
- Kotlin Coroutines Android `1.8.1`

These libraries and included transitive libraries such as Kotlin standard
libraries and Okio are provided under Apache License 2.0. Copyright belongs
to their respective authors and contributors.

## Lalezar font

- Upstream: https://github.com/BornaIz/Lalezar
- Copyright 2015 The Lalezar Project Authors
- License: SIL Open Font License 1.1
- Use: embedded Windows and Android user-interface typeface.

## Inno Setup

- Upstream: https://jrsoftware.org/isinfo.php
- Copyright: Jordan Russell, Martijn Laan, and contributors
- License: Inno Setup License
- Use: build tool used to create `FOLD_setup.exe`; it is not part of FOLD's
  proprietary source license.

Windows is a trademark of the Microsoft group of companies. Android is a
trademark of Google LLC. Other names and brands belong to their respective
owners and are used for identification only.
