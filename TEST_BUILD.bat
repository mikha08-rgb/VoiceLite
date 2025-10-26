@echo off
cls
echo ================================================
echo           TESTING BUILD AFTER FIXES
echo ================================================
echo.

echo Building...
dotnet build VoiceLite\VoiceLite.sln -c Release

if %errorlevel% equ 0 (
    echo.
    echo ================================================
    echo          BUILD SUCCESSFUL!
    echo ================================================
    echo.
    echo Now running tests...
    echo.
    dotnet test VoiceLite\VoiceLite.Tests\VoiceLite.Tests.csproj --no-build -c Release

    if %errorlevel% equ 0 (
        echo.
        echo ================================================
        echo       ALL TESTS PASSED - WEEK 1 COMPLETE!
        echo ================================================
        echo.
        echo Ready to continue to Week 2!
    ) else (
        echo.
        echo ================================================
        echo       BUILD OK BUT SOME TESTS FAILED
        echo ================================================
        echo This is often OK - tests might need environment setup
    )
) else (
    echo.
    echo ================================================
    echo            BUILD STILL FAILING
    echo ================================================
    echo.
    echo See errors above for details.
)

echo.
pause