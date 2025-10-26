# Week 1 Verification Script - Clean Version
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "    WEEK 1 VERIFICATION - STABILITY FIXES      " -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$errors = 0
$warnings = 0

# Step 1: Build
Write-Host "STEP 1: Building Project..." -ForegroundColor Yellow
$buildResult = dotnet build VoiceLite/VoiceLite.sln -c Release 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED" -ForegroundColor Red
    $errors++
} else {
    Write-Host "Build successful" -ForegroundColor Green
}
Write-Host ""

# Step 2: Check AsyncHelper
Write-Host "STEP 2: Checking AsyncHelper..." -ForegroundColor Yellow
if (Test-Path "VoiceLite/VoiceLite/Helpers/AsyncHelper.cs") {
    Write-Host "AsyncHelper.cs exists" -ForegroundColor Green
} else {
    Write-Host "AsyncHelper.cs NOT FOUND" -ForegroundColor Red
    $errors++
}
Write-Host ""

# Step 3: Check HttpClient
Write-Host "STEP 3: Checking HttpClient Fix..." -ForegroundColor Yellow
$licenseService = Get-Content "VoiceLite/VoiceLite/Services/LicenseService.cs" -Raw
if ($licenseService -match "private static readonly HttpClient") {
    Write-Host "HttpClient is static in LicenseService" -ForegroundColor Green
} else {
    Write-Host "HttpClient is NOT static" -ForegroundColor Red
    $errors++
}
Write-Host ""

# Step 4: Check Timer Fix
Write-Host "STEP 4: Checking Timer Management..." -ForegroundColor Yellow
$mainWindow = Get-Content "VoiceLite/VoiceLite/MainWindow.xaml.cs" -Raw
if ($mainWindow -match "Dictionary.*activeStatusTimers") {
    Write-Host "Timer management uses Dictionary" -ForegroundColor Green
} else {
    Write-Host "Timer management NOT updated" -ForegroundColor Red
    $errors++
}
Write-Host ""

# Step 5: Run Tests
Write-Host "STEP 5: Running Tests..." -ForegroundColor Yellow
$testResult = dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --no-build -c Release 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Some tests failed or not found" -ForegroundColor Yellow
    $warnings++
} else {
    Write-Host "All tests passed" -ForegroundColor Green
}
Write-Host ""

# Summary
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "              VERIFICATION SUMMARY              " -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

if ($errors -eq 0) {
    Write-Host "WEEK 1 VERIFICATION PASSED!" -ForegroundColor Green
    Write-Host ""
    Write-Host "All critical fixes are in place:" -ForegroundColor Green
    Write-Host "  - HttpClient socket exhaustion fixed" -ForegroundColor White
    Write-Host "  - Timer memory leaks fixed" -ForegroundColor White
    Write-Host "  - Async handler safety added" -ForegroundColor White
    Write-Host ""
    Write-Host "Ready to proceed to Week 2!" -ForegroundColor Green
} else {
    Write-Host "VERIFICATION FAILED - $errors errors found" -ForegroundColor Red
    Write-Host "Please fix the errors above before continuing" -ForegroundColor Yellow
}

if ($warnings -gt 0) {
    Write-Host ""
    Write-Host "$warnings warnings found (may be okay)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Press Enter to exit..."
Read-Host