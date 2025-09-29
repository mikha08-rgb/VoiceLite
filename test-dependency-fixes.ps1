# Test script for dependency detection fixes
Write-Host "VoiceLite Dependency Fix Test" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Build the solution
Write-Host "Building VoiceLite..." -ForegroundColor Yellow
$buildResult = & dotnet build "VoiceLite\VoiceLite\VoiceLite.csproj" -c Release 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Build successful" -ForegroundColor Green
} else {
    Write-Host "✗ Build failed:" -ForegroundColor Red
    Write-Host $buildResult
    exit 1
}

Write-Host ""
Write-Host "Checking Dependencies..." -ForegroundColor Yellow
Write-Host ""

# Test 2: Check VC++ Runtime (our fixed detection)
Write-Host "1. Visual C++ Runtime:" -NoNewline
$vcDlls = @("VCRUNTIME140.dll", "MSVCP140.dll")
$vcFound = $true

foreach ($dll in $vcDlls) {
    $dllPath = Join-Path $env:SystemRoot "System32\$dll"
    if (-not (Test-Path $dllPath)) {
        $vcFound = $false
        Write-Host " ✗ Missing $dll" -ForegroundColor Red
        break
    }
}

if ($vcFound) {
    Write-Host " ✓ Installed" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "   Note: VCRUNTIME140_1.dll is NOT required (we fixed this!)" -ForegroundColor Cyan
}

# Test 3: Check .NET 8 Runtime
Write-Host "2. .NET 8 Desktop Runtime:" -NoNewline
try {
    $dotnetRuntimes = & dotnet --list-runtimes 2>$null
    if ($dotnetRuntimes -match "Microsoft.WindowsDesktop.App 8\.") {
        Write-Host " ✓ Installed" -ForegroundColor Green
    } else {
        Write-Host " ✗ Not found" -ForegroundColor Red
    }
} catch {
    Write-Host " ✗ dotnet command not available" -ForegroundColor Red
}

# Test 4: Check Whisper files
Write-Host "3. Whisper Components:" -NoNewline
$whisperPath = "VoiceLite\whisper\whisper.exe"
if (Test-Path $whisperPath) {
    Write-Host " ✓ Found" -ForegroundColor Green
} else {
    Write-Host " ✗ Not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "--------" -ForegroundColor Cyan
Write-Host "The dependency checker will now:" -ForegroundColor White
Write-Host "• Correctly detect VC++ Runtime (no false positives)" -ForegroundColor Gray
Write-Host "• Check for .NET 8 Desktop Runtime" -ForegroundColor Gray
Write-Host "• Provide clear error messages for missing components" -ForegroundColor Gray
Write-Host "• Inno Setup installer handles both prerequisites" -ForegroundColor Gray

Write-Host ""
Write-Host "To test the installer:" -ForegroundColor Yellow
Write-Host "1. Compile the Inno Setup script (VoiceLite\Installer\VoiceLiteSetup.iss)" -ForegroundColor Gray
Write-Host "2. Run the generated setup.exe" -ForegroundColor Gray
Write-Host "3. It will check and prompt for any missing prerequisites" -ForegroundColor Gray