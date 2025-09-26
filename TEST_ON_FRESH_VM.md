# VoiceLite Fresh Installation Test Guide

## Prerequisites for VM Testing

### VM Setup
1. **Windows 10/11 VM** (VMware, VirtualBox, or Hyper-V)
   - Minimum 4GB RAM allocated
   - 20GB disk space
   - Audio input enabled (critical for testing)
   - Network connection

2. **Clean Windows State**
   - No Visual C++ Runtime installed
   - No .NET Runtime installed
   - Windows Defender enabled (default)
   - No modifications to system

## Test Scenarios

### Scenario 1: Complete Fresh Install
```powershell
# Run this on fresh VM to verify clean state
@"
Checking system state...
"@ | Out-Host

# Check VC++ Runtime
$vcInstalled = $false
try {
    $null = [System.Runtime.InteropServices.Marshal]::LoadLibrary("VCRUNTIME140.dll")
    $vcInstalled = $true
} catch { }

Write-Host "VC++ Runtime Installed: $vcInstalled" -ForegroundColor $(if($vcInstalled){"Red"}else{"Green"})

# Check .NET
$dotnet = dotnet --list-runtimes 2>$null | Where-Object { $_ -match "Microsoft.WindowsDesktop.App 8" }
Write-Host ".NET 8 Desktop: $(if($dotnet){'Installed' }else{'Not Installed'})" -ForegroundColor $(if($dotnet){"Red"}else{"Green"})

# Check if Defender is active
$defender = Get-MpPreference | Select-Object DisableRealtimeMonitoring
Write-Host "Windows Defender: $(if($defender.DisableRealtimeMonitoring){'Disabled'}else{'Active'})" -ForegroundColor Yellow
```

### Scenario 2: Download and Run Test
1. Download release from GitHub
2. Extract to Desktop (not Program Files)
3. Run without any preparation

**Expected behavior:**
- App should detect missing VC++ Runtime
- Should prompt to auto-install
- Should work after installation

### Scenario 3: Antivirus Interference Test
```powershell
# Simulate aggressive AV scanning
Set-MpPreference -ScanOnlyIfIdleEnabled $false
Set-MpPreference -DisableCpuThrottleOnIdleScans $false

# Run VoiceLite
# Expected: Should detect AV interference and provide instructions
```

### Scenario 4: Permission Issues Test
```powershell
# Test from different locations
$testLocations = @(
    "C:\Program Files\VoiceLite",  # Protected
    "C:\Windows\Temp\VoiceLite",   # Temp
    "$env:USERPROFILE\Desktop\VoiceLite",  # User folder
    "C:\VoiceLite"  # Root (needs admin)
)

foreach ($location in $testLocations) {
    Write-Host "`nTesting from: $location"
    # Copy and run VoiceLite from each location
}
```

## Automated Test Script

Save as `test-fresh-install.ps1` and run on VM:

```powershell
#Requires -RunAsAdministrator

param(
    [string]$DownloadUrl = "https://github.com/yourusername/VoiceLite/releases/latest/download/VoiceLite.zip",
    [string]$TestDir = "$env:USERPROFILE\Desktop\VoiceLite-Test"
)

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "VoiceLite Fresh Install Tester" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan

# Clean previous tests
if (Test-Path $TestDir) {
    Remove-Item $TestDir -Recurse -Force
}

New-Item -ItemType Directory -Path $TestDir | Out-Null
Set-Location $TestDir

# Test 1: System State Check
Write-Host "`n[TEST 1] Checking System State..." -ForegroundColor Yellow

$results = @{
    Timestamp = Get-Date
    WindowsVersion = [System.Environment]::OSVersion.Version.ToString()
    DotNetInstalled = $false
    VCRuntimeInstalled = $false
    DefenderActive = $true
    DiskSpaceGB = 0
    MicrophoneFound = $false
}

# Check .NET
try {
    $dotnet = & dotnet --list-runtimes 2>$null | Where-Object { $_ -match "Microsoft.WindowsDesktop.App 8" }
    $results.DotNetInstalled = [bool]$dotnet
} catch { }

