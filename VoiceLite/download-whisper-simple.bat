@echo off
echo ===========================================
echo    VoiceLite - Whisper Setup (Simple)
echo ===========================================
echo.

set WHISPER_DIR=VoiceLite\whisper

if not exist %WHISPER_DIR% (
    mkdir %WHISPER_DIR%
    echo Created whisper directory
)

echo.
echo This script will download:
echo 1. Whisper.cpp Windows binary
echo 2. Small Whisper model (466 MB)
echo.
echo Please download these files manually:
echo.

echo ---------------------------------------------
echo WHISPER.CPP BINARY
echo ---------------------------------------------
echo Download from:
echo https://github.com/ggerganov/whisper.cpp/releases/latest
echo.
echo Look for: whisper-bin-x64.zip or similar
echo Extract and rename main.exe to whisper.exe
echo Place in: %CD%\%WHISPER_DIR%\whisper.exe
echo.

echo ---------------------------------------------
echo WHISPER MODEL (ggml-small.bin)
echo ---------------------------------------------
echo Download from:
echo https://huggingface.co/ggerganov/whisper.cpp/blob/main/ggml-small.bin
echo.
echo Click "download" button on the page
echo Place in: %CD%\%WHISPER_DIR%\ggml-small.bin
echo.

echo ---------------------------------------------
echo ALTERNATIVE: Using curl (if available)
echo ---------------------------------------------
echo.

echo Attempting to download small model with curl...
curl -L -o %WHISPER_DIR%\ggml-small.bin https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin

if exist %WHISPER_DIR%\ggml-small.bin (
    echo Model downloaded successfully!
) else (
    echo Could not download automatically. Please download manually.
)

echo.
echo Press any key to check installation status...
pause > nul

echo.
echo Checking installation:
echo ----------------------

if exist %WHISPER_DIR%\whisper.exe (
    echo [OK] whisper.exe found
) else (
    echo [MISSING] whisper.exe not found - please download manually
)

if exist %WHISPER_DIR%\ggml-small.bin (
    echo [OK] ggml-small.bin found
    for %%I in (%WHISPER_DIR%\ggml-small.bin) do echo      Size: %%~zI bytes
) else (
    echo [MISSING] ggml-small.bin not found - please download manually
)

echo.
echo Setup check complete!
pause