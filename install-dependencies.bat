@echo off
echo ==============================================
echo VoiceLite Dependency Installer
echo ==============================================
echo.
echo This will install required components for VoiceLite:
echo 1. Visual C++ Runtime (Required for speech recognition)
echo 2. .NET 8 Desktop Runtime (Required for app)
echo.
pause

echo.
echo Checking for Visual C++ Runtime...
reg query "HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" >nul 2>&1
if %errorlevel% == 0 (
    echo [OK] Visual C++ Runtime is already installed
) else (
    echo [MISSING] Downloading Visual C++ Runtime...
    echo.
    echo Please wait...
    powershell -Command "Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile '%TEMP%\vc_redist.x64.exe'"
    echo Installing Visual C++ Runtime...
    "%TEMP%\vc_redist.x64.exe" /quiet /norestart
    echo [DONE] Visual C++ Runtime installed
)

echo.
echo Checking for .NET 8 Desktop Runtime...
dotnet --list-runtimes | findstr "Microsoft.WindowsDesktop.App 8." >nul 2>&1
if %errorlevel% == 0 (
    echo [OK] .NET 8 Desktop Runtime is already installed
) else (
    echo [MISSING] .NET 8 Desktop Runtime not found
    echo.
    echo Please download and install from:
    echo https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime
    echo Choose: Windows x64 - Desktop Runtime
    echo.
    start https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime
)

echo.
echo ==============================================
echo Installation check complete!
echo.
echo You can now run VoiceLite.exe
echo ==============================================
echo.
pause