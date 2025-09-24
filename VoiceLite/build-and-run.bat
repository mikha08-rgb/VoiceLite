@echo off
echo Restoring NuGet packages...
dotnet restore VoiceLite.sln

if %ERRORLEVEL% NEQ 0 (
    echo Failed to restore packages
    pause
    exit /b 1
)

echo.
echo Building project...
dotnet build VoiceLite.sln -c Debug

if %ERRORLEVEL% NEQ 0 (
    echo Build failed
    pause
    exit /b 1
)

echo.
echo Build successful!
echo Starting VoiceLite...
start VoiceLite\bin\Debug\net8.0-windows\VoiceLite.exe