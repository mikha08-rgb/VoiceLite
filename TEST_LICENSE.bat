@echo off
echo ========================================
echo VoiceLite License Test Tool
echo ========================================
echo.
echo Generating a test license for immediate use...
echo.
cd license-server
node admin.js generate test@voicelite.app Personal
echo.
echo ========================================
echo Use this license key in VoiceLite:
echo Help menu → Enter License
echo ========================================
pause