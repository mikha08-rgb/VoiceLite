# VoiceLite Release Package Creator
# Creates a clean release package with all required files

$ErrorActionPreference = "Stop"

Write-Host "Creating VoiceLite Release Package..." -ForegroundColor Green

# Define paths
$releaseDir = ".\VoiceLite-Release"
$sourceDir = ".\VoiceLite\bin\Release\net8.0-windows"

# Clean previous release folder if exists
if (Test-Path $releaseDir) {
    Write-Host "Cleaning previous release folder..." -ForegroundColor Yellow
    Remove-Item $releaseDir -Recurse -Force
}

# Create release folder structure
Write-Host "Creating release folder structure..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
New-Item -ItemType Directory -Path "$releaseDir\whisper" -Force | Out-Null
New-Item -ItemType Directory -Path "$releaseDir\temp" -Force | Out-Null

# Copy main executable and dependencies
Write-Host "Copying application files..." -ForegroundColor Cyan
$filesToCopy = @(
    "VoiceLite.exe",
    "VoiceLite.dll",
    "VoiceLite.runtimeconfig.json",
    "VoiceLite.deps.json",
    "VoiceLite.ico",
    "settings.json",
    "*.dll"  # All DLL dependencies
)

foreach ($pattern in $filesToCopy) {
    $files = Get-ChildItem -Path $sourceDir -Filter $pattern -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        Copy-Item $file.FullName -Destination $releaseDir -Force
        Write-Host "  Copied: $($file.Name)" -ForegroundColor Gray
    }
}

# Copy runtime folders if they exist
if (Test-Path "$sourceDir\runtimes") {
    Write-Host "Copying runtime dependencies..." -ForegroundColor Cyan
    Copy-Item "$sourceDir\runtimes" -Destination $releaseDir -Recurse -Force
}

# Copy Whisper files (only required ones)
Write-Host "Copying Whisper components..." -ForegroundColor Cyan
$whisperFiles = @(
    "whisper.exe",
    "whisper.dll",
    "SDL2.dll",
    "libopenblas.dll",
    "clblast.dll"
)

foreach ($file in $whisperFiles) {
    $sourcePath = "$sourceDir\whisper\$file"
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath -Destination "$releaseDir\whisper\" -Force
        Write-Host "  Copied: whisper\$file" -ForegroundColor Gray
    }
}

# Copy Whisper models (ask user which ones to include)
Write-Host "`nSelect Whisper models to include:" -ForegroundColor Yellow
Write-Host "1. Tiny (78 MB) - Fastest, lowest accuracy"
Write-Host "2. Small (488 MB) - Recommended balance"
Write-Host "3. Medium (1.5 GB) - Better accuracy"
Write-Host "4. Large (3.1 GB) - Best accuracy"
Write-Host "5. All models"
Write-Host "Enter choices (e.g., '2' for small only, '2,3' for small and medium):" -ForegroundColor Cyan

$choice = Read-Host
$selectedModels = @()

if ($choice -contains "1" -or $choice -contains "5") { $selectedModels += "ggml-tiny.bin" }
if ($choice -contains "2" -or $choice -contains "5") { $selectedModels += "ggml-small.bin", "ggml-small.en.bin" }
if ($choice -contains "3" -or $choice -contains "5") { $selectedModels += "ggml-medium.bin", "ggml-medium.en.bin" }
if ($choice -contains "4" -or $choice -contains "5") { $selectedModels += "ggml-large-v3.bin" }

# Default to small if no selection
if ($selectedModels.Count -eq 0) {
    $selectedModels = @("ggml-small.bin")
    Write-Host "No selection made, defaulting to small model" -ForegroundColor Yellow
}

Write-Host "Copying selected Whisper models..." -ForegroundColor Cyan
foreach ($model in $selectedModels) {
    $sourcePath = "$sourceDir\whisper\$model"
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath -Destination "$releaseDir\whisper\" -Force
        $size = [math]::Round((Get-Item $sourcePath).Length / 1MB, 2)
        Write-Host "  Copied: $model ($size MB)" -ForegroundColor Gray
    }
}

# Create README file
Write-Host "Creating README..." -ForegroundColor Cyan
@"
# VoiceLite - Speech-to-Text for Windows

## Installation
1. Extract all files to a folder (e.g., C:\Program Files\VoiceLite)
2. Run VoiceLite.exe
3. The app will minimize to system tray

## Usage
- Press F1 (hold) to record and transcribe
- Right-click system tray icon for options
- Configure settings through the Settings window

## Requirements
- Windows 10/11
- .NET 8.0 Runtime (will prompt to install if missing)
- Microphone

## Troubleshooting
If the app doesn't start:
1. Ensure .NET 8.0 Desktop Runtime is installed
2. Check Windows Defender/Antivirus isn't blocking it
3. Run as Administrator if needed

## Features
- Global hotkey (F1) works in any application
- High accuracy with Whisper AI
- Low latency (<200ms)
- Push-to-talk or toggle recording modes
- System tray integration

Version: 2.5
"@ | Out-File -FilePath "$releaseDir\README.txt" -Encoding UTF8

# Calculate total size
$totalSize = (Get-ChildItem -Path $releaseDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "`nRelease package created successfully!" -ForegroundColor Green
Write-Host "Location: $releaseDir" -ForegroundColor Cyan
Write-Host "Total Size: $([math]::Round($totalSize, 2)) MB" -ForegroundColor Cyan
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Test the release package"
Write-Host "2. Create a ZIP file for distribution"
Write-Host "3. Consider creating an installer (e.g., with Inno Setup)"