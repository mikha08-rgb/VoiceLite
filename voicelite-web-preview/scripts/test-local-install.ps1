# Quick Local Installation Test
# Run this to simulate what a user would experience

param(
    [switch]$SimulateCleanSystem,
    [switch]$TestFromGitHub
)

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "VoiceLite Local Installation Tester" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

$testDir = "$env:TEMP\VoiceLite-InstallTest-$(Get-Random)"
New-Item -ItemType Directory -Path $testDir -Force | Out-Null

Write-Host "`nTest directory: $testDir" -ForegroundColor Gray

# Build the release first
Write-Host "`n[STEP 1] Building Release Package..." -ForegroundColor Yellow

if (Test-Path ".\build-release.ps1") {
    & .\build-release.ps1 -Configuration Release -ModelSize small

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed!"
        exit 1
    }
} else {
    Write-Warning "build-release.ps1 not found. Using existing build."
}

# Copy to test location
Write-Host "`n[STEP 2] Copying to test location..." -ForegroundColor Yellow

$sourceDir = ".\VoiceLite-Release"
if (-not (Test-Path $sourceDir)) {
    Write-Error "Release directory not found. Run build-release.ps1 first."
    exit 1
}

Copy-Item -Path $sourceDir\* -Destination $testDir -Recurse -Force
Write-Host "  Copied to: $testDir" -ForegroundColor Green

# Simulate downloaded files being blocked
if ($SimulateCleanSystem) {
    Write-Host "`n[STEP 3] Simulating Windows Security Block..." -ForegroundColor Yellow

    Get-ChildItem -Path $testDir -Include "*.exe","*.dll" -Recurse | ForEach-Object {
        # Add Zone.Identifier to simulate downloaded file
        $zoneContent = @"
[ZoneTransfer]
ZoneId=3
ReferrerUrl=https://github.com
HostUrl=https://github.com/releases/download/v1.0/VoiceLite.zip
"@
        Set-Content -Path "$($_.FullName):Zone.Identifier" -Value $zoneContent -Stream Zone.Identifier
    }

    Write-Host "  Files marked as downloaded from internet" -ForegroundColor Green
}

# Test if VC++ Runtime is missing
Write-Host "`n[STEP 4] Checking Dependencies..." -ForegroundColor Yellow

$vcMissing = $false
try {
    Add-Type -TypeDefinition @"
    using System;
    using System.Runtime.InteropServices;
    public class NativeMethods {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);
    }
"@

    $requiredDlls = @("VCRUNTIME140.dll", "VCRUNTIME140_1.dll")
    foreach ($dll in $requiredDlls) {
        $handle = [NativeMethods]::LoadLibrary($dll)
        if ($handle -eq [IntPtr]::Zero) {
            $vcMissing = $true
            Write-Host "  Missing: $dll" -ForegroundColor Red
        } else {
            [NativeMethods]::FreeLibrary($handle) | Out-Null
            Write-Host "  Found: $dll" -ForegroundColor Green
        }
    }
} catch {
    Write-Warning "  Could not check VC++ Runtime"
}

# Start VoiceLite
Write-Host "`n[STEP 5] Starting VoiceLite..." -ForegroundColor Yellow

$exePath = Join-Path $testDir "VoiceLite.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "VoiceLite.exe not found!"
    exit 1
}

Write-Host "  Launching: $exePath" -ForegroundColor Gray

# Start and monitor
$proc = Start-Process -FilePath $exePath -PassThru

# Wait a moment
Start-Sleep -Seconds 3

# Check status
if ($proc.HasExited) {
    Write-Host "`n❌ VoiceLite crashed immediately!" -ForegroundColor Red
    Write-Host "  Exit code: $($proc.ExitCode)" -ForegroundColor Red

    # Check Windows Event Log for crash details
    $events = Get-WinEvent -FilterHashtable @{LogName='Application'; ID=1000} -MaxEvents 5 -ErrorAction SilentlyContinue |
              Where-Object { $_.Message -like "*VoiceLite*" }

    if ($events) {
        Write-Host "`nCrash details from Event Log:" -ForegroundColor Yellow
        $events | ForEach-Object { Write-Host $_.Message -ForegroundColor Gray }
    }

    # Common exit codes
    switch ($proc.ExitCode) {
        -1073741515 { Write-Host "Missing DLL dependencies (likely VC++ Runtime)" -ForegroundColor Red }
        -1073740791 { Write-Host "Access violation - check antivirus" -ForegroundColor Red }
        3221225477 { Write-Host "Access denied - check permissions" -ForegroundColor Red }
        default { Write-Host "Unknown error - check logs" -ForegroundColor Red }
    }
} else {
    Write-Host "`n✅ VoiceLite is running!" -ForegroundColor Green
    Write-Host "  Process ID: $($proc.Id)" -ForegroundColor Gray

    # Check what the user sees
    Write-Host "`n[STEP 6] Manual Verification Required:" -ForegroundColor Yellow
    Write-Host "  1. Did a dependency installer window appear?" -ForegroundColor Cyan
    Write-Host "  2. Is there a system tray icon?" -ForegroundColor Cyan
    Write-Host "  3. Does the main window show?" -ForegroundColor Cyan
    Write-Host "  4. Any error messages?" -ForegroundColor Cyan

    $continue = Read-Host "`nPress Enter to stop VoiceLite and clean up"

    # Stop the process
    if (-not $proc.HasExited) {
        Stop-Process -Id $proc.Id -Force
    }
}

# Check logs
Write-Host "`n[STEP 7] Checking Logs..." -ForegroundColor Yellow

$logPath = Join-Path $testDir "logs"
if (Test-Path $logPath) {
    $logs = Get-ChildItem -Path $logPath -Filter "*.log" -ErrorAction SilentlyContinue
    if ($logs) {
        Write-Host "  Found log files:" -ForegroundColor Green
        foreach ($log in $logs) {
            Write-Host "    - $($log.Name)" -ForegroundColor Gray

            # Show last few lines
            $content = Get-Content $log.FullName -Tail 10
            if ($content) {
                Write-Host "    Last entries:" -ForegroundColor Gray
                $content | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
            }
        }
    }
}

# Cleanup
Write-Host "`n[STEP 8] Cleanup..." -ForegroundColor Yellow

$cleanup = Read-Host "Remove test directory? (Y/N)"
if ($cleanup -eq 'Y') {
    Remove-Item -Path $testDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  Cleaned up test files" -ForegroundColor Green
} else {
    Write-Host "  Test files remain at: $testDir" -ForegroundColor Yellow
}

# Summary
Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

if ($vcMissing) {
    Write-Host "⚠ VC++ Runtime missing - installer should have triggered" -ForegroundColor Yellow
}

if ($proc.HasExited -and $proc.ExitCode -ne 0) {
    Write-Host "❌ Installation test FAILED" -ForegroundColor Red
    Write-Host "   VoiceLite crashed on startup" -ForegroundColor Red
} else {
    Write-Host "✅ Installation test PASSED" -ForegroundColor Green
    Write-Host "   VoiceLite started successfully" -ForegroundColor Green
}

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "  1. Test on a real VM with fresh Windows" -ForegroundColor Gray
Write-Host "  2. Upload to GitHub and test download" -ForegroundColor Gray
Write-Host "  3. Test on different Windows versions" -ForegroundColor Gray