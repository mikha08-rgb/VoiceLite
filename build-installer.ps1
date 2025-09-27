# Build script for VoiceLite installer
# This script builds the release version and creates an installer

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VoiceLite Installer Build Script" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build the release version
Write-Host "Step 1: Building Release version..." -ForegroundColor Yellow
cd VoiceLite
dotnet publish VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained -o publish

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Build completed successfully" -ForegroundColor Green
Write-Host ""

# Step 2: Check if Inno Setup is installed
$innoPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (!(Test-Path $innoPath)) {
    Write-Host "Inno Setup not found!" -ForegroundColor Red
    Write-Host "Please install Inno Setup from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternative: Creating ZIP package instead..." -ForegroundColor Yellow

    # Create ZIP package as fallback
    $zipName = "VoiceLite-1.0.0-Windows.zip"
    Write-Host "Creating $zipName..." -ForegroundColor Yellow

    # Ensure the publish directory exists
    if (Test-Path "publish") {
        Compress-Archive -Path "publish\*" -DestinationPath $zipName -Force
        Write-Host "✓ ZIP package created: $zipName" -ForegroundColor Green
        Write-Host "  Size: $((Get-Item $zipName).Length / 1MB) MB" -ForegroundColor Gray
    } else {
        Write-Host "Publish directory not found!" -ForegroundColor Red
        exit 1
    }
} else {
    # Step 3: Build installer with Inno Setup
    Write-Host "Step 2: Building installer with Inno Setup..." -ForegroundColor Yellow
    & $innoPath "VoiceLite.iss"

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Installer created successfully" -ForegroundColor Green
        $installerPath = "Installer\VoiceLite-Setup-1.0.0.exe"
        if (Test-Path $installerPath) {
            Write-Host "  Location: $installerPath" -ForegroundColor Gray
            Write-Host "  Size: $((Get-Item $installerPath).Length / 1MB) MB" -ForegroundColor Gray
        }
    } else {
        Write-Host "Installer build failed!" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Test the installer/ZIP locally" -ForegroundColor White
Write-Host "2. Upload to GitHub Releases" -ForegroundColor White
Write-Host "3. Update download link on landing page" -ForegroundColor White
Write-Host ""