@echo off
cls
echo ================================================
echo        QUICK WEEK 1 VERIFICATION TEST
echo ================================================
echo.

echo [1] Checking if project builds...
dotnet build VoiceLite\VoiceLite.sln -c Release --nologo --verbosity quiet
if %errorlevel% equ 0 (
    echo     SUCCESS - Project builds!
) else (
    echo     FAILED - Build errors found
    echo     Run: dotnet build VoiceLite\VoiceLite.sln
)
echo.

echo [2] Checking AsyncHelper exists...
if exist "VoiceLite\VoiceLite\Helpers\AsyncHelper.cs" (
    echo     SUCCESS - AsyncHelper.cs found!
) else (
    echo     FAILED - AsyncHelper.cs missing
)
echo.

echo [3] Checking HttpClient is static...
findstr /C:"private static readonly HttpClient" VoiceLite\VoiceLite\Services\LicenseService.cs >nul 2>&1
if %errorlevel% equ 0 (
    echo     SUCCESS - HttpClient is static!
) else (
    echo     FAILED - HttpClient not static
)
echo.

echo [4] Checking timer management fixed...
findstr /C:"Dictionary.*activeStatusTimers" VoiceLite\VoiceLite\MainWindow.xaml.cs >nul 2>&1
if %errorlevel% equ 0 (
    echo     SUCCESS - Timer management updated!
) else (
    echo     FAILED - Timer management not fixed
)
echo.

echo [5] Running tests...
dotnet test VoiceLite\VoiceLite.Tests\VoiceLite.Tests.csproj --nologo --verbosity quiet
if %errorlevel% equ 0 (
    echo     SUCCESS - Tests pass!
) else (
    echo     WARNING - Some tests failed or not found
    echo     Run: dotnet test VoiceLite\VoiceLite.Tests\VoiceLite.Tests.csproj
)
echo.

echo ================================================
echo                    SUMMARY
echo ================================================
echo.
echo If you see mostly SUCCESS above, Week 1 is working!
echo If you see FAILED messages, we need to fix those first.
echo.
echo ================================================
pause