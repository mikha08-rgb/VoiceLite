# Secure Key Generator for VoiceLite
# Run this to generate secure API keys for production

Write-Host "VoiceLite Secure Key Generator" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
Write-Host ""

# Generate secure random keys
$apiKey = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | % {[char]$_})
$adminKey = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | % {[char]$_})
$webhookSecret = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 24 | % {[char]$_})

Write-Host "Generated Secure Keys:" -ForegroundColor Green
Write-Host ""
Write-Host "API_KEY=$apiKey" -ForegroundColor Yellow
Write-Host "ADMIN_KEY=$adminKey" -ForegroundColor Yellow
Write-Host "WEBHOOK_SECRET=$webhookSecret" -ForegroundColor Yellow
Write-Host ""

Write-Host "Instructions:" -ForegroundColor Cyan
Write-Host "1. Copy these keys to your .env file" -ForegroundColor White
Write-Host "2. Set them in Railway/Render environment variables" -ForegroundColor White
Write-Host "3. NEVER commit these keys to Git" -ForegroundColor Red
Write-Host "4. Store them securely (password manager)" -ForegroundColor White
Write-Host ""

# Offer to save to file
$save = Read-Host "Save to production-keys.txt? (y/n)"
if ($save -eq 'y') {
    $content = @"
VoiceLite Production Keys
Generated: $(Get-Date)
========================

API_KEY=$apiKey
ADMIN_KEY=$adminKey
WEBHOOK_SECRET=$webhookSecret

IMPORTANT: Store these securely and delete this file after saving elsewhere!
"@

    $content | Out-File -FilePath "production-keys.txt"
    Write-Host "Keys saved to production-keys.txt" -ForegroundColor Green
    Write-Host "IMPORTANT: Move this file to a secure location immediately!" -ForegroundColor Red
}