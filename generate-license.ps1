# Simple PowerShell script to generate VoiceLite license keys
# Usage: .\generate-license.ps1 [type]
# Types: personal, professional, business

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("personal", "professional", "business")]
    [string]$Type = "personal"
)

function Generate-LicenseKey {
    param([string]$Prefix)

    # Generate random hex values
    $part1 = (Get-Random -Maximum 0xFFFFFFFF).ToString("X8")
    $part2 = (Get-Random -Maximum 0xFFFFFFFF).ToString("X8")
    $part3 = (Get-Random -Maximum 0xFFFFFFFF).ToString("X8")

    # Format as XXX-AAAA-BBBB-CCCC-DDDD
    $key = "$Prefix-$($part1.Substring(0,4))-$($part1.Substring(4,4))-$($part2.Substring(0,4))-$($part2.Substring(4,4))"

    return $key.ToUpper()
}

# Determine prefix based on type
$prefix = switch ($Type) {
    "personal"      { "PER" }
    "professional"  { "PRO" }
    "business"      { "BUS" }
    default         { "PER" }
}

# Generate the key
$licenseKey = Generate-LicenseKey -Prefix $prefix

# Output the key
Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "VoiceLite License Key Generated" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Type:        " -NoNewline
Write-Host "$Type" -ForegroundColor Yellow
Write-Host "License Key: " -NoNewline
Write-Host "$licenseKey" -ForegroundColor Green
Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Copy to clipboard
$licenseKey | Set-Clipboard
Write-Host "✓ License key copied to clipboard!" -ForegroundColor Green
Write-Host ""

# Save to log file
$logFile = "generated-licenses.log"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$logEntry = "$timestamp | $Type | $licenseKey"
Add-Content -Path $logFile -Value $logEntry

Write-Host "License saved to $logFile" -ForegroundColor Gray
Write-Host ""

# Show email template
Write-Host "EMAIL TEMPLATE:" -ForegroundColor Yellow
Write-Host @"
Subject: Your VoiceLite License Key

Thank you for purchasing VoiceLite!

Your license information:
-----------------------
License Type: $Type
License Key: $licenseKey

To activate:
1. Open VoiceLite
2. Click the system tray icon and select "License..."
3. Enter your license key: $licenseKey
4. Click Activate

Download VoiceLite (if needed):
https://voicelite.app

If you have any issues, please reply to this email.

Best regards,
VoiceLite Team
"@