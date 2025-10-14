# Manual BFG Setup (No Chocolatey Required)
# Downloads and configures BFG Repo-Cleaner directly

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Manual BFG Setup Script" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$bfgVersion = "1.14.0"
$bfgUrl = "https://repo1.maven.org/maven2/com/madgag/bfg/$bfgVersion/bfg-$bfgVersion.jar"
$downloadDir = "$env:USERPROFILE\Downloads"
$bfgPath = "$downloadDir\bfg.jar"

# Step 1: Check Java
Write-Host "[Step 1/2] Checking for Java..." -ForegroundColor Green

try {
    $javaVersion = java -version 2>&1 | Select-String -Pattern "version"
    Write-Host "[OK] Java is installed: $javaVersion" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Java not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Java first:" -ForegroundColor Yellow
    Write-Host "1. Download from: https://adoptium.net/temurin/releases/?version=11" -ForegroundColor Cyan
    Write-Host "2. Run the installer" -ForegroundColor Cyan
    Write-Host "3. Re-run this script" -ForegroundColor Cyan
    exit 1
}

Write-Host ""

# Step 2: Download BFG
Write-Host "[Step 2/2] Downloading BFG Repo-Cleaner..." -ForegroundColor Green

if (Test-Path $bfgPath) {
    Write-Host "[INFO] BFG already downloaded at: $bfgPath" -ForegroundColor Yellow
    $redownload = Read-Host "Re-download? (y/N)"
    if ($redownload -ne "y" -and $redownload -ne "Y") {
        Write-Host "[SKIP] Using existing BFG" -ForegroundColor Cyan
    } else {
        Remove-Item $bfgPath -Force
        Write-Host "Downloading from: $bfgUrl" -ForegroundColor Cyan
        Invoke-WebRequest -Uri $bfgUrl -OutFile $bfgPath
        Write-Host "[OK] Downloaded to: $bfgPath" -ForegroundColor Green
    }
} else {
    Write-Host "Downloading from: $bfgUrl" -ForegroundColor Cyan
    try {
        Invoke-WebRequest -Uri $bfgUrl -OutFile $bfgPath
        Write-Host "[OK] Downloaded to: $bfgPath" -ForegroundColor Green
    } catch {
        Write-Host "[ERROR] Download failed: $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "Manual download:" -ForegroundColor Yellow
        Write-Host "1. Visit: https://rtyley.github.io/bfg-repo-cleaner/" -ForegroundColor Cyan
        Write-Host "2. Download bfg.jar" -ForegroundColor Cyan
        Write-Host "3. Save to: $downloadDir" -ForegroundColor Cyan
        exit 1
    }
}

Write-Host ""

# Step 3: Verify BFG works
Write-Host "[Step 3/3] Verifying BFG..." -ForegroundColor Green

try {
    $bfgOutput = java -jar $bfgPath --version 2>&1
    Write-Host "[OK] BFG is working!" -ForegroundColor Green
    Write-Host $bfgOutput -ForegroundColor Cyan
} catch {
    Write-Host "[ERROR] BFG verification failed" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "[SUCCESS] BFG is ready!" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "BFG Location: $bfgPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next step: Run the scrubbing script with BFG path:" -ForegroundColor Yellow
Write-Host "  powershell -ExecutionPolicy Bypass -File scrub-git-history.ps1 -BfgPath '$bfgPath'" -ForegroundColor Cyan
