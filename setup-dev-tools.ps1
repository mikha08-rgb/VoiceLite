# VoiceLite Dev Tools Setup Script
# Run this in PowerShell (regular user, NOT Administrator)

Write-Host "=== VoiceLite Dev Tools Setup ===" -ForegroundColor Cyan
Write-Host ""

# Function to check if command exists
function Test-CommandExists {
    param($command)
    try {
        $null = Get-Command $command -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

# 1. Install Scoop (package manager) if not installed
Write-Host "[1/4] Checking Scoop package manager..." -ForegroundColor Yellow
if (-not (Test-CommandExists "scoop")) {
    Write-Host "Installing Scoop..." -ForegroundColor Green
    Write-Host "Note: Scoop requires regular user permissions (not Admin)" -ForegroundColor Cyan
    try {
        Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force
        Invoke-RestMethod -Uri https://get.scoop.sh | Invoke-Expression

        # Refresh PATH
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path","User") + ";" + [System.Environment]::GetEnvironmentVariable("Path","Machine")

        if (Test-CommandExists "scoop") {
            Write-Host "SUCCESS: Scoop installed" -ForegroundColor Green
        }
        else {
            Write-Host "WARNING: Scoop installed but not in PATH yet" -ForegroundColor Yellow
            Write-Host "Please close and reopen PowerShell, then run this script again" -ForegroundColor Cyan
            pause
            exit
        }
    }
    catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
        Write-Host "Try installing manually: https://scoop.sh" -ForegroundColor Cyan
        pause
        exit
    }
}
else {
    Write-Host "SUCCESS: Scoop already installed" -ForegroundColor Green
}

Write-Host ""

# 2. Install Stripe CLI
Write-Host "[2/4] Installing Stripe CLI..." -ForegroundColor Yellow
if (-not (Test-CommandExists "stripe")) {
    try {
        Write-Host "Adding Stripe bucket..." -ForegroundColor Cyan
        scoop bucket add stripe https://github.com/stripe/scoop-stripe-cli.git 2>&1 | Out-Null

        Write-Host "Installing Stripe CLI..." -ForegroundColor Cyan
        scoop install stripe

        # Refresh PATH
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path","User") + ";" + [System.Environment]::GetEnvironmentVariable("Path","Machine")

        if (Test-CommandExists "stripe") {
            Write-Host "SUCCESS: Stripe CLI installed" -ForegroundColor Green
            stripe --version
            Write-Host ""
            Write-Host "Next: Run 'stripe login' to connect to your Stripe account" -ForegroundColor Cyan
        }
        else {
            Write-Host "WARNING: Stripe installed but not in PATH yet" -ForegroundColor Yellow
            Write-Host "Please close and reopen PowerShell" -ForegroundColor Cyan
        }
    }
    catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
    }
}
else {
    Write-Host "SUCCESS: Stripe CLI already installed" -ForegroundColor Green
    stripe --version
}

Write-Host ""

# 3. Download Bruno (manual download required)
Write-Host "[3/4] Bruno API Client..." -ForegroundColor Yellow
Write-Host "Bruno requires manual download:" -ForegroundColor Cyan
Write-Host "  1. Visit: https://www.usebruno.com/downloads" -ForegroundColor White
Write-Host "  2. Download 'Bruno-X.X.X-Setup.exe'" -ForegroundColor White
Write-Host "  3. Run the installer" -ForegroundColor White
Write-Host ""
$openBruno = Read-Host "Open Bruno download page now? (y/n)"
if ($openBruno -eq 'y' -or $openBruno -eq 'Y') {
    Start-Process "https://www.usebruno.com/downloads"
    Write-Host "Browser opened - download and install Bruno" -ForegroundColor Green
}

Write-Host ""

# 4. Check Node.js and npm (should already be installed)
Write-Host "[4/4] Checking Node.js and npm..." -ForegroundColor Yellow
if (Test-CommandExists "node") {
    $nodeVersion = node --version
    Write-Host "SUCCESS: Node.js installed ($nodeVersion)" -ForegroundColor Green
}
else {
    Write-Host "ERROR: Node.js not found" -ForegroundColor Red
    Write-Host "Download from: https://nodejs.org/" -ForegroundColor Cyan
}

if (Test-CommandExists "npm") {
    $npmVersion = npm --version
    Write-Host "SUCCESS: npm installed (v$npmVersion)" -ForegroundColor Green
}
else {
    Write-Host "ERROR: npm not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Setup Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Tools installed:" -ForegroundColor Yellow
if (Test-CommandExists "scoop") { Write-Host "  [✓] Scoop" -ForegroundColor Green } else { Write-Host "  [✗] Scoop" -ForegroundColor Red }
if (Test-CommandExists "stripe") { Write-Host "  [✓] Stripe CLI" -ForegroundColor Green } else { Write-Host "  [✗] Stripe CLI" -ForegroundColor Red }
if (Test-CommandExists "node") { Write-Host "  [✓] Node.js" -ForegroundColor Green } else { Write-Host "  [✗] Node.js" -ForegroundColor Red }
if (Test-CommandExists "npm") { Write-Host "  [✓] npm" -ForegroundColor Green } else { Write-Host "  [✗] npm" -ForegroundColor Red }
Write-Host "  [ ] Bruno (manual install)" -ForegroundColor Yellow

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Run: stripe login" -ForegroundColor White
Write-Host "  2. Download & install Bruno" -ForegroundColor White
Write-Host "  3. Test Prisma Studio: cd voicelite-web; npm run db:studio" -ForegroundColor White
Write-Host ""
Write-Host "To test Stripe webhooks locally:" -ForegroundColor Yellow
Write-Host "  Terminal 1: cd voicelite-web; npm run dev" -ForegroundColor White
Write-Host "  Terminal 2: stripe listen --forward-to localhost:3000/api/webhook" -ForegroundColor White
Write-Host "  Terminal 3: stripe trigger payment_intent.succeeded" -ForegroundColor White
Write-Host ""
Write-Host "See DEV_TOOLS_SETUP.md for detailed instructions" -ForegroundColor Cyan
Write-Host ""
pause