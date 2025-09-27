# VoiceLite License Key Generator with Checksum
# Usage: .\generate-license.ps1 [type]
# Types: personal, professional, business

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("personal", "professional", "business")]
    [string]$Type = "personal"
)

function Generate-LicenseKey {
    param([string]$Prefix)

    # Generate random hex values (12 chars)
    $chars = "0123456789ABCDEF"
    $mainKey = $Prefix

    # Generate 12 random hex characters
    for ($i = 0; $i -lt 12; $i++) {
        $mainKey += $chars[(Get-Random -Maximum 16)]
    }

    # Calculate checksum
    $sum = 0
    foreach ($char in $mainKey.ToCharArray()) {
        $sum += [int][char]$char
    }
    $checksumValue = ($sum * 7919).ToString("X")
    $checksum = $checksumValue.Substring($checksumValue.Length - 4).PadLeft(4, '0')

    # Combine main key with checksum
    $fullKey = $mainKey + $checksum

    # Format as XXX-XXXX-XXXX-XXXX-XXXX
    $formatted = $fullKey.Substring(0,3) + "-" +
                 $fullKey.Substring(3,4) + "-" +
                 $fullKey.Substring(7,4) + "-" +
                 $fullKey.Substring(11,4) + "-" +
                 $fullKey.Substring(15,4)

    return $formatted
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
Write-Host "Type:        $Type" -ForegroundColor Yellow
Write-Host "License Key: $licenseKey" -ForegroundColor Green
Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Copy to clipboard
$licenseKey | Set-Clipboard
Write-Host "License key copied to clipboard!" -ForegroundColor Green
Write-Host ""

# Save to log file
$logFile = "generated-licenses.log"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$logEntry = "$timestamp - $Type - $licenseKey"
Add-Content -Path $logFile -Value $logEntry

Write-Host "License saved to $logFile" -ForegroundColor Gray
Write-Host ""

# Show email template
Write-Host "EMAIL TEMPLATE:" -ForegroundColor Yellow
Write-Host "===============" -ForegroundColor Yellow
Write-Host ""
Write-Host "Subject: Your VoiceLite License Key"
Write-Host ""
Write-Host "Thank you for purchasing VoiceLite!"
Write-Host ""
Write-Host "Your license information:"
Write-Host "License Type: $Type"
Write-Host "License Key: $licenseKey"
Write-Host ""
Write-Host "To activate:"
Write-Host "1. Open VoiceLite"
Write-Host "2. Click the system tray icon and select License..."
Write-Host "3. Enter your license key: $licenseKey"
Write-Host "4. Click Activate"
Write-Host ""
Write-Host "Download VoiceLite (if needed):"
Write-Host "https://voicelite.app"
Write-Host ""
Write-Host "If you have any issues, please reply to this email."
Write-Host ""
Write-Host "Best regards,"
Write-Host "VoiceLite Team"
Write-Host ""