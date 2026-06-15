Add-Type -AssemblyName System.Drawing

function Make-Bitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(15, 15, 15))

    $blue  = [System.Drawing.Color]::FromArgb(66, 133, 244)
    $pen   = New-Object System.Drawing.Pen($blue, [float][math]::Max(1.0, $size / 16.0))
    $brush = New-Object System.Drawing.SolidBrush($blue)

    $pad  = [int]($size * 0.12)
    $mH   = [int]($size * 0.55)
    $g.DrawRectangle($pen, $pad, $pad, $size - $pad * 2 - 1, $mH)

    $dotSz = [math]::Max(2.0, $size / 8.0)
    $dotX  = [int]($size * 0.5 - $dotSz / 2)
    $dotY  = [int]($size * 0.78 - $dotSz / 2)
    $g.FillEllipse($brush, $dotX, $dotY, $dotSz, $dotSz)

    $g.Dispose(); $pen.Dispose(); $brush.Dispose()
    return $bmp
}

$b16 = Make-Bitmap 16
$b32 = Make-Bitmap 32

$dir = "c:\Users\idham\Downloads\Devolopment\TEST APK\FOLD\windows\FOLD\Resources"
if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

$outPath = Join-Path $dir "tray_icon.ico"

$ms16 = New-Object System.IO.MemoryStream
$ms32 = New-Object System.IO.MemoryStream
$b16.Save($ms16, [System.Drawing.Imaging.ImageFormat]::Png)
$b32.Save($ms32, [System.Drawing.Imaging.ImageFormat]::Png)
$png16 = $ms16.ToArray()
$png32 = $ms32.ToArray()

$ico = New-Object System.IO.MemoryStream
$w   = New-Object System.IO.BinaryWriter($ico)

# ICONDIR header
$w.Write([int16]0)   # Reserved
$w.Write([int16]1)   # Type ICO
$w.Write([int16]2)   # Count

# Offsets: 6 (header) + 2x16 (entries) = 38
$offset1 = 38
$offset2 = $offset1 + $png16.Length

# Entry 1: 16x16
$w.Write([byte]16); $w.Write([byte]16); $w.Write([byte]0); $w.Write([byte]0)
$w.Write([int16]1); $w.Write([int16]32)
$w.Write([int32]$png16.Length); $w.Write([int32]$offset1)

# Entry 2: 32x32
$w.Write([byte]32); $w.Write([byte]32); $w.Write([byte]0); $w.Write([byte]0)
$w.Write([int16]1); $w.Write([int16]32)
$w.Write([int32]$png32.Length); $w.Write([int32]$offset2)

$w.Write($png16)
$w.Write($png32)
$w.Flush()

[System.IO.File]::WriteAllBytes($outPath, $ico.ToArray())
Write-Host "tray_icon.ico created ($($ico.Length) bytes) at: $outPath" -ForegroundColor Green
