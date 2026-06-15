# MakeRelease.ps1 - FOLD Release Builder
# Produces a /release folder at the project root containing:
#   FOLD.exe           - Standalone single-file launcher (~70 MB, no .NET needed)
#   FOLD_portable.zip  - Folder build zipped (needs .NET 8 Desktop Runtime, ~60 MB)
#   FOLD.apk           - Android application

$ErrorActionPreference = "Stop"

$root    = Split-Path $PSScriptRoot -Parent
$csproj  = Join-Path $root "windows\FOLD\FOLD.csproj"
$launcherCsproj = Join-Path $root "windows\FOLDLauncher\FOLDLauncher.csproj"
$android = Join-Path $root "android"
$release = Join-Path $root "release"

function SizeMB($path) { [math]::Round((Get-Item $path).Length / 1MB, 1) }
function Title($msg)   { Write-Host "" ; Write-Host $msg -ForegroundColor Cyan }
function Ok($msg)      { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Warn($msg)    { Write-Host "  [!!] $msg" -ForegroundColor Yellow }
function Fail($msg)    { Write-Host "  [XX] $msg" -ForegroundColor Red }

Title "=== FOLD Release Builder ==="
if (Test-Path $release) {
    try {
        Get-ChildItem $release | Where-Object { $_.Name -ne "FOLD_test" } | Remove-Item -Recurse -Force
    } catch {
        Warn "Could not fully clean the release directory (some files may be locked)."
    }
} else {
    New-Item $release -ItemType Directory | Out-Null
}
Write-Host "  Output: $release"

# -------------------------------------------------------------------------
# 1. Portable Folder & ZIP (framework-dependent, requires .NET 8)
#    This forms the core of both the portable ZIP and the standalone launcher.
# -------------------------------------------------------------------------
Title "[1/3] Building portable (framework-dependent)..."
$portableFolder = Join-Path $release "FOLD"
if (Test-Path $portableFolder) {
    try { Remove-Item $portableFolder -Recurse -Force } catch {}
}
New-Item $portableFolder -ItemType Directory -Force | Out-Null

dotnet publish $csproj -c Release -r win-x64 `
    "-p:SelfContained=false" `
    "-p:PublishSingleFile=false" `
    -o $portableFolder | Out-Null

# Strip unused locale folders from portable folder
Get-ChildItem $portableFolder -Directory | Where-Object {
    $_.Name -match "^(cs|de|es|fr|it|ja|ko|pl|pt-BR|ru|tr|zh-Hans|zh-Hant)$"
} | ForEach-Object { Remove-Item $_.FullName -Recurse -Force }

$portableZip = "$release\FOLD_portable.zip"
Title "  Compressing to ZIP..."
Start-Sleep -Seconds 1
tar -a -cf $portableZip -C $release FOLD

if (Test-Path $portableZip) {
    $folderSize = [math]::Round((Get-ChildItem $portableFolder -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
    Ok ("FOLD folder         ->  " + $folderSize + " MB  (needs .NET 8 Desktop Runtime)")
    Ok ("FOLD_portable.zip   ->  " + (SizeMB $portableZip) + " MB  (compressed)")
} else {
    Fail "FOLD_portable.zip was not created."
}

# Clean up the portable folder (keep only the ZIP)
try { Remove-Item $portableFolder -Recurse -Force } catch {}

# -------------------------------------------------------------------------
# 2. Standalone Launcher EXE (self-contained, single-file, no .NET needed)
#    Embeds FOLD_portable.zip as a resource inside a trimmed wrapper.
# -------------------------------------------------------------------------
Title "[2/3] Building standalone launcher EXE..."
$launcherDir = Join-Path $root "windows\FOLDLauncher"
$embeddedZipPath = Join-Path $launcherDir "FOLD_portable.zip"

# Copy the zip to the launcher folder so it gets embedded
Copy-Item $portableZip $embeddedZipPath -Force

$tmpLauncher = Join-Path $root "windows\FOLDLauncher\_tmp"
if (Test-Path $tmpLauncher) { Remove-Item $tmpLauncher -Recurse -Force }

dotnet publish $launcherCsproj -c Release -r win-x64 `
    "-p:SelfContained=true" `
    "-p:PublishSingleFile=true" `
    "-p:PublishTrimmed=true" `
    -o $tmpLauncher | Out-Null

$launcherExe = Join-Path $tmpLauncher "FOLD.exe"
$targetExe = Join-Path $release "FOLD.exe"

if (Test-Path $launcherExe) {
    Copy-Item $launcherExe $targetExe -Force
    Ok ("FOLD.exe            ->  " + (SizeMB $targetExe) + " MB  (standalone launcher, no .NET required)")
} else {
    Fail "FOLD.exe (launcher) was not created."
}

# Clean up launcher temp files and embedded zip copy
try {
    Remove-Item $embeddedZipPath -Force -ErrorAction SilentlyContinue
    Remove-Item $tmpLauncher -Recurse -Force -ErrorAction SilentlyContinue
} catch {}

# -------------------------------------------------------------------------
# 3. APK
# -------------------------------------------------------------------------
Title "[3/3] Copying APK..."
$apk = Get-ChildItem $android -Recurse -Filter "*.apk" |
       Sort-Object LastWriteTime -Descending |
       Select-Object -First 1

if ($apk) {
    Copy-Item $apk.FullName "$release\FOLD.apk"
    Ok ("FOLD.apk            ->  " + (SizeMB "$release\FOLD.apk") + " MB   (from: " + $apk.Name + ")")
} else {
    Warn "No APK found. Build it first with:"
    Warn "  cd android; .\gradlew assembleRelease"
}

# -------------------------------------------------------------------------
# Summary
# -------------------------------------------------------------------------
Title "=== Release ready ==="
$files = Get-ChildItem $release -File | Sort-Object Length -Descending
foreach ($f in $files) {
    Write-Host ("  {0,-26} {1,6} MB" -f $f.Name, [math]::Round($f.Length/1MB,1))
}
$total = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host ""
Write-Host "  Total: $total MB"
Write-Host "  Path:  $release" -ForegroundColor Cyan
Write-Host ""
