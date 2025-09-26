# VoiceLite Dependency Installer
# Run this as Administrator if VoiceLite shows errors

param(
    [switch]$Silent = $false
)

$ErrorActionPreference = "Stop"

# Check if running as admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")

if (-not $isAdmin) {
    Write-Host "This script needs to run as Administrator to install dependencies." -ForegroundColor Yellow
    Write-Host "Restarting with elevated privileges..." -ForegroundColor Yellow

    Start-Process PowerShell -ArgumentList "-ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Write-Host @"
==========================================
VoiceLite Dependency Installer
==========================================

This will install required components:
- Microsoft Visual C++ Runtime 2015-2022
- .NET 8.0 Desktop Runtime (if needed)

"@ -ForegroundColor Cyan

if (-not $Silent) {
    $continue = Read-Host "Continue? (Y/N)"
    if ($continue -ne "Y" -and $continue -ne "y") {
        exit
    }
}

# Create temp directory
$tempDir = Join-Path $env:TEMP "VoiceLite-Setup"
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir | Out-Null
}

# Function to download file with progress
function Download-WithProgress {
    param(
        [string]$Url,
        [string]$OutFile,
        [string]$DisplayName
    )

    Write-Host "`nDownloading $DisplayName..." -ForegroundColor Green

    try {
        $webClient = New-Object System.Net.WebClient

        # Register event for progress
        $progressActivity = "Downloading $DisplayName"
        Register-ObjectEvent -InputObject $webClient -EventName DownloadProgressChanged -Action {
            Write-Progress -Activity $progressActivity `
                          -Status "$($EventArgs.ProgressPercentage)% Complete" `
                          -PercentComplete $EventArgs.ProgressPercentage
        } | Out-Null

        # Download file
        $webClient.DownloadFile($Url, $OutFile)

        Write-Progress -Activity $progressActivity -Completed
        Write-Host "  Downloaded successfully" -ForegroundColor Gray
        return $true
    }
    catch {
        Write-Host "  Download failed: $_" -ForegroundColor Red
        return $false
    }
}

# 1. Install Visual C++ Runtime
Write-Host "`n1. Visual C++ Runtime" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan

$vcRedistUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
$vcRedistPath = Join-Path $tempDir "vc_redist.x64.exe"

# Check if already installed
$vcInstalled = $false
$vcKeys = @(
    "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
)

foreach ($key in $vcKeys) {
    if (Test-Path $key) {
        $installed = Get-ItemProperty $key -Name Installed -ErrorAction SilentlyContinue
        if ($installed.Installed -eq 1) {
            $vcInstalled = $true
            $version = Get-ItemProperty $key -Name Version -ErrorAction SilentlyContinue
            Write-Host "  Already installed (Version: $($version.Version))" -ForegroundColor Green
            break
        }
    }
}

if (-not $vcInstalled) {
    if (Download-WithProgress -Url $vcRedistUrl -OutFile $vcRedistPath -DisplayName "Visual C++ Runtime") {
        Write-Host "  Installing Visual C++ Runtime..." -ForegroundColor Yellow

        $process = Start-Process -FilePath $vcRedistPath `
                                -ArgumentList "/quiet", "/norestart" `
                                -PassThru `
                                -Wait

        if ($process.ExitCode -eq 0) {
            Write-Host "  Installed successfully!" -ForegroundColor Green
        } elseif ($process.ExitCode -eq 3010) {
            Write-Host "  Installed successfully! (Restart required)" -ForegroundColor Green
            $restartRequired = $true
        } else {
            Write-Host "  Installation failed (Exit code: $($process.ExitCode))" -ForegroundColor Red
        }
    }
}

# 2. Check .NET Runtime
Write-Host "`n2. .NET Desktop Runtime" -ForegroundColor Cyan
Write-Host "=======================" -ForegroundColor Cyan

$dotnetInstalled = $false

try {
    $runtimes = & dotnet --list-runtimes 2>$null
    if ($runtimes -match "Microsoft.WindowsDesktop.App 8") {
        $dotnetInstalled = $true
        Write-Host "  Already installed" -ForegroundColor Green
    }
} catch {
    # dotnet command not found
}

if (-not $dotnetInstalled) {
    Write-Host "  .NET 8 Desktop Runtime not found" -ForegroundColor Yellow

    $dotnetUrl = "https://download.visualstudio.microsoft.com/download/pr/b280d97f-25a9-4ab7-8a12-8291aa3af117/a37ed0e68f51fcd973e9f6cb4f40b1a7/windowsdesktop-runtime-8.0.0-win-x64.exe"
    $dotnetPath = Join-Path $tempDir "windowsdesktop-runtime-8.0.0-win-x64.exe"

    if (Download-WithProgress -Url $dotnetUrl -OutFile $dotnetPath -DisplayName ".NET Desktop Runtime") {
        Write-Host "  Installing .NET Desktop Runtime..." -ForegroundColor Yellow

        $process = Start-Process -FilePath $dotnetPath `
                                -ArgumentList "/quiet", "/norestart" `
                                -PassThru `
                                -Wait

        if ($process.ExitCode -eq 0) {
            Write-Host "  Installed successfully!" -ForegroundColor Green
        } else {
            Write-Host "  Installation failed or cancelled" -ForegroundColor Red
        }
    }
}

# 3. Verify Whisper components
Write-Host "`n3. Checking VoiceLite Components" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

$scriptDir = Split-Path -Parent $PSCommandPath
$whisperExe = Join-Path $scriptDir "VoiceLite\whisper\whisper.exe"

if (Test-Path $whisperExe) {
    Write-Host "  Whisper.exe found" -ForegroundColor Green

    # Try to run whisper.exe to verify dependencies
    try {
        $proc = Start-Process -FilePath $whisperExe `
                             -ArgumentList "--help" `
                             -NoNewWindow `
                             -PassThru `
                             -RedirectStandardOutput "NUL" `
                             -Wait

        if ($proc.ExitCode -eq 0 -or $proc.ExitCode -eq 1) {
            Write-Host "  Whisper.exe verified - ready to use!" -ForegroundColor Green
        } else {
            Write-Host "  Whisper.exe test failed" -ForegroundColor Red
        }
    } catch {
        Write-Host "  Could not verify whisper.exe: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Whisper.exe not found (expected at: $whisperExe)" -ForegroundColor Yellow
}

# Clean up
Write-Host "`nCleaning up temporary files..." -ForegroundColor Gray
Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue

# Summary
Write-Host "`n===========================================" -ForegroundColor Green
Write-Host "Installation Complete!" -ForegroundColor Green
Write-Host "===========================================" -ForegroundColor Green

if ($restartRequired) {
    Write-Host "`nIMPORTANT: A system restart is recommended" -ForegroundColor Yellow
    Write-Host "Please restart your computer before using VoiceLite" -ForegroundColor Yellow
}

Write-Host "`nYou can now run VoiceLite!" -ForegroundColor Cyan
Write-Host "If you still see errors, try:" -ForegroundColor Gray
Write-Host "  1. Restart your computer" -ForegroundColor Gray
Write-Host "  2. Run this installer again" -ForegroundColor Gray
Write-Host "  3. Check antivirus settings" -ForegroundColor Gray

Write-Host "`nPress any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")