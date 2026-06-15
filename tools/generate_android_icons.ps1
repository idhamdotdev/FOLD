Add-Type -AssemblyName System.Drawing

$srcPath = "c:\Users\idham\Downloads\Devolopment\TEST APK\FOLD\icon\android.png"
$resDir = "c:\Users\idham\Downloads\Devolopment\TEST APK\FOLD\android\app\src\main\res"

$configs = @(
    @{ Name = "mdpi"; LegacySize = 48; AdaptiveSize = 108; ArtSize = 72 },
    @{ Name = "hdpi"; LegacySize = 72; AdaptiveSize = 162; ArtSize = 108 },
    @{ Name = "xhdpi"; LegacySize = 96; AdaptiveSize = 216; ArtSize = 144 },
    @{ Name = "xxhdpi"; LegacySize = 144; AdaptiveSize = 324; ArtSize = 216 },
    @{ Name = "xxxhdpi"; LegacySize = 192; AdaptiveSize = 432; ArtSize = 288 }
)

if (!(Test-Path $srcPath)) {
    Write-Error "Source file not found: $srcPath"
    exit 1
}

$src = [System.Drawing.Bitmap]::FromFile($srcPath)
$bgColor = [System.Drawing.Color]::FromArgb(15, 23, 42) # #0F172A

foreach ($cfg in $configs) {
    $dir = Join-Path $resDir "mipmap-$($cfg.Name)"
    if (!(Test-Path $dir)) { 
        New-Item -ItemType Directory -Path $dir | Out-Null 
    }

    # 1. Legacy Icons (ic_launcher.png and ic_launcher_round.png)
    # These contain the background and are scaled to LegacySize x LegacySize
    $legacySize = $cfg.LegacySize
    $bmpLegacy = New-Object System.Drawing.Bitmap($legacySize, $legacySize)
    $gLegacy = [System.Drawing.Graphics]::FromImage($bmpLegacy)
    $gLegacy.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gLegacy.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $gLegacy.DrawImage($src, 0, 0, $legacySize, $legacySize)
    $gLegacy.Dispose()
    
    $legacyPath = Join-Path $dir "ic_launcher.png"
    $roundPath = Join-Path $dir "ic_launcher_round.png"
    
    $bmpLegacy.Save($legacyPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmpLegacy.Save($roundPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmpLegacy.Dispose()
    Write-Host "Generated legacy icons for $($cfg.Name) ($legacySize x $legacySize)"

    # 2. Adaptive Foreground Icon (ic_launcher_foreground.png)
    # This is a transparent canvas of AdaptiveSize x AdaptiveSize,
    # with the artwork scaled to ArtSize x ArtSize and centered,
    # and the background color #0F172A made transparent.
    $adaptiveSize = $cfg.AdaptiveSize
    $artSize = $cfg.ArtSize
    
    # First, scale the source image to ArtSize x ArtSize
    $bmpArt = New-Object System.Drawing.Bitmap($artSize, $artSize)
    $gArt = [System.Drawing.Graphics]::FromImage($bmpArt)
    $gArt.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gArt.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $gArt.DrawImage($src, 0, 0, $artSize, $artSize)
    $gArt.Dispose()
    
    # Make the background color transparent
    $bmpArt.MakeTransparent($bgColor)
    
    # Create the final adaptive foreground bitmap (AdaptiveSize x AdaptiveSize)
    $bmpForeground = New-Object System.Drawing.Bitmap($adaptiveSize, $adaptiveSize)
    $gForeground = [System.Drawing.Graphics]::FromImage($bmpForeground)
    $gForeground.Clear([System.Drawing.Color]::Transparent)
    
    # Center the artwork
    $offset = [int](($adaptiveSize - $artSize) / 2)
    $gForeground.DrawImage($bmpArt, $offset, $offset)
    $gForeground.Dispose()
    $bmpArt.Dispose()
    
    $fgPath = Join-Path $dir "ic_launcher_foreground.png"
    $bmpForeground.Save($fgPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmpForeground.Dispose()
    Write-Host "Generated adaptive foreground icon for $($cfg.Name) ($adaptiveSize x $adaptiveSize with $artSize x $artSize artwork)"
}

$src.Dispose()
Write-Host "All icons generated successfully!" -ForegroundColor Green
