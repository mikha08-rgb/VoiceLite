# PowerShell script to download Whisper.cpp and models for VoiceLite

Write-Host "=== VoiceLite Whisper Setup ===" -ForegroundColor Cyan
Write-Host ""

$whisperDir = "VoiceLite\whisper"

# Create directory if it doesn't exist
if (!(Test-Path $whisperDir)) {
    New-Item -ItemType Directory -Force -Path $whisperDir | Out-Null
    Write-Host "Created whisper directory at $whisperDir" -ForegroundColor Green
}

# Function to download with progress
function Download-WithProgress {
    param(
        [string]$Url,
        [string]$OutputPath,
        [string]$Description
    )

    Write-Host "Downloading $Description..." -ForegroundColor Yellow
    Write-Host "From: $Url" -ForegroundColor Gray
    Write-Host "To: $OutputPath" -ForegroundColor Gray

    try {
        $ProgressPreference = 'Continue'
        Invoke-WebRequest -Uri $Url -OutFile $OutputPath -UseBasicParsing
        Write-Host "✓ Successfully downloaded $Description" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "✗ Failed to download $Description" -ForegroundColor Red
        Write-Host "Error: $_" -ForegroundColor Red
        return $false
    }
}

# Download Whisper.cpp Windows binary
Write-Host ""
Write-Host "Step 1: Downloading Whisper.cpp Windows Binary" -ForegroundColor Cyan
Write-Host "-----------------------------------------------" -ForegroundColor Gray

# Using the latest release from whisper.cpp GitHub
# Note: You might need to update this URL to the latest version
$whisperExeUrl = "https://github.com/ggerganov/whisper.cpp/releases/download/v1.5.4/whisper-blas-clblast-bin-x64.zip"
$whisperZipPath = "$whisperDir\whisper-bin.zip"

if (Download-WithProgress -Url $whisperExeUrl -OutputPath $whisperZipPath -Description "Whisper.cpp binary package") {
    Write-Host "Extracting whisper.exe..." -ForegroundColor Yellow

    try {
        # Extract the zip file
        Expand-Archive -Path $whisperZipPath -DestinationPath $whisperDir -Force

        # Find and move whisper.exe to the correct location
        $whisperExe = Get-ChildItem -Path $whisperDir -Filter "main.exe" -Recurse | Select-Object -First 1
        if ($whisperExe) {
            Move-Item -Path $whisperExe.FullName -Destination "$whisperDir\whisper.exe" -Force
            Write-Host "✓ Whisper.exe installed successfully" -ForegroundColor Green
        } else {
            Write-Host "Note: main.exe not found in archive. You may need to manually rename the executable to whisper.exe" -ForegroundColor Yellow
        }

        # Clean up zip file
        Remove-Item $whisperZipPath -Force
    }
    catch {
        Write-Host "Error extracting whisper binary: $_" -ForegroundColor Red
    }
}

# Download Whisper models
Write-Host ""
Write-Host "Step 2: Downloading Whisper AI Models" -ForegroundColor Cyan
Write-Host "--------------------------------------" -ForegroundColor Gray

$models = @{
    "ggml-small.bin" = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin"
    "ggml-medium.bin" = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin"
    "ggml-large-v3.bin" = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin"
}

# Ask user which models to download
Write-Host ""
Write-Host "Available models:" -ForegroundColor White
Write-Host "1. Small (466 MB) - Recommended for testing" -ForegroundColor Gray
Write-Host "2. Medium (1.42 GB) - Better accuracy" -ForegroundColor Gray
Write-Host "3. Large (2.87 GB) - Best accuracy" -ForegroundColor Gray
Write-Host "4. All models" -ForegroundColor Gray
Write-Host ""

$choice = Read-Host "Which model(s) would you like to download? (1-4)"

$modelsToDownload = @()
switch ($choice) {
    "1" { $modelsToDownload = @("ggml-small.bin") }
    "2" { $modelsToDownload = @("ggml-medium.bin") }
    "3" { $modelsToDownload = @("ggml-large-v3.bin") }
    "4" { $modelsToDownload = $models.Keys }
    default {
        Write-Host "Defaulting to small model..." -ForegroundColor Yellow
        $modelsToDownload = @("ggml-small.bin")
    }
}

foreach ($modelName in $modelsToDownload) {
    $modelUrl = $models[$modelName]
    $modelPath = "$whisperDir\$modelName"

    if (Test-Path $modelPath) {
        Write-Host "Model $modelName already exists, skipping..." -ForegroundColor Gray
        continue
    }

    Write-Host ""
    Download-WithProgress -Url $modelUrl -OutputPath $modelPath -Description $modelName
}

# Verify installation
Write-Host ""
Write-Host "Step 3: Verifying Installation" -ForegroundColor Cyan
Write-Host "-------------------------------" -ForegroundColor Gray

$whisperExePath = "$whisperDir\whisper.exe"
$smallModelPath = "$whisperDir\ggml-small.bin"

if (Test-Path $whisperExePath) {
    Write-Host "✓ whisper.exe found" -ForegroundColor Green

    # Try to get version info
    try {
        & $whisperExePath --help | Select-Object -First 1
    }
    catch {
        Write-Host "Note: Could not verify whisper.exe version" -ForegroundColor Yellow
    }
} else {
    Write-Host "✗ whisper.exe not found at $whisperExePath" -ForegroundColor Red
    Write-Host "You may need to manually download and place whisper.exe in the whisper directory" -ForegroundColor Yellow
}

if (Test-Path $smallModelPath) {
    $size = (Get-Item $smallModelPath).Length / 1MB
    Write-Host "✓ ggml-small.bin found (${size:N0} MB)" -ForegroundColor Green
} else {
    Write-Host "✗ ggml-small.bin not found" -ForegroundColor Red
}

# Final summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "1. If whisper.exe was not found, download it manually from:" -ForegroundColor Gray
Write-Host "   https://github.com/ggerganov/whisper.cpp/releases" -ForegroundColor Blue
Write-Host "2. Run the tests to verify everything works:" -ForegroundColor Gray
Write-Host "   dotnet test VoiceLite.Tests" -ForegroundColor Yellow
Write-Host ""