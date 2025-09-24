# VoiceLite Project Cleanup Script
# Creates backups before cleaning to ensure safety

Write-Host "VoiceLite Project Cleanup Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Create backup directory with timestamp
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$backupDir = "backup_$timestamp"

Write-Host "`nCreating backup directory: $backupDir" -ForegroundColor Yellow
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

# Define what to keep and what to remove
$keepWhisperFiles = @(
    "whisper.exe",
    "whisper.dll",
    "ggml-small.bin",
    "clblast.dll",
    "libopenblas.dll"
)

$removeWhisperFiles = @(
    "bench.exe",
    "command.exe",
    "lsp.exe",
    "quantize.exe",
    "server.exe",
    "stream.exe",
    "talk.exe",
    "talk-llama.exe",
    "wchess.exe",
    "SDL2.dll",
    "ggml-tiny.bin",
    "ggml-base.bin",
    "ggml-medium.bin",
    "ggml-large-v3.bin"
)

$cleanupRootFiles = @(
    "ACCURACY_IMPROVEMENT_PLAN.md",
    "ACCURACY_IMPROVEMENT_PLAN_KISS.md",
    "icon-preview.html",
    "test_whisper.ps1",
    "VoiceLite_Implementation_Steps.md",
    "VoiceLite_UI_Enhancement_Guide.md"
)

$cleanupVoiceLiteFiles = @(
    "ARCHITECTURE_AUDIT_REPORT.md",
    "CODEBASE_ANALYSIS_REPORT.md",
    "COMPREHENSIVE_CODEBASE_ANALYSIS.cs",
    "convert-to-ico.ps1",
    "CreateIcon.cs",
    "create-icon.ps1",
    "create-microphone-icon.ps1",
    "create-simple-icon.ps1",
    "download-whisper.ps1",
    "git-commands.txt",
    "IMMEDIATE_FIXES.cs",
    "IMPLEMENTATION_ROADMAP.md",
    "PRESS_AND_HOLD_NOTES.md",
    "PTT_FIX_TEST_PROCEDURE.md",
    "RELEASE_BUILD_SUMMARY.md",
    "RELEASE_CHECKLIST.md",
    "RELEASE_READY_SUMMARY.md",
    "test-build.bat",
    "test-performance.ps1",
    "test-push-to-talk-fix.ps1",
    "test-recording.ps1",
    "test-whisper.ps1",
    "ULTRA_DETAILED_FINDINGS.cs",
    "VoiceLite-v2.5-Windows.zip"
)

# Function to safely move files
function Move-FileWithBackup {
    param($Source, $Destination)

    if (Test-Path $Source) {
        $destDir = Split-Path -Parent $Destination
        if (!(Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        Move-Item -Path $Source -Destination $Destination -Force
        return $true
    }
    return $false
}

# 1. Clean up Whisper directory
Write-Host "`n1. Cleaning Whisper directory..." -ForegroundColor Green
$whisperPath = "VoiceLite\whisper"

foreach ($file in $removeWhisperFiles) {
    $fullPath = Join-Path $whisperPath $file
    if (Test-Path $fullPath) {
        $backupPath = Join-Path $backupDir "whisper\$file"
        if (Move-FileWithBackup -Source $fullPath -Destination $backupPath) {
            Write-Host "   Backed up and removed: $file" -ForegroundColor Gray
        }
    }
}

# Calculate space saved from model files
$spaceSaved = 0
if (Test-Path "$whisperPath\ggml-large-v3.bin") {
    $spaceSaved += (Get-Item "$whisperPath\ggml-large-v3.bin").Length / 1GB
}
if (Test-Path "$whisperPath\ggml-medium.bin") {
    $spaceSaved += (Get-Item "$whisperPath\ggml-medium.bin").Length / 1GB
}
if (Test-Path "$whisperPath\ggml-base.bin") {
    $spaceSaved += (Get-Item "$whisperPath\ggml-base.bin").Length / 1MB
}
if (Test-Path "$whisperPath\ggml-tiny.bin") {
    $spaceSaved += (Get-Item "$whisperPath\ggml-tiny.bin").Length / 1MB
}

# 2. Clean up root directory files
Write-Host "`n2. Cleaning root directory files..." -ForegroundColor Green
foreach ($file in $cleanupRootFiles) {
    if (Test-Path $file) {
        $backupPath = Join-Path $backupDir "root\$file"
        if (Move-FileWithBackup -Source $file -Destination $backupPath) {
            Write-Host "   Backed up and removed: $file" -ForegroundColor Gray
        }
    }
}

# 3. Clean up VoiceLite directory files
Write-Host "`n3. Cleaning VoiceLite directory files..." -ForegroundColor Green
foreach ($file in $cleanupVoiceLiteFiles) {
    $fullPath = Join-Path "VoiceLite" $file
    if (Test-Path $fullPath) {
        $backupPath = Join-Path $backupDir "VoiceLite\$file"
        if (Move-FileWithBackup -Source $fullPath -Destination $backupPath) {
            Write-Host "   Backed up and removed: $file" -ForegroundColor Gray
            if ($file -eq "VoiceLite-v2.5-Windows.zip") {
                $spaceSaved += (Get-Item $fullPath).Length / 1MB
            }
        }
    }
}

# 4. Clean up Debug build artifacts
Write-Host "`n4. Cleaning Debug build artifacts..." -ForegroundColor Green
$debugPath = "VoiceLite\VoiceLite\bin\Debug"
if (Test-Path $debugPath) {
    $debugBackup = Join-Path $backupDir "Debug"
    Move-Item -Path $debugPath -Destination $debugBackup -Force
    Write-Host "   Backed up and removed Debug folder" -ForegroundColor Gray
}

# 5. Clean up duplicate VoiceLite-Release folder
Write-Host "`n5. Cleaning duplicate release folder..." -ForegroundColor Green
$duplicateRelease = "VoiceLite\VoiceLite-Release"
if (Test-Path $duplicateRelease) {
    $releaseBackup = Join-Path $backupDir "VoiceLite-Release"
    Move-Item -Path $duplicateRelease -Destination $releaseBackup -Force
    Write-Host "   Backed up and removed VoiceLite-Release folder" -ForegroundColor Gray
}

# 6. Report on remaining whisper files
Write-Host "`n6. Whisper directory status:" -ForegroundColor Green
Write-Host "   Files kept for application to work:" -ForegroundColor Cyan
foreach ($file in $keepWhisperFiles) {
    $fullPath = Join-Path $whisperPath $file
    if (Test-Path $fullPath) {
        $size = (Get-Item $fullPath).Length / 1MB
        Write-Host "   [OK] $file ($('{0:N2}' -f $size) MB)" -ForegroundColor Green
    }
}

# Summary
Write-Host "`n================================" -ForegroundColor Cyan
Write-Host "Cleanup Complete!" -ForegroundColor Green
Write-Host "Space saved: approximately $('{0:N2}' -f $spaceSaved) GB" -ForegroundColor Yellow
Write-Host "Backup created at: $backupDir" -ForegroundColor Yellow
Write-Host "`nTo restore any files, copy them back from the backup directory." -ForegroundColor Gray
Write-Host "Once you're sure everything works, you can delete the backup folder." -ForegroundColor Gray