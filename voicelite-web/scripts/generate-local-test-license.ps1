# Quick script to generate a test license for local development
# Usage: .\scripts\generate-local-test-license.ps1 [email]

param(
    [string]$Email = "test@example.com"
)

Write-Host ""
Write-Host "🔑 Generating test license for: $Email" -ForegroundColor Yellow
Write-Host ""

# Check if .env.local exists
if (-not (Test-Path ".env.local")) {
    Write-Host "❌ Error: .env.local not found" -ForegroundColor Red
    Write-Host "Create .env.local and add: ADMIN_SECRET=your-secret-here"
    exit 1
}

# Extract ADMIN_SECRET from .env.local
$envContent = Get-Content ".env.local" -Raw
if ($envContent -match 'ADMIN_SECRET\s*=\s*[''"]?([^''""\r\n]+)[''"]?') {
    $adminSecret = $matches[1]
} else {
    Write-Host "❌ Error: ADMIN_SECRET not found in .env.local" -ForegroundColor Red
    Write-Host "Add to .env.local: ADMIN_SECRET=your-secret-here"
    exit 1
}

if ([string]::IsNullOrWhiteSpace($adminSecret)) {
    Write-Host "❌ Error: ADMIN_SECRET is empty" -ForegroundColor Red
    exit 1
}

# Make the API call
try {
    $body = @{
        email = $Email
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "http://localhost:3000/api/admin/generate-test-license" `
        -Method POST `
        -Headers @{
            "x-admin-secret" = $adminSecret
            "Content-Type" = "application/json"
        } `
        -Body $body

    if ($response.success) {
        Write-Host "✅ License generated successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        Write-Host "License Key: $($response.license.key)" -ForegroundColor Green
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        Write-Host ""
        Write-Host "📋 Next steps:"
        Write-Host "1. Copy the license key above"
        Write-Host "2. Launch VoiceLite desktop app"
        Write-Host "3. Click 'Activate License'"
        Write-Host "4. Paste the key and activate"
        Write-Host ""

        # Copy to clipboard if possible
        try {
            Set-Clipboard -Value $response.license.key
            Write-Host "📋 License key copied to clipboard!" -ForegroundColor Cyan
            Write-Host ""
        } catch {
            # Clipboard not available, ignore
        }
    } else {
        Write-Host "❌ Failed to generate license" -ForegroundColor Red
        Write-Host ""
        Write-Host "Response:"
        $response | ConvertTo-Json -Depth 10
        exit 1
    }
} catch {
    Write-Host "❌ Error calling API" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)"
    Write-Host ""
    Write-Host "Make sure:"
    Write-Host "1. Next.js dev server is running (npm run dev)"
    Write-Host "2. NODE_ENV is not set to 'production'"
    Write-Host "3. ADMIN_SECRET is correctly configured"
    Write-Host "4. Server is accessible at http://localhost:3000"
    exit 1
}
