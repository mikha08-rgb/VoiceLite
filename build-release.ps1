# VoiceLite Release Build Script
# Produces a self-contained release package. As of v2.0.0 the speech engine is
# Sherpa-ONNX + Parakeet v3 (in-process via NuGet). No whisper.exe, no GGML
# models — the Parakeet model is downloaded on first launch (~640MB).

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "VoiceLite Release Builder" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

$projectDir = $PSScriptRoot
$voiceLiteProject = Join-Path $projectDir "VoiceLite\VoiceLite\VoiceLite.csproj"
$outputDir = Join-Path $projectDir "VoiceLite-Release"

if (-not (Test-Path $voiceLiteProject)) {
    Write-Error "VoiceLite project not found at: $voiceLiteProject"
    exit 1
}

if (Test-Path $outputDir) {
    Write-Host "Cleaning output directory..." -ForegroundColor Yellow
    Remove-Item $outputDir -Recurse -Force
}

# Stale bin/obj DLLs caused the v2.1.1 type-init crash on launch — a clean
# publish from scratch is mandatory for release builds (see CLAUDE.md).
Write-Host "Cleaning bin/obj (stale-DLL footgun)..." -ForegroundColor Yellow
foreach ($dir in @("VoiceLite\VoiceLite\bin", "VoiceLite\VoiceLite\obj")) {
    $full = Join-Path $projectDir $dir
    if (Test-Path $full) { Remove-Item $full -Recurse -Force }
}

Write-Host "`nBuilding VoiceLite ($Configuration, win-x64, self-contained)..." -ForegroundColor Green
dotnet publish $voiceLiteProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -o $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

# Ship third-party license texts alongside the binary
$licensesSrc = Join-Path $projectDir "VoiceLite\LICENSES"
$licensesDest = Join-Path $outputDir "LICENSES"
if (Test-Path $licensesSrc) {
    Write-Host "`nCopying third-party license texts..." -ForegroundColor Green
    Copy-Item $licensesSrc $licensesDest -Recurse -Force
} else {
    Write-Warning "LICENSES directory not found at $licensesSrc — NVIDIA Parakeet CC-BY-4.0 attribution must ship with the binary."
}

# User-facing README
$readmeContent = @"
===========================================
VoiceLite - Voice Typing for Windows
===========================================

QUICK START:
1. Run VoiceLite.exe
2. On first launch, download the Parakeet v3 speech model (~640MB)
3. Hold the hotkey to record, release to transcribe

FIRST TIME SETUP:
- If you see an error about missing components, install the
  Visual C++ Redistributable from: https://aka.ms/vs/17/release/vc_redist.x64.exe
- The Parakeet model downloads automatically on first launch.

TROUBLESHOOTING:
- If transcription fails to start, ensure VC++ Runtime is installed.
- If the model download fails, retry from Settings > AI Models.

FEATURES:
- Hold hotkey (default Shift+Z) to record
- Works in any Windows application
- 100% offline after first-launch model download
- Powered by NVIDIA Parakeet TDT 0.6B v3 via Sherpa-ONNX

Engine: Sherpa-ONNX + Parakeet v3 (int8)
Version: $Configuration Build
Date: $(Get-Date -Format "yyyy-MM-dd")

For support: https://github.com/mikha08-rgb/VoiceLite
"@

Set-Content -Path (Join-Path $outputDir "README.txt") -Value $readmeContent

# Total size
$totalSize = (Get-ChildItem $outputDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
$totalSize = [math]::Round($totalSize, 2)

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host "Output: $outputDir" -ForegroundColor Cyan
Write-Host "Total Size: $totalSize MB" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Run the installer build: build-installer.ps1"
Write-Host "2. Upload the resulting installer to GitHub Releases"
