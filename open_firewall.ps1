# Run this script as Administrator to open the required firewall ports for FOLD.
# Right-click → "Run as Administrator"

Write-Host "Opening FOLD firewall rules..." -ForegroundColor Cyan

New-NetFirewallRule `
    -DisplayName "FOLD Stream (TCP 8765)" `
    -Direction Inbound `
    -Protocol TCP `
    -LocalPort 8765 `
    -Action Allow `
    -Profile Any `
    -ErrorAction SilentlyContinue

New-NetFirewallRule `
    -DisplayName "FOLD Touch (TCP 8766)" `
    -Direction Inbound `
    -Protocol TCP `
    -LocalPort 8766 `
    -Action Allow `
    -Profile Any `
    -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Done! Ports 8765 (stream) and 8766 (touch) are now open." -ForegroundColor Green
Write-Host "You can verify with: Get-NetFirewallRule -DisplayName 'FOLD*'"
