$appDataPath = [Environment]::GetFolderPath('LocalApplicationData') + '\VoiceLite'
$licensePath = Join-Path $appDataPath 'license.dat'

# Create license object
$licenseKey = 'VL-TEMP-' + [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$license = @{
    LicenseKey = $licenseKey
    Email = 'temp@voicelite.local'
    ValidatedAt = (Get-Date).ToUniversalTime().ToString('o')
    Type = 'LIFETIME'
}

# Create directory
New-Item -ItemType Directory -Force -Path $appDataPath | Out-Null

# Convert to JSON
$json = $license | ConvertTo-Json

# Load required assembly for DPAPI
Add-Type -AssemblyName System.Security

# Encrypt using DPAPI
$bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
$encrypted = [System.Security.Cryptography.ProtectedData]::Protect($bytes, $null, 'CurrentUser')

# Save
[System.IO.File]::WriteAllBytes($licensePath, $encrypted)

Write-Host ''
Write-Host '✅ Temporary Pro License Created!' -ForegroundColor Green
Write-Host ''
Write-Host 'License Key:' $licenseKey -ForegroundColor Cyan
Write-Host 'Email: temp@voicelite.local' -ForegroundColor Cyan
Write-Host 'Type: LIFETIME (Pro)' -ForegroundColor Cyan
Write-Host 'Location:' $licensePath -ForegroundColor Cyan
Write-Host ''
Write-Host '🎯 VoiceLite is now running in PRO mode!' -ForegroundColor Yellow
Write-Host ''
Write-Host 'Pro Features Now Available:' -ForegroundColor White
Write-Host '  ✓ Models tab visible in Settings' -ForegroundColor Green
Write-Host '  ✓ Can download Base, Small, Medium, Large models' -ForegroundColor Green
Write-Host '  ✓ Can switch between all 5 models' -ForegroundColor Green
Write-Host ''
Write-Host 'The app is already running. Restart VoiceLite to activate Pro mode!' -ForegroundColor Yellow
Write-Host ''
