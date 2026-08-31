# Pinned Native Binary Inputs

These files are inputs to the Windows release but are not all stored in Git.
`tools/MakeRelease.ps1` verifies every SHA-256 before packaging to prevent an
unreviewed replacement from entering an official release.

## Android SDK Platform-Tools 36.0.0

Official download archive:

https://dl.google.com/android/repository/platform-tools_r36.0.0-win.zip

Archive SHA-256:

`12C2841F354E92A0EB2FD7BF6F0F9BF8538ABCE7BD6B060AC8349D6F6A61107C`

Upstream source:

https://android.googlesource.com/platform/packages/modules/adb/

| File | SHA-256 |
|------|---------|
| `windows/FOLD/Resources/adb.exe` | `1E1C2280B90B3F01AD84CD8DF4858B1B1995012814F3CA8893BCC3BA3848EDEC` |
| `windows/FOLD/Resources/AdbWinApi.dll` | `120BEF587119C6CB926B86B9BE90FDFBCE38937588EAE28CD91A94CE63C7B965` |
| `windows/FOLD/Resources/AdbWinUsbApi.dll` | `6CA69A2CA0E31309C087D288F058977D421AD03500E4C3E1DBD981241A069C60` |
| `LICENSES/Android-Platform-Tools-36.0.0-NOTICE.txt` | `B319EE2DC88F5D38B0921B0349BF6F7A1F7B30453ECEF2180A74989503D7F704` |

The inspected binaries carry valid Google LLC Authenticode signatures.
`adb.exe` matches the official archive byte-for-byte. The two bundled DLLs
carry a later Google timestamp-signature envelope; their unsigned PE payloads
match the official archive: `AdbWinApi.dll` has unsigned payload SHA-256
`A9F2672371A08288C7157912394648D0EB123D8324DB0E08508BF5058B6A28A4` and
`AdbWinUsbApi.dll` has unsigned payload SHA-256
`B821BB50ECEA35CFEDEE6464EF8811C7A7FC1538E812B675646552873485A715`.
The bundled notice is copied byte-for-byte from the same official archive.

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

FOLD uses the driver's supported custom-EDID setting to expose the user-facing
monitor name `FOLD Display`. The 256-byte FOLD-owned EDID overlay has SHA-256
`8593CF76058E7B16DF08F02560ED773EA379F57B150D3A5F3A3646FA8F7E50FF`.
It preserves the upstream `MTT` manufacturer ID and `1337` product code used
for driver detection. Applying this overlay does not modify the pinned driver
installer or its installed driver binary.

When updating any pinned dependency, verify its upstream license and
signature, update the third-party notices, then deliberately update both this
file and the matching hash in `tools/MakeRelease.ps1`.
