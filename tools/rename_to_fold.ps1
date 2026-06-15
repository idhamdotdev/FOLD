# rename_to_fold.ps1 - Rename FOLD/FOLD -> FOLD throughout the project
$root = 'c:\Users\idham\Downloads\Devolopment\TEST APK\FOLD'
$extensions = @('*.cs','*.csproj','*.sln','*.xml','*.md','*.json','*.gradle','*.gradle.kts','*.properties','*.kt','*.txt','*.ps1','*.manifest')

Write-Host '=== Step 1: Replace text in all source files ===' -ForegroundColor Cyan
Get-ChildItem -Path $root -Recurse -File -Include $extensions | ForEach-Object {
    $file = $_
    try {
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8 -ErrorAction Stop
        if ($content -and ($content -match 'FOLD|FOLD')) {
            $newContent = $content -replace 'FOLD', 'FOLD' -replace 'FOLD', 'FOLD'
            Set-Content -Path $file.FullName -Value $newContent -Encoding UTF8 -NoNewline
            Write-Host "  Updated: $($file.FullName.Replace($root, '.'))"
        }
    } catch {
        Write-Warning "  Skipped (locked/binary): $($file.Name)"
    }
}

Write-Host ''
Write-Host '=== Step 2: Rename FOLD.csproj -> FOLD.csproj ===' -ForegroundColor Cyan
$csprojPath = Join-Path $root 'windows\FOLD\FOLD.csproj'
if (Test-Path $csprojPath) {
    Rename-Item -Path $csprojPath -NewName 'FOLD.csproj' -ErrorAction SilentlyContinue
    Write-Host '  OK'
} else { Write-Host '  Not found (already renamed?)' }

Write-Host ''
Write-Host '=== Step 3: Rename FOLD.sln -> FOLD.sln ===' -ForegroundColor Cyan
$slnPath = Join-Path $root 'windows\FOLD.sln'
if (Test-Path $slnPath) {
    Rename-Item -Path $slnPath -NewName 'FOLD.sln' -ErrorAction SilentlyContinue
    Write-Host '  OK'
} else { Write-Host '  Not found (already renamed?)' }

Write-Host ''
Write-Host '=== Step 4: Rename nuget dgspec json ===' -ForegroundColor Cyan
$dgspecPath = Join-Path $root 'windows\FOLD\obj\FOLD.csproj.nuget.dgspec.json'
if (Test-Path $dgspecPath) {
    Rename-Item -Path $dgspecPath -NewName 'FOLD.csproj.nuget.dgspec.json' -ErrorAction SilentlyContinue
    Write-Host '  OK'
} else { Write-Host '  Not found (already renamed?)' }

Write-Host ''
Write-Host '=== Step 5: Rename FileListAbsolute.txt ===' -ForegroundColor Cyan
$flPath = Join-Path $root 'windows\FOLD\obj\Debug\net8.0-windows10.0.22621.0\win-x64\FOLD.csproj.FileListAbsolute.txt'
if (Test-Path $flPath) {
    Rename-Item -Path $flPath -NewName 'FOLD.csproj.FileListAbsolute.txt' -ErrorAction SilentlyContinue
    Write-Host '  OK'
} else { Write-Host '  Not found (already renamed?)' }

Write-Host ''
Write-Host '=== Step 6: Rename windows\FOLD folder -> windows\FOLD ===' -ForegroundColor Cyan
$winFolderPath = Join-Path $root 'windows\FOLD'
if (Test-Path $winFolderPath) {
    Rename-Item -Path $winFolderPath -NewName 'FOLD' -ErrorAction SilentlyContinue
    Write-Host '  OK - windows\FOLD -> windows\FOLD'
} else { Write-Host '  Not found (already renamed?)' }

Write-Host ''
Write-Host '=== Step 7: Rename FOLD_Implementation_Plan_v2.md ===' -ForegroundColor Cyan
$mdPath = Join-Path $root 'FOLD_Implementation_Plan_v2.md'
if (Test-Path $mdPath) {
    Rename-Item -Path $mdPath -NewName 'FOLD_Implementation_Plan_v2.md' -ErrorAction SilentlyContinue
    Write-Host '  OK'
} else { Write-Host '  Not found (already renamed?)' }

Write-Host ''
Write-Host '=== Step 8: Rename root FOLD folder -> FOLD ===' -ForegroundColor Cyan
$parentDir = 'c:\Users\idham\Downloads\Devolopment\TEST APK'
$rootFolder = Join-Path $parentDir 'FOLD'
if (Test-Path $rootFolder) {
    Rename-Item -Path $rootFolder -NewName 'FOLD' -ErrorAction SilentlyContinue
    Write-Host '  OK - FOLD -> FOLD'
    Write-Host ''
    Write-Host '  NOTE: The root folder is now: c:\Users\idham\Downloads\Devolopment\TEST APK\FOLD' -ForegroundColor Yellow
} else { Write-Host '  Not found (already renamed?)' }

Write-Host ''
Write-Host '=== ALL DONE ===' -ForegroundColor Green
