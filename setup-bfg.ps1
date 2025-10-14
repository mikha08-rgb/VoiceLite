# BFG Repo-Cleaner Setup Script
# This script automates the installation of prerequisites for git history scrubbing

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "BFG Repo-Cleaner Setup Script" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "WARNING: Not running as Administrator" -ForegroundColor Yellow
    Write-Host "Some installations may require elevated privileges" -ForegroundColor Yellow
    Write-Host ""
}

# Step 1: Check for Chocolatey
Write-Host "[Step 1/4] Checking for Chocolatey package manager..." -ForegroundColor Green

try {
    choco --version | Out-Null
    Write-Host "[OK] Chocolatey is installed" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Chocolatey not found" -ForegroundColor Red
    Write-Host ""
    Write-Host "Install Chocolatey? (Recommended)" -ForegroundColor Yellow
    Write-Host "Run this command in an ADMIN PowerShell:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host 'Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString("https://community.chocolatey.org/install.ps1"))' -ForegroundColor Cyan
    Write-Host ""
    Write-Host "After installing Chocolatey, re-run this script." -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Step 2: Check for Java
Write-Host "[Step 2/4] Checking for Java Runtime..." -ForegroundColor Green

try {
    $javaVersion = java -version 2>&1 | Select-String -Pattern 'version'
    Write-Host "[OK] Java is installed: $javaVersion" -ForegroundColor Green
} catch {
    Write-Host "[WARN] Java not found - installing..." -ForegroundColor Yellow

    try {
        choco install openjdk11 -y
        Write-Host "[OK] Java installed successfully" -ForegroundColor Green

        # Refresh environment variables
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    } catch {
        Write-Host "[ERROR] Failed to install Java via Chocolatey" -ForegroundColor Red
        Write-Host "Please install Java manually from: https://adoptium.net/" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""

# Step 3: Check for BFG
Write-Host "[Step 3/4] Checking for BFG Repo-Cleaner..." -ForegroundColor Green

$bfgPath = "C:\ProgramData\chocolatey\lib\bfg-repo-cleaner\tools\bfg.jar"

if (Test-Path $bfgPath) {
    Write-Host "[OK] BFG is installed at: $bfgPath" -ForegroundColor Green
} else {
    Write-Host "[WARN] BFG not found - installing..." -ForegroundColor Yellow

    try {
        choco install bfg-repo-cleaner -y
        Write-Host "[OK] BFG installed successfully" -ForegroundColor Green
    } catch {
        Write-Host "[ERROR] Failed to install BFG via Chocolatey" -ForegroundColor Red
        Write-Host "Please download manually from: https://rtyley.github.io/bfg-repo-cleaner/" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""

# Step 4: Verify installation
Write-Host "[Step 4/4] Verifying BFG installation..." -ForegroundColor Green

try {
    $bfgVersion = java -jar $bfgPath --version 2>&1
    Write-Host "[OK] BFG is working: $bfgVersion" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] BFG verification failed" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "[SUCCESS] All prerequisites installed!" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Close this PowerShell window" -ForegroundColor White
Write-Host "2. Open GIT_HISTORY_SCRUB_INSTRUCTIONS.md" -ForegroundColor White
Write-Host "3. Follow Steps 3-9 to scrub git history" -ForegroundColor White
Write-Host ""
Write-Host "BFG Location: $bfgPath" -ForegroundColor Cyan
