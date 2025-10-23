# Quick ZIP Package Creator for VoiceLite
# Creates a ready-to-distribute ZIP file from existing build

Write-Host "Creating VoiceLite ZIP Package..." -ForegroundColor Cyan
Write-Host ""

# Configuration
$version = "1.0.0"
$sourcePath = "VoiceLite\VoiceLite\bin\Release\net8.0-windows\win-x64\publish"
$outputName = "VoiceLite-$version-Windows.zip"

# Check if publish directory exists
if (!(Test-Path $sourcePath)) {
    Write-Host "Published build not found at: $sourcePath" -ForegroundColor Red
    Write-Host "Building release version first..." -ForegroundColor Yellow

    cd VoiceLite
    dotnet publish VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained -o bin\Release\net8.0-windows\win-x64\publish
    cd ..

    if (!(Test-Path $sourcePath)) {
        Write-Host "Build failed or output path incorrect!" -ForegroundColor Red
        exit 1
    }
}

# Create the ZIP file
Write-Host "Creating ZIP from: $sourcePath" -ForegroundColor Yellow

if (Test-Path $outputName) {
    Remove-Item $outputName -Force
}

Compress-Archive -Path "$sourcePath\*" -DestinationPath $outputName -CompressionLevel Optimal

# Calculate file info
$fileInfo = Get-Item $outputName
$fileSize = [math]::Round($fileInfo.Length / 1MB, 2)
$fileHash = (Get-FileHash $outputName -Algorithm SHA256).Hash.Substring(0, 8)

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "ZIP Package Created Successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "File: $outputName" -ForegroundColor Cyan
Write-Host "Size: $fileSize MB" -ForegroundColor Cyan
Write-Host "Hash: $fileHash..." -ForegroundColor Gray
Write-Host ""
Write-Host "This package includes:" -ForegroundColor Yellow
Write-Host "  ✓ VoiceLite.exe"
Write-Host "  ✓ All Whisper AI models"
Write-Host "  ✓ All dependencies"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Upload to GitHub Releases or cloud storage"
Write-Host "2. Update download link on landing page"
Write-Host "3. Test the package on a clean Windows machine"
Write-Host ""