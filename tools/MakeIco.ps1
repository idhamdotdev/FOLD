Add-Type -AssemblyName System.Drawing

$pngPath = 'c:\Users\idham\Downloads\Devolopment\TEST APK\FOLD\icon\windows.png'
$icoPath = 'c:\Users\idham\Downloads\Devolopment\TEST APK\FOLD\windows\FOLD\Resources\tray_icon.ico'

# Resize to 256x256
$src = [System.Drawing.Image]::FromFile($pngPath)
$bmp = New-Object System.Drawing.Bitmap(256, 256)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($src, 0, 0, 256, 256)
$g.Dispose()
$src.Dispose()

# Save PNG bytes to memory
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$pngBytes = $ms.ToArray()
$ms.Dispose()

# Build ICO file manually
# ICO header: 6 bytes
# ICONDIRENTRY: 16 bytes
# PNG data follows at offset 22

$out = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter($out)

# ICONDIR header
$bw.Write([uint16]0)   # reserved
$bw.Write([uint16]1)   # type = icon
$bw.Write([uint16]1)   # count = 1 image

# ICONDIRENTRY
$bw.Write([byte]0)     # width  (0 = 256)
$bw.Write([byte]0)     # height (0 = 256)
$bw.Write([byte]0)     # color count
$bw.Write([byte]0)     # reserved
$bw.Write([uint16]1)   # planes
$bw.Write([uint16]32)  # bit count
$bw.Write([uint32]$pngBytes.Length)  # size of image data
$bw.Write([uint32]22)  # offset to image data (6 + 16 = 22)

# PNG image data
$bw.Write($pngBytes)
$bw.Flush()

[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
$bw.Dispose()
$out.Dispose()

Write-Host "ICO written to: $icoPath  ($($pngBytes.Length) bytes)"