# Check VC Runtime
$vcDlls = @("VCRUNTIME140.dll", "VCRUNTIME140_1.dll", "MSVCP140.dll")
$results.VCRuntimeInstalled = $true
foreach ($dll in $vcDlls) {
    if (-not (Test-Path "$env:SYSTEMROOT\System32\$dll")) {
        $results.VCRuntimeInstalled = $false
        break
    }
}

# Check Defender
try {
    $defender = Get-MpPreference
    $results.DefenderActive = -not $defender.DisableRealtimeMonitoring
} catch { }

# Check disk space
$drive = Get-PSDrive C
$results.DiskSpaceGB = [math]::Round($drive.Free / 1GB, 2)

# Check microphone
$results.MicrophoneFound = (Get-PnpDevice -Class AudioEndpoint -Status OK | Where-Object { $_.FriendlyName -match "Microphone" }).Count -gt 0

# Display results
Write-Host "System State:" -ForegroundColor Cyan
$results.GetEnumerator() | ForEach-Object {
    $color = if ($_.Key -match "Installed|Found" -and -not $_.Value) { "Red" } else { "Green" }
    Write-Host "  $($_.Key): $($_.Value)" -ForegroundColor $color
}

# Test 2: Download VoiceLite
Write-Host "`n[TEST 2] Downloading VoiceLite..." -ForegroundColor Yellow

try {
    # Try to download from GitHub
    $zipPath = Join-Path $TestDir "VoiceLite.zip"
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $zipPath -UseBasicParsing
    Write-Host "  Downloaded successfully" -ForegroundColor Green

    # Extract
    Expand-Archive -Path $zipPath -DestinationPath $TestDir -Force
    Write-Host "  Extracted successfully" -ForegroundColor Green
} catch {
    Write-Host "  Download failed: $_" -ForegroundColor Red
    Write-Host "  Please manually download and extract VoiceLite to: $TestDir" -ForegroundColor Yellow
    Read-Host "Press Enter after extracting files"
}

# Test 3: First Run
Write-Host "`n[TEST 3] Testing First Run..." -ForegroundColor Yellow

$exePath = Get-ChildItem -Path $TestDir -Filter "VoiceLite.exe" -Recurse | Select-Object -First 1

if ($exePath) {
    Write-Host "  Found VoiceLite.exe at: $($exePath.FullName)" -ForegroundColor Green

    # Check if files are blocked
    $blockedFiles = Get-ChildItem -Path $TestDir -Recurse -Include "*.exe","*.dll" | Where-Object {
        Get-Item $_.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue
    }

    if ($blockedFiles) {
        Write-Host "  Warning: $($blockedFiles.Count) files are blocked by Windows" -ForegroundColor Yellow
        Write-Host "  VoiceLite should auto-unblock these" -ForegroundColor Gray
    }

    # Start VoiceLite
    Write-Host "  Starting VoiceLite..." -ForegroundColor Cyan
    $process = Start-Process -FilePath $exePath.FullName -PassThru

    # Wait and check if still running
    Start-Sleep -Seconds 5

    if ($process.HasExited) {
        Write-Host "  ERROR: VoiceLite exited with code: $($process.ExitCode)" -ForegroundColor Red

        # Check for common error codes
        switch ($process.ExitCode) {
            -532462766 { Write-Host "  Likely missing DLL dependencies" -ForegroundColor Red }
            default { Write-Host "  Unknown error - check Event Viewer" -ForegroundColor Red }
        }
    } else {
        Write-Host "  VoiceLite is running!" -ForegroundColor Green
        Write-Host "  PID: $($process.Id)" -ForegroundColor Gray

        # Test 4: Wait for user interaction
        Write-Host "`n[TEST 4] Manual Testing Required:" -ForegroundColor Yellow
        Write-Host "  1. Check if dependency installer appeared" -ForegroundColor Cyan
        Write-Host "  2. Click 'Yes' to install VC++ Runtime" -ForegroundColor Cyan
        Write-Host "  3. Test microphone with Left Alt key" -ForegroundColor Cyan
        Write-Host "  4. Verify transcription works" -ForegroundColor Cyan

        $testPassed = Read-Host "`nDid all tests pass? (Y/N)"

        if ($testPassed -eq 'Y') {
            $results.AllTestsPassed = $true
            Write-Host "`n✓ All tests passed!" -ForegroundColor Green
        } else {
            $results.AllTestsPassed = $false
            $results.FailureNotes = Read-Host "What failed?"
        }
    }
} else {
    Write-Host "  ERROR: VoiceLite.exe not found!" -ForegroundColor Red
}

