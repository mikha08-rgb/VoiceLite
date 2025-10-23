@echo off
REM Post-build obfuscation script for VoiceLite
REM This script runs ConfuserEx on the Release build

SET CONFIG_FILE=VoiceLite.crproj
SET CONFUSER_PATH=..\Tools\ConfuserEx\Confuser.CLI.exe
SET OUTPUT_DIR=.\bin\Release\net8.0-windows\Obfuscated

echo ====================================
echo Starting VoiceLite Obfuscation...
echo ====================================

REM Check if this is a Release build
IF NOT "%1"=="Release" (
    echo Skipping obfuscation for Debug build
    exit /b 0
)

REM Create output directory
IF NOT EXIST "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

REM Check if ConfuserEx exists
IF NOT EXIST "%CONFUSER_PATH%" (
    echo WARNING: ConfuserEx not found at %CONFUSER_PATH%
    echo Download from: https://github.com/mkaring/ConfuserEx/releases
    echo Place in: ..\Tools\ConfuserEx\
    exit /b 0
)

REM Copy .NET 8 dependencies to help ConfuserEx resolve them
echo Copying dependencies...
xcopy /Y ".\bin\Release\net8.0-windows\*.dll" ".\bin\Release\net8.0-windows\" 2>nul

REM Run ConfuserEx
echo Running ConfuserEx...
"%CONFUSER_PATH%" -n "%CONFIG_FILE%"

IF %ERRORLEVEL% NEQ 0 (
    echo ERROR: Obfuscation failed!
    exit /b 1
)

echo ====================================
echo Obfuscation completed successfully!
echo Output: %OUTPUT_DIR%
echo ====================================

REM Copy additional files to obfuscated output
echo Copying dependencies...
xcopy /Y ".\bin\Release\net8.0-windows\*.config" "%OUTPUT_DIR%\" 2>nul
xcopy /Y ".\bin\Release\net8.0-windows\*.json" "%OUTPUT_DIR%\" 2>nul
xcopy /Y /E /I ".\bin\Release\net8.0-windows\whisper" "%OUTPUT_DIR%\whisper" 2>nul

echo Done!
exit /b 0