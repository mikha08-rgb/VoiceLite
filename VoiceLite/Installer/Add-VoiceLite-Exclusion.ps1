# Add VoiceLite to Windows Defender Exclusions
# This script helps fix antivirus blocking issues

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VoiceLite Antivirus Exclusion Helper" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: This script requires administrator privileges." -ForegroundColor Red
    Write-Host ""
    Write-Host "Please:" -ForegroundColor Yellow
    Write-Host "1. Right-click on this shortcut" -ForegroundColor Yellow
    Write-Host "2. Select 'Run as administrator'" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

# Get VoiceLite installation path
$installPath = Split-Path -Parent $PSScriptRoot
if (-not $installPath) {
    $installPath = "${env:ProgramFiles}\VoiceLite"
}

Write-Host "VoiceLite installation detected at:" -ForegroundColor Green
Write-Host "  $installPath" -ForegroundColor White
Write-Host ""

# Check if Windows Defender is available
try {
    $defenderStatus = Get-MpComputerStatus -ErrorAction Stop
    Write-Host "Windows Defender detected and running." -ForegroundColor Green
} catch {
    Write-Host "WARNING: Windows Defender cmdlets not available." -ForegroundColor Yellow
    Write-Host "This script only works with Windows Defender." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "If you use a different antivirus (Kaspersky, McAfee, Norton, etc.):" -ForegroundColor Yellow
    Write-Host "  1. Open your antivirus settings" -ForegroundColor White
    Write-Host "  2. Add these to exclusions:" -ForegroundColor White
    Write-Host "     - Folder: $installPath" -ForegroundColor Cyan
    Write-Host "     - Process: VoiceLite.exe" -ForegroundColor Cyan
    Write-Host "     - Process: whisper.exe" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "Adding exclusions to Windows Defender..." -ForegroundColor Cyan
Write-Host ""

$successCount = 0
$failCount = 0

# Add folder exclusion
try {
    Write-Host "[1/3] Adding folder exclusion: $installPath" -ForegroundColor White
    Add-MpPreference -ExclusionPath $installPath -ErrorAction Stop
    Write-Host "  ✓ Success" -ForegroundColor Green
    $successCount++
} catch {
    Write-Host "  ✗ Failed: $($_.Exception.Message)" -ForegroundColor Red
    $failCount++
}

# Add VoiceLite.exe process exclusion
try {
    $voiceLiteExe = Join-Path $installPath "VoiceLite.exe"
    Write-Host "[2/3] Adding process exclusion: VoiceLite.exe" -ForegroundColor White
    Add-MpPreference -ExclusionProcess $voiceLiteExe -ErrorAction Stop
    Write-Host "  ✓ Success" -ForegroundColor Green
    $successCount++
} catch {
    Write-Host "  ✗ Failed: $($_.Exception.Message)" -ForegroundColor Red
    $failCount++
}

# Add whisper.exe process exclusion
try {
    $whisperExe = Join-Path $installPath "whisper\whisper.exe"
    Write-Host "[3/3] Adding process exclusion: whisper.exe" -ForegroundColor White
    Add-MpPreference -ExclusionProcess $whisperExe -ErrorAction Stop
    Write-Host "  ✓ Success" -ForegroundColor Green
    $successCount++
} catch {
    Write-Host "  ✗ Failed: $($_.Exception.Message)" -ForegroundColor Red
    $failCount++
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Summary: $successCount succeeded, $failCount failed" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Yellow" })
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "All exclusions added successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "VoiceLite should now work without antivirus interference." -ForegroundColor Green
    Write-Host "Please restart VoiceLite if it's currently running." -ForegroundColor Yellow
} else {
    Write-Host "Some exclusions failed to add." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "You can try adding them manually:" -ForegroundColor White
    Write-Host "  1. Open Windows Security" -ForegroundColor White
    Write-Host "  2. Go to Virus & threat protection > Manage settings" -ForegroundColor White
    Write-Host "  3. Scroll down to Exclusions" -ForegroundColor White
    Write-Host "  4. Add folder: $installPath" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
