@echo off
REM Batch script to run tests with coverage

echo Running VoiceLite Tests with Coverage...

REM Clean previous results
if exist TestResults (
    rmdir /s /q TestResults
)

REM Run tests with coverage
dotnet test VoiceLite.Tests\VoiceLite.Tests.csproj ^
    --configuration Release ^
    --logger "console;verbosity=normal" ^
    --logger "html;logfilename=testresults.html" ^
    --collect:"XPlat Code Coverage" ^
    --settings VoiceLite.Tests\coverlet.runsettings ^
    --results-directory TestResults

REM Check if tests passed
if %ERRORLEVEL% NEQ 0 (
    echo Tests failed!
    exit /b %ERRORLEVEL%
)

echo.
echo Tests completed successfully!
echo Test results available in TestResults\testresults.html
echo.

REM Generate simple summary
echo Test Summary:
echo -------------
dotnet test VoiceLite.Tests\VoiceLite.Tests.csproj --no-build --no-restore --verbosity quiet --logger:"console;verbosity=minimal"

pause