@echo off
REM Week 1 Verification Script (Windows Batch)
REM This script tests all Week 1 changes

echo ================================================
echo     WEEK 1 VERIFICATION - STABILITY FIXES
echo ================================================
echo.

REM Step 1: Build
echo STEP 1: Building Project...
echo ----------------------------
dotnet build VoiceLite\VoiceLite.sln -c Release >nul 2>&1
if %errorlevel% neq 0 (
    echo [FAILED] Build failed - run "dotnet build VoiceLite\VoiceLite.sln" to see errors
    set /a errors+=1
) else (
    echo [PASS] Build successful
)
echo.

REM Step 2: Run all tests
echo STEP 2: Running All Tests...
echo ----------------------------
dotnet test VoiceLite\VoiceLite.Tests\VoiceLite.Tests.csproj --no-build -c Release --verbosity quiet
if %errorlevel% neq 0 (
    echo [FAILED] Some tests failed
    echo Run this to see details: dotnet test VoiceLite\VoiceLite.Tests\VoiceLite.Tests.csproj
    set /a errors+=1
) else (
    echo [PASS] All tests passed
)
echo.

REM Step 3: Check critical files exist
echo STEP 3: Checking Critical Files...
echo -----------------------------------
if exist "VoiceLite\VoiceLite\Helpers\AsyncHelper.cs" (
    echo [PASS] AsyncHelper.cs exists
) else (
    echo [FAILED] AsyncHelper.cs not found
    set /a errors+=1
)

if exist "VoiceLite\VoiceLite.Tests\Resources\ResourceLeakTests.cs" (
    echo [PASS] ResourceLeakTests.cs exists
) else (
    echo [FAILED] ResourceLeakTests.cs not found
    set /a errors+=1
)

if exist "VoiceLite\VoiceLite.Tests\Integration\EndToEndTests.cs" (
    echo [PASS] EndToEndTests.cs exists
) else (
    echo [FAILED] EndToEndTests.cs not found
    set /a errors+=1
)
echo.

REM Step 4: Check if HttpClient is static
echo STEP 4: Verifying HttpClient Fix...
echo ------------------------------------
findstr /C:"private static readonly HttpClient" VoiceLite\VoiceLite\Services\LicenseService.cs >nul
if %errorlevel% equ 0 (
    echo [PASS] HttpClient is static in LicenseService
) else (
    echo [FAILED] HttpClient is NOT static in LicenseService
    set /a errors+=1
)
echo.

REM Summary
echo ================================================
echo               VERIFICATION SUMMARY
echo ================================================
if defined errors (
    echo VERIFICATION FAILED - Fix errors above before Week 2
    echo.
    echo To debug, run these commands:
    echo   dotnet build VoiceLite\VoiceLite.sln
    echo   dotnet test VoiceLite\VoiceLite.Tests\VoiceLite.Tests.csproj
) else (
    echo SUCCESS! All Week 1 changes verified!
    echo.
    echo Fixed:
    echo   - HttpClient socket exhaustion
    echo   - Timer memory leaks
    echo   - Async handler crashes
    echo   - Added integration tests
    echo.
    echo Ready for Week 2!
)
echo ================================================
pause