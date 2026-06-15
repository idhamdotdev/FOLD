param()

# ── Keep window open on any error ────────────────────────────────────────────
trap {
    Write-Host ""
    Write-Host "==================================================" -ForegroundColor Red
    Write-Host " FATAL ERROR                                      " -ForegroundColor Red
    Write-Host "==================================================" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkRed
    Write-Host ""
    Write-Host "Press Enter to close..."
    Read-Host
    exit 1
}

$ErrorActionPreference = "Stop"

# ── Check Admin manually (no #Requires) ──────────────────────────────────────
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "This script must run as Administrator." -ForegroundColor Red
    Write-Host "Right-click PowerShell → 'Run as administrator', then run this script again."
    Write-Host ""
    Write-Host "Press Enter to close..."
    Read-Host
    exit 1
}

# ── Configuration ─────────────────────────────────────────────────────────────
$RELEASE_URL = "https://github.com/VirtualDrivers/Virtual-Display-Driver/releases/download/25.5.2/Virtual.Display.Driver-v25.05.03-setup-x64.exe"
$CACHE_DIR   = Join-Path $env:LOCALAPPDATA "FOLD\drivers"
$INSTALL_EXE = Join-Path $CACHE_DIR "vdd_setup.exe"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " FOLD - Virtual Display Driver Installer           " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# ── Step 1: Download (or use cache) ──────────────────────────────────────────
if (Test-Path $INSTALL_EXE) {
    $size = (Get-Item $INSTALL_EXE).Length
    if ($size -gt 500000) {
        Write-Host "[1/3] Using cached installer ($([math]::Round($size/1MB, 2)) MB)" -ForegroundColor Green
    } else {
        Remove-Item $INSTALL_EXE -Force -ErrorAction Ignore
    }
}

if (-not (Test-Path $INSTALL_EXE)) {
    Write-Host "[1/3] Downloading Virtual Display Driver Installer..." -ForegroundColor Yellow
    Write-Host "      URL: $RELEASE_URL"

    New-Item -Path $CACHE_DIR -ItemType Directory -Force | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $wc = New-Object System.Net.WebClient
    $wc.DownloadFile($RELEASE_URL, $INSTALL_EXE)

    if (-not (Test-Path $INSTALL_EXE)) { throw "Download failed - file not found at $INSTALL_EXE" }
    Write-Host "      Downloaded OK. ($([math]::Round((Get-Item $INSTALL_EXE).Length/1MB, 2)) MB)" -ForegroundColor Green
}

# ── Step 2: Install ───────────────────────────────────────────────────────────
Write-Host "[2/3] Installing Virtual Display Driver (Silent)..." -ForegroundColor Yellow
Write-Host "      This will automatically create the virtual monitor."

$process = Start-Process -FilePath $INSTALL_EXE -ArgumentList "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES" -Wait -PassThru
if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) {
    throw "Installer failed with exit code $($process.ExitCode). Please try running $INSTALL_EXE manually."
}

Write-Host "      Driver installed OK!" -ForegroundColor Green

# ── Step 3: Write VDD configuration ──────────────────────────────────────────
Write-Host "[3/3] Writing display configuration..." -ForegroundColor Yellow

$optDir = "C:\IddSampleDriver"
New-Item -Path $optDir -ItemType Directory -Force | Out-Null
$config = @"
1
1920, 1080, 60
2560, 1440, 60
2560, 1600, 60
3840, 2160, 60
1280, 720, 60
1366, 768, 60
"@
Set-Content -Path (Join-Path $optDir "option.txt") -Value $config -Encoding UTF8

# Auto-extend the display
Write-Host "      Extending display..." -ForegroundColor Yellow
Start-Process -FilePath "DisplaySwitch.exe" -ArgumentList "/extend" -Wait -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Write-Host "      Configuration applied!" -ForegroundColor Green

Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host " SUCCESS! Virtual Display Driver installed!       " -ForegroundColor Green
Write-Host "                                                   " 
Write-Host " NEXT STEPS:                                       " -ForegroundColor White
Write-Host " 1. Open FOLD and click START                     " -ForegroundColor White
Write-Host " 2. Connect your Android device via WiFi or USB   " -ForegroundColor White
Write-Host "                                                   "
Write-Host " The virtual monitor should appear automatically.  " -ForegroundColor White
Write-Host " If not, press Win+P and choose 'Extend'.         " -ForegroundColor White
Write-Host "==================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Press Enter to close..."
Read-Host
