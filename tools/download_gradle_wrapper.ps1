$url  = 'https://github.com/gradle/gradle/raw/v8.7.0/gradle/wrapper/gradle-wrapper.jar'
$dest = 'c:\Users\idham\Downloads\Devolopment\TEST APK\FOLD\android\gradle\wrapper\gradle-wrapper.jar'

try {
    Write-Host "Downloading gradle-wrapper.jar..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing -TimeoutSec 60
    $size = (Get-Item $dest).Length
    Write-Host "Downloaded! $size bytes -> $dest" -ForegroundColor Green
} catch {
    Write-Host "Could not download: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "Alternative: Open android/ in Android Studio - it auto-downloads the wrapper JAR."
}
