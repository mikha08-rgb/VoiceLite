# VoiceLite Release Build Script
# This script creates a complete, ready-to-run release package with all dependencies

param(
    [string]$Configuration = "Release",
    [string]$ModelSize = "small"  # tiny, base, small, medium, or large
)

$ErrorActionPreference = "Stop"

Write-Host "VoiceLite Release Builder" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

# Set paths
$projectDir = $PSScriptRoot
$voiceLiteProject = Join-Path $projectDir "VoiceLite\VoiceLite\VoiceLite.csproj"
$whisperDir = Join-Path $projectDir "VoiceLite\whisper"
$outputDir = Join-Path $projectDir "VoiceLite-Release"

# Check if project exists
if (-not (Test-Path $voiceLiteProject)) {
    Write-Error "VoiceLite project not found at: $voiceLiteProject"
    exit 1
}

# Clean output directory
if (Test-Path $outputDir) {
    Write-Host "Cleaning output directory..." -ForegroundColor Yellow
    Remove-Item $outputDir -Recurse -Force
}

# Build the project
Write-Host "`nBuilding VoiceLite..." -ForegroundColor Green
dotnet publish $voiceLiteProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -o $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

# Ensure whisper directory exists in output
$outputWhisperDir = Join-Path $outputDir "whisper"
if (-not (Test-Path $outputWhisperDir)) {
    New-Item -ItemType Directory -Path $outputWhisperDir | Out-Null
}

# Copy whisper.exe and required DLLs
Write-Host "`nCopying Whisper components..." -ForegroundColor Green

$whisperFiles = @(
    "whisper.exe",
    "whisper.dll",
    "clblast.dll",
    "libopenblas.dll"
)

foreach ($file in $whisperFiles) {
    $sourcePath = Join-Path $whisperDir $file
    $destPath = Join-Path $outputWhisperDir $file

    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath $destPath -Force
        Write-Host "  - Copied: $file" -ForegroundColor Gray
    } else {
        Write-Warning "  - Missing: $file (this may be optional)"
    }
}

# Copy the selected model
Write-Host "`nCopying Whisper model ($ModelSize)..." -ForegroundColor Green

$modelFile = switch ($ModelSize) {
    "tiny"   { "ggml-tiny.bin" }
    "base"   { "ggml-base.bin" }
    "small"  { "ggml-small.bin" }
    "medium" { "ggml-medium.bin" }
    "large"  { "ggml-large-v3.bin" }
    default  { "ggml-small.bin" }
}

$modelSource = Join-Path $whisperDir $modelFile
$modelDest = Join-Path $outputWhisperDir $modelFile

if (Test-Path $modelSource) {
    Copy-Item $modelSource $modelDest -Force
    $modelSizeMB = [math]::Round((Get-Item $modelDest).Length / 1MB, 2)
    Write-Host "  - Model: $modelFile ($modelSizeMB MB)" -ForegroundColor Gray
} else {
    Write-Error "Model file not found: $modelSource"
    Write-Host "Please ensure the Whisper models are downloaded to: $whisperDir"
    exit 1
}

# Create README for users
Write-Host "`nCreating user documentation..." -ForegroundColor Green

$readmeContent = @"
===========================================
VoiceLite - Voice Typing for Windows
===========================================

QUICK START:
1. Run VoiceLite.exe
2. Hold Left Alt key to record
3. Release to transcribe and type

FIRST TIME SETUP:
- If you see an error about missing components, VoiceLite will
  automatically prompt you to install them.
- Just click "Yes" when asked to install Visual C++ Runtime.

TROUBLESHOOTING:
- If speech recognition doesn't work:
  1. VoiceLite will auto-detect and offer to install missing components
  2. If auto-install fails, manually download Visual C++ Runtime from:
     https://aka.ms/vs/17/release/vc_redist.x64.exe

- If you see "whisper.exe not found":
  Make sure the 'whisper' folder is in the same directory as VoiceLite.exe

FEATURES:
- Hold Left Alt (or custom key) to record
- Works in any Windows application
- 100% offline - no internet required
- Multiple accuracy models available

SETTINGS:
- Right-click system tray icon -> Settings
- Customize hotkey, accuracy model, and more

Model: $modelFile
Version: $Configuration Build
Date: $(Get-Date -Format "yyyy-MM-dd")

For support: https://github.com/YourUsername/VoiceLite
"@

Set-Content -Path (Join-Path $outputDir "README.txt") -Value $readmeContent

# Create a batch file for easy startup with error checking
Write-Host "Creating startup helper..." -ForegroundColor Green

$launcherContent = @'
@echo off
title VoiceLite Launcher

echo Starting VoiceLite...
echo.

REM Check if VoiceLite.exe exists
if not exist "%~dp0VoiceLite.exe" (
    echo ERROR: VoiceLite.exe not found!
    echo Please ensure all files were extracted correctly.
    pause
    exit /b 1
)

REM Check if whisper folder exists
if not exist "%~dp0whisper\whisper.exe" (
    echo WARNING: Whisper components not found!
    echo VoiceLite may not work properly.
    echo.
)

REM Start VoiceLite
start "" "%~dp0VoiceLite.exe"

REM Check if it started
if errorlevel 1 (
    echo.
    echo ERROR: Failed to start VoiceLite
    echo.
    echo Common causes:
    echo - Missing Visual C++ Runtime (VoiceLite will prompt to install)
    echo - Antivirus blocking the application
    echo - Missing .NET Runtime
    echo.
    echo Press any key to exit...
    pause > nul
)
'@

Set-Content -Path (Join-Path $outputDir "Start-VoiceLite.bat") -Value $launcherContent

# Create PowerShell diagnostic script
Write-Host "Creating diagnostic tool..." -ForegroundColor Green

