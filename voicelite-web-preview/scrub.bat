@echo off
REM Git History Scrubbing - Windows Batch Script
REM Double-click this file to run

echo =====================================
echo GIT HISTORY SCRUBBING
echo =====================================
echo.

cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"

echo [Step 1] Removing old mirror...
rmdir /s /q "C:\Users\mishk\Codingprojects\SpeakLite\VoiceLite-mirror.git" 2>nul
echo Done.
echo.

echo [Step 2] Cloning fresh mirror from GitHub...
git clone --mirror https://github.com/mikha08-rgb/VoiceLite.git "C:\Users\mishk\Codingprojects\SpeakLite\VoiceLite-mirror.git"
echo Done.
echo.

echo [Step 3] Running BFG to redact secrets...
cd "C:\Users\mishk\Codingprojects\SpeakLite\VoiceLite-mirror.git"
java -jar "C:\Users\mishk\Downloads\bfg.jar" --replace-text "..\HereWeGoAgain v3.3 Fuck\secrets-to-redact.txt" .
echo Done.
echo.

echo [Step 4] Cleaning git references...
git reflog expire --expire=now --all
git gc --prune=now --aggressive
echo Done.
echo.

echo [Step 5] Verifying secrets removed...
git log --all --full-history -p -S "vS89Zv4vrDNoM9zXm5aAsba" --oneline > verify.txt
echo.
echo Checking verify.txt...
type verify.txt
echo.

set /p confirm="If verify.txt is EMPTY, type YES to force push: "
if /i "%confirm%" NEQ "YES" (
    echo Cancelled.
    pause
    exit /b
)

echo.
echo [Step 6] Force pushing to GitHub...
git push --force --all
git push --force --tags
echo Done.
echo.

echo [Step 7] Updating local repository...
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
git fetch origin
git reset --hard origin/master
echo Done.
echo.

echo =====================================
echo SCRUBBING COMPLETE!
echo =====================================
echo.
echo Verify on GitHub:
echo https://github.com/mikha08-rgb/VoiceLite/search?q=vS89Zv4vrDNoM9zXm5aAsba
echo.
pause
