@echo off
echo ========================================
echo VoiceLite System Cleanup Script
echo ========================================
echo.

REM Kill any running VoiceLite processes
echo [1/5] Stopping VoiceLite processes...
taskkill /F /IM VoiceLite.exe 2>nul
if %ERRORLEVEL% EQU 0 (
    echo   ^> VoiceLite process killed
    timeout /t 2 /nobreak >nul
) else (
    echo   ^> No VoiceLite processes running
)

REM Uninstall existing version
echo.
echo [2/5] Uninstalling VoiceLite...
wmic product where "name like '%%VoiceLite%%'" call uninstall /nointeractive 2>nul
if %ERRORLEVEL% EQU 0 (
    echo   ^> VoiceLite uninstalled
) else (
    echo   ^> No installer found, trying manual cleanup...
)

REM Delete installation directory
echo.
echo [3/5] Removing installation files...
if exist "%LOCALAPPDATA%\Programs\VoiceLite" (
    rmdir /S /Q "%LOCALAPPDATA%\Programs\VoiceLite"
    echo   ^> Installation directory removed
) else (
    echo   ^> No installation directory found
)

REM Delete settings and logs
echo.
echo [4/5] Removing settings and logs...
if exist "%LOCALAPPDATA%\VoiceLite" (
    rmdir /S /Q "%LOCALAPPDATA%\VoiceLite"
    echo   ^> Settings and logs removed
) else (
    echo   ^> No settings directory found
)

REM Delete temp audio files
echo.
echo [5/5] Cleaning temp files...
del /Q "%TEMP%\voicelite_*.wav" 2>nul
if %ERRORLEVEL% EQU 0 (
    echo   ^> Temp audio files removed
) else (
    echo   ^> No temp files found
)

echo.
echo ========================================
echo Cleanup Complete!
echo ========================================
echo.
echo Your system is now clean and ready for a fresh VoiceLite installation.
echo.
echo Next steps:
echo 1. Download v1.0.89 from: https://voicelite.app
echo 2. Install the new version
echo 3. Test transcription functionality
echo.
pause
