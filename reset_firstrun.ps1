$settingsPath = Join-Path $env:LOCALAPPDATA 'VoiceLite\settings.json'

if (Test-Path $settingsPath) {
    Write-Host "Found settings file at: $settingsPath" -ForegroundColor Yellow
    Write-Host "Resetting first-run flag..." -ForegroundColor Yellow

    $settings = Get-Content $settingsPath | ConvertFrom-Json
    $settings.HasSeenFirstRunDiagnostics = $false
    $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath

    Write-Host "✓ First-run flag reset successfully!" -ForegroundColor Green
    Write-Host "The diagnostic window will appear on next launch." -ForegroundColor Green
} else {
    Write-Host "No settings file found (first-time setup)" -ForegroundColor Cyan
    Write-Host "The diagnostic window will appear automatically on first launch." -ForegroundColor Cyan
}
