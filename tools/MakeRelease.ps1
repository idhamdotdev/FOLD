# MakeRelease.ps1 - FOLD Release Builder
# Produces a /release folder at the project root containing:
#   FOLD.exe           - Standalone single-file (any PC, no .NET needed)
#   FOLD_portable.zip  - Folder build zipped    (needs .NET 8 Desktop Runtime)
#   FOLD.apk           - Android application

$ErrorActionPreference = "Stop"

$root    = Split-Path $PSScriptRoot -Parent
$csproj  = Join-Path $root "windows\FOLD\FOLD.csproj"
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
# 1. Standalone EXE (self-contained, single file, works on any PC)
# -------------------------------------------------------------------------
Title "[1/3] Building standalone EXE..."
$tmp1 = Join-Path $root "windows\FOLD\_tmp_standalone"
if (Test-Path $tmp1) { Remove-Item $tmp1 -Recurse -Force }

dotnet publish $csproj -c Release -r win-x64 `
    "-p:SelfContained=true" `
    "-p:PublishSingleFile=true" `
    "-p:IncludeNativeLibrariesForSelfExtract=true" `
    -o $tmp1 | Out-Null

$exeSrc = Join-Path $tmp1 "FOLD.exe"
if (Test-Path $exeSrc) {
    Copy-Item $exeSrc "$release\FOLD.exe" -Force
    try {
        Remove-Item $tmp1 -Recurse -Force
    } catch {
        Warn "Could not clean up temporary directory $tmp1 (files may be locked)."
    }
    Ok ("FOLD.exe  ->  " + (SizeMB "$release\FOLD.exe") + " MB  (standalone, no .NET required)")
} else {
    Fail "FOLD.exe not found - standalone build failed."
    try {
        Remove-Item $tmp1 -Recurse -Force
    } catch {}
}

# -------------------------------------------------------------------------
# 2. Portable Folder & ZIP (framework-dependent folder, requires .NET 8)
# -------------------------------------------------------------------------
Title "[2/3] Building portable folder & ZIP..."
$portableFolder = Join-Path $release "FOLD"
if (Test-Path $portableFolder) {
    try {
        Remove-Item $portableFolder -Recurse -Force
    } catch {
        Warn "Could not remove $portableFolder, trying to delete files individually..."
        Get-ChildItem $portableFolder -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}
if (-not (Test-Path $portableFolder)) {
    New-Item $portableFolder -ItemType Directory | Out-Null
}

dotnet publish $csproj -c Release -r win-x64 `
    "-p:SelfContained=false" `
    "-p:PublishSingleFile=false" `
    -o $portableFolder | Out-Null

$zip = "$release\FOLD_portable.zip"
Title "  Waiting for file locks to clear..."
Start-Sleep -Seconds 1
tar -a -cf $zip -C $release FOLD

if (Test-Path $zip) {
    $folderSize = [math]::Round((Get-ChildItem $portableFolder -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
    Ok ("FOLD folder         ->  " + $folderSize + " MB  (same structure as STICam)")
    Ok ("FOLD_portable.zip   ->  " + (SizeMB $zip) + " MB  (zipped version)")
} else {
    Fail "FOLD_portable.zip was not created."
}

# -------------------------------------------------------------------------
# 3. APK
# -------------------------------------------------------------------------
Title "[3/3] Copying APK..."
$apk = Get-ChildItem $android -Recurse -Filter "*.apk" |
       Sort-Object LastWriteTime -Descending |
       Select-Object -First 1

if ($apk) {
    Copy-Item $apk.FullName "$release\FOLD.apk"
    Ok ("FOLD.apk  ->  " + (SizeMB "$release\FOLD.apk") + " MB   (from: " + $apk.Name + ")")
} else {
    Warn "No APK found. Build it first with:"
    Warn "  cd android && .\gradlew assembleRelease"
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
