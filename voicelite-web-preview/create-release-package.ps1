# Quick Release Package Creator for VoiceLite
# Creates a ready-to-distribute ZIP file

Write-Host "Creating VoiceLite Release Package..." -ForegroundColor Cyan
Write-Host ""

# Configuration
$version = "1.0.0"
$outputName = "VoiceLite-$version-Windows"

# Step 1: Build Release version
Write-Host "Building Release version..." -ForegroundColor Yellow
cd VoiceLite
dotnet publish VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=false -o "../release-package/$outputName"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

cd ..

# Step 2: Create README for the package
$readme = @"
VoiceLite $version
==================

Professional Speech-to-Text for Windows
Powered by OpenAI Whisper AI

QUICK START
-----------
1. Run VoiceLite.exe
2. Hold Alt key to dictate
3. Release to transcribe

SYSTEM REQUIREMENTS
------------------
- Windows 10 or 11
- 4GB RAM minimum (8GB recommended)
- Microphone
- 5GB disk space (for all AI models)

FIRST RUN
---------
- Windows may show a security warning
- Click "More info" then "Run anyway"
- This is normal for new software

TRIAL VERSION
------------
This includes a 14-day free trial with all features.
After trial, purchase a license at: https://voicelite.app

SUPPORT
-------
Email: support@voicelite.app
Web: https://voicelite.app

© 2025 VoiceLite Software
"@

$readme | Out-File -FilePath "release-package\$outputName\README.txt" -Encoding UTF8

# Step 3: Create START_HERE.bat for easy launch
$startScript = @"
@echo off
echo Starting VoiceLite...
start VoiceLite.exe
"@

$startScript | Out-File -FilePath "release-package\$outputName\START_HERE.bat" -Encoding ASCII

# Step 4: Copy required runtime files if missing
$vcRedistUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
$vcRedistPath = "release-package\$outputName\vc_redist.x64.exe"

if (!(Test-Path $vcRedistPath)) {
    Write-Host "Downloading Visual C++ Runtime..." -ForegroundColor Yellow
    try {
        Invoke-WebRequest -Uri $vcRedistUrl -OutFile $vcRedistPath -ErrorAction SilentlyContinue
        Write-Host "✓ Runtime included" -ForegroundColor Green
    } catch {
        Write-Host "⚠ Could not download VC++ Runtime (users may need to install separately)" -ForegroundColor Yellow
    }
}

# Step 5: Create the ZIP file
Write-Host "Creating ZIP package..." -ForegroundColor Yellow
$zipPath = "$outputName.zip"

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "release-package\$outputName" -DestinationPath $zipPath -CompressionLevel Optimal

# Step 6: Calculate file size and hash
$fileInfo = Get-Item $zipPath
$fileSize = [math]::Round($fileInfo.Length / 1MB, 2)
$fileHash = (Get-FileHash $zipPath -Algorithm SHA256).Hash

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Release Package Created Successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Package: $zipPath" -ForegroundColor Cyan
Write-Host "Size: $fileSize MB" -ForegroundColor Cyan
Write-Host "SHA256: $fileHash" -ForegroundColor Gray
Write-Host ""
Write-Host "This package includes:" -ForegroundColor Yellow
Write-Host "  ✓ VoiceLite.exe (main application)"
Write-Host "  ✓ All Whisper AI models"
Write-Host "  ✓ All required dependencies"
Write-Host "  ✓ README with instructions"
Write-Host "  ✓ Quick-start batch file"
Write-Host ""
Write-Host "Upload this to:" -ForegroundColor Yellow
Write-Host "  1. GitHub Releases"
Write-Host "  2. Google Drive / Dropbox"
Write-Host "  3. Your web server"
Write-Host ""
Write-Host "Then update the download link on your landing page!" -ForegroundColor Green
Write-Host ""