$diagnosticContent = @'
# VoiceLite Diagnostic Tool
Write-Host "VoiceLite Diagnostic Tool" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

$issues = @()

# Check 1: VoiceLite.exe
Write-Host "Checking VoiceLite.exe... " -NoNewline
if (Test-Path "$PSScriptRoot\VoiceLite.exe") {
    Write-Host "OK" -ForegroundColor Green
} else {
    Write-Host "MISSING" -ForegroundColor Red
    $issues += "VoiceLite.exe not found"
}

# Check 2: Whisper.exe
Write-Host "Checking whisper.exe... " -NoNewline
if (Test-Path "$PSScriptRoot\whisper\whisper.exe") {
    Write-Host "OK" -ForegroundColor Green

    # Try to run it
    Write-Host "Testing whisper.exe... " -NoNewline
    try {
        $proc = Start-Process -FilePath "$PSScriptRoot\whisper\whisper.exe" `
                             -ArgumentList "--help" `
                             -NoNewWindow `
                             -PassThru `
                             -RedirectStandardOutput "NUL" `
                             -RedirectStandardError "NUL" `
                             -Wait

        if ($proc.ExitCode -eq 0 -or $proc.ExitCode -eq 1) {
            Write-Host "OK" -ForegroundColor Green
        } else {
            Write-Host "FAILED" -ForegroundColor Red
            $issues += "whisper.exe cannot run (likely missing VC++ Runtime)"
        }
    } catch {
        Write-Host "ERROR" -ForegroundColor Red
        $issues += "whisper.exe missing dependencies"
    }
} else {
    Write-Host "MISSING" -ForegroundColor Red
    $issues += "whisper.exe not found"
}

# Check 3: Model files
Write-Host "Checking AI models... " -NoNewline
$models = Get-ChildItem "$PSScriptRoot\whisper\*.bin" -ErrorAction SilentlyContinue
if ($models) {
    Write-Host "OK ($($models.Count) found)" -ForegroundColor Green
} else {
    Write-Host "MISSING" -ForegroundColor Red
    $issues += "No AI model files found"
}

# Check 4: .NET Runtime
Write-Host "Checking .NET Runtime... " -NoNewline
try {
    $dotnet = & dotnet --list-runtimes 2>$null | Where-Object { $_ -match "Microsoft.WindowsDesktop.App 8" }
    if ($dotnet) {
        Write-Host "OK" -ForegroundColor Green
    } else {
        Write-Host "NOT FOUND" -ForegroundColor Yellow
        $issues += ".NET 8 Desktop Runtime may be needed"
    }
} catch {
    Write-Host "UNKNOWN" -ForegroundColor Yellow
}

# Check 5: VC++ Runtime
Write-Host "Checking Visual C++ Runtime... " -NoNewline
$vcFound = $false

# Check common VC++ DLL
$systemDir = [System.Environment]::SystemDirectory
if (Test-Path "$systemDir\VCRUNTIME140.dll") {
    $vcFound = $true
}

# Check registry
$vcKeys = @(
    "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
)

foreach ($key in $vcKeys) {
    if (Test-Path $key) {
        $installed = Get-ItemProperty $key -Name Installed -ErrorAction SilentlyContinue
        if ($installed.Installed -eq 1) {
            $vcFound = $true
            break
        }
    }
}

if ($vcFound) {
    Write-Host "OK" -ForegroundColor Green
} else {
    Write-Host "NOT FOUND" -ForegroundColor Red
    $issues += "Visual C++ Runtime not installed"
}

# Summary
Write-Host ""
Write-Host "Diagnostic Summary" -ForegroundColor Cyan
Write-Host "==================" -ForegroundColor Cyan

if ($issues.Count -eq 0) {
    Write-Host "All checks passed! VoiceLite should work correctly." -ForegroundColor Green
} else {
    Write-Host "Issues found:" -ForegroundColor Yellow
    foreach ($issue in $issues) {
        Write-Host "  - $issue" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Solutions:" -ForegroundColor Cyan

    if ($issues -match "VC\+\+ Runtime") {
        Write-Host "  1. Run VoiceLite.exe - it will auto-install missing components" -ForegroundColor Green
        Write-Host "  2. Or download manually: https://aka.ms/vs/17/release/vc_redist.x64.exe" -ForegroundColor Gray
    }

    if ($issues -match "\.NET") {
        Write-Host "  - Download .NET 8 Desktop Runtime from:" -ForegroundColor Gray
        Write-Host "    https://dotnet.microsoft.com/download/dotnet/8.0/runtime" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
'@

Set-Content -Path (Join-Path $outputDir "Diagnose-VoiceLite.ps1") -Value $diagnosticContent

# Calculate total size
$totalSize = (Get-ChildItem $outputDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
$totalSize = [math]::Round($totalSize, 2)

# Success message
Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host "Output: $outputDir" -ForegroundColor Cyan
Write-Host "Total Size: $totalSize MB" -ForegroundColor Cyan
Write-Host "Model: $modelFile" -ForegroundColor Cyan
Write-Host ""
Write-Host "Package includes:" -ForegroundColor Gray
Write-Host "  - VoiceLite.exe (main application)" -ForegroundColor Gray
Write-Host "  - Whisper components (speech engine)" -ForegroundColor Gray
Write-Host "  - AI Model ($ModelSize size)" -ForegroundColor Gray
Write-Host "  - Auto-installer for missing components" -ForegroundColor Gray
Write-Host "  - Diagnostic tool" -ForegroundColor Gray
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Test the build by running: $outputDir\Start-VoiceLite.bat"
Write-Host "2. Create ZIP file for distribution"
Write-Host "3. Upload to GitHub Releases"