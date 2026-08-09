# Pinned Native Binary Inputs

These files are inputs to the Windows release but are not all stored in Git.
`tools/MakeRelease.ps1` verifies every SHA-256 before packaging to prevent an
unreviewed replacement from entering an official release.

## Android SDK Platform-Tools 36.0.0

Official download archive:

https://dl.google.com/android/repository/platform-tools_r36.0.0-win.zip

Upstream source:

https://android.googlesource.com/platform/packages/modules/adb/

| File | SHA-256 |
|------|---------|
| `windows/FOLD/Resources/adb.exe` | `1E1C2280B90B3F01AD84CD8DF4858B1B1995012814F3CA8893BCC3BA3848EDEC` |
| `windows/FOLD/Resources/AdbWinApi.dll` | `120BEF587119C6CB926B86B9BE90FDFBCE38937588EAE28CD91A94CE63C7B965` |
| `windows/FOLD/Resources/AdbWinUsbApi.dll` | `6CA69A2CA0E31309C087D288F058977D421AD03500E4C3E1DBD981241A069C60` |

The inspected files carry valid Google LLC Authenticode signatures.

## Virtual Display Driver installer 25.05.03

Upstream release:

https://github.com/VirtualDrivers/Virtual-Display-Driver/releases/tag/25.5.2

Upstream source revision: `3a1871f`

| File | SHA-256 |
|------|---------|
| `windows/FOLD/Resources/vdd_setup.exe` | `CA10B85BABECFB636C85B3F04D2306968D4F940DD3DD35767F866207BFBA846E` |

The bundled file is the unmodified upstream release asset named
`Virtual.Display.Driver-v25.05.03-setup-x64.exe`. It reports product version
`25.05.03` and is not Authenticode-signed. FOLD pins its exact SHA-256 and does
not modify or sign the binary.

When updating any pinned dependency, verify its upstream license and
signature, update the third-party notices, then deliberately update both this
file and the matching hash in `tools/MakeRelease.ps1`.