# Test 5: Generate Report
Write-Host "`n[TEST 5] Generating Report..." -ForegroundColor Yellow

$reportPath = Join-Path $TestDir "test-report.json"
$results | ConvertTo-Json -Depth 5 | Out-File $reportPath

Write-Host "  Report saved to: $reportPath" -ForegroundColor Green

# Display summary
Write-Host "`n==================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan

if ($results.AllTestsPassed) {
    Write-Host "✓ Installation successful on fresh Windows!" -ForegroundColor Green
} else {
    Write-Host "✗ Installation had issues" -ForegroundColor Red
    if ($results.FailureNotes) {
        Write-Host "  Issues: $($results.FailureNotes)" -ForegroundColor Yellow
    }
}

Write-Host "`nRecommendations:" -ForegroundColor Cyan
if (-not $results.VCRuntimeInstalled) {
    Write-Host "  - VC++ Runtime auto-installer should have triggered" -ForegroundColor Yellow
}
if (-not $results.DotNetInstalled) {
    Write-Host "  - .NET installer may be needed" -ForegroundColor Yellow
}
if ($results.DefenderActive) {
    Write-Host "  - Watch for Windows Defender interference" -ForegroundColor Yellow
}

Write-Host "`nPress any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
```

## Manual Testing Checklist

### ✅ Pre-Installation
- [ ] Fresh Windows VM created
- [ ] No VC++ Runtime installed
- [ ] No .NET 8 installed
- [ ] Windows Defender active
- [ ] Microphone enabled in VM

### ✅ Download & Extract
- [ ] Download from GitHub releases
- [ ] Extract to Desktop (not system folder)
- [ ] Files show as "blocked" in properties

### ✅ First Run
- [ ] Double-click VoiceLite.exe
- [ ] Dependency checker appears
- [ ] Prompts to install VC++ Runtime
- [ ] Installation succeeds
- [ ] First-run wizard appears

### ✅ Functionality
- [ ] Microphone test works
- [ ] Transcription test succeeds
- [ ] Hotkey (Left Alt) works
- [ ] Text injection works in Notepad
- [ ] System tray icon appears

### ✅ Error Scenarios
- [ ] Move to Program Files → Shows permission error
- [ ] Delete whisper.exe → Shows missing file error
- [ ] Delete model file → Shows model error
- [ ] Block with antivirus → Shows AV warning

## Expected Timeline

1. **0-30 seconds**: App starts, detects missing dependencies
2. **30-60 seconds**: User clicks to install VC++ Runtime
3. **1-3 minutes**: VC++ downloads and installs
4. **3-4 minutes**: App restarts, first-run wizard appears
5. **4-5 minutes**: User completes setup wizard
6. **5+ minutes**: App is fully functional

## Common Issues & Solutions

| Issue | Expected App Response |
|-------|----------------------|
| Missing VC++ Runtime | Auto-prompts to install |
| Files blocked by Windows | Auto-unblocks on startup |
| Antivirus blocking | Shows clear message with solution |
| No microphone | Shows warning but allows continue |
| In Program Files | Suggests moving to user folder |
| Timeout on first run | Extended 60s timeout with explanation |

## Success Criteria

✅ **Installation succeeds if:**
- User can go from download to working app in <5 minutes
- No manual file downloads required
- No command-line usage needed
- Clear error messages for any issues
- One-click fixes for all common problems

---

After running these tests, update the GitHub release notes with any findings!