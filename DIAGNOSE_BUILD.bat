@echo off
cls
echo ================================================
echo           DIAGNOSING BUILD FAILURE
echo ================================================
echo.
echo Running detailed build to find the error...
echo.
echo ------------------------------------------------
dotnet build VoiceLite\VoiceLite.sln -c Release
echo ------------------------------------------------
echo.
echo Build errors are shown above.
echo.
echo Common issues:
echo 1. Missing 'using VoiceLite.Helpers;' in files that use AsyncHelper
echo 2. Test files not properly added to test project
echo 3. Namespace issues
echo.
pause