# PowerShell script to integrate SimpleTelemetry into MainWindow.xaml.cs
# Run this after other Claude Code instances are done with their changes

$mainWindowPath = "VoiceLite\VoiceLite\MainWindow.xaml.cs"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "SimpleTelemetry Integration Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Check if file exists
if (-not (Test-Path $mainWindowPath)) {
    Write-Host "ERROR: MainWindow.xaml.cs not found at $mainWindowPath" -ForegroundColor Red
    exit 1
}

Write-Host "Reading MainWindow.xaml.cs..." -ForegroundColor Yellow
$content = Get-Content $mainWindowPath -Raw

# Backup original file
$backupPath = "$mainWindowPath.backup_" + (Get-Date -Format "yyyyMMdd_HHmmss")
Copy-Item $mainWindowPath $backupPath
Write-Host "Backup created: $backupPath" -ForegroundColor Green

# Change 1: Add telemetry field
Write-Host ""
Write-Host "[1/6] Adding telemetry field..." -ForegroundColor Yellow
$pattern1 = '(private RecordingCoordinator\? recordingCoordinator;)'
$replacement1 = '$1' + "`n        private SimpleTelemetry? telemetry; // Production telemetry for performance/reliability/usage tracking"
$content = $content -replace $pattern1, $replacement1

# Change 2: Initialize telemetry in MainWindow_Loaded
# This will be added after analyticsService initialization
Write-Host "[2/6] Adding telemetry initialization (requires manual placement)..." -ForegroundColor Yellow
Write-Host "  NOTE: You'll need to manually add these lines in MainWindow_Loaded:" -ForegroundColor Yellow
Write-Host "    telemetry = new SimpleTelemetry(settings);" -ForegroundColor Cyan
Write-Host "    telemetry.TrackAppStart();" -ForegroundColor Cyan
Write-Host "    telemetry.TrackDailyActiveUser();" -ForegroundColor Cyan

# Change 3: Track hotkey response (requires manual placement)
Write-Host "[3/6] Hotkey tracking (requires manual placement)..." -ForegroundColor Yellow
Write-Host "  NOTE: Add in OnHotkeyPressed method:" -ForegroundColor Yellow
Write-Host "    At start: telemetry?.TrackHotkeyResponseStart();" -ForegroundColor Cyan
Write-Host "    After recording starts: telemetry?.TrackHotkeyResponseEnd();" -ForegroundColor Cyan

# Change 4: Track transcription (requires manual placement)
Write-Host "[4/6] Transcription tracking (requires manual placement)..." -ForegroundColor Yellow
Write-Host "  NOTE: Add in OnTranscriptionCompleted method" -ForegroundColor Yellow

# Change 5: Track errors (requires manual placement)
Write-Host "[5/6] Error tracking (requires manual placement)..." -ForegroundColor Yellow
Write-Host "  NOTE: Add after ErrorLogger.LogError calls" -ForegroundColor Yellow

# Change 6: Add session end tracking
Write-Host "[6/6] Session end tracking (requires manual placement)..." -ForegroundColor Yellow
Write-Host "  NOTE: Add in MainWindow_Closing before disposing services:" -ForegroundColor Yellow
Write-Host "    telemetry?.TrackSessionEnd();" -ForegroundColor Cyan
Write-Host "    telemetry?.Dispose();" -ForegroundColor Cyan

# Write modified content
Write-Host ""
Write-Host "Writing changes to MainWindow.xaml.cs..." -ForegroundColor Yellow
Set-Content -Path $mainWindowPath -Value $content

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "Partial Integration Complete!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Cyan
Write-Host "1. Open MainWindow.xaml.cs in your editor" -ForegroundColor White
Write-Host "2. Follow the manual placement instructions above" -ForegroundColor White
Write-Host "3. Or use telemetry_integration.patch for reference" -ForegroundColor White
Write-Host "4. Build and test the application" -ForegroundColor White
Write-Host ""
Write-Host "If something goes wrong, restore from: $backupPath" -ForegroundColor Yellow
