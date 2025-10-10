@echo off
REM Git History Cleanup Script
REM This script removes exposed secrets from git history using BFG Repo-Cleaner

echo ========================================
echo VoiceLite Git History Cleanup
echo ========================================
echo.
echo WARNING: This will rewrite git history!
echo All collaborators must force-pull after this operation.
echo.
echo Press Ctrl+C to cancel, or
pause

echo.
echo Step 1: Removing .claude/settings.local.json from all commits...
java -jar bfg.jar --delete-files "settings.local.json" --no-blob-protection

echo.
echo Step 2: Replacing secret strings with ***REMOVED***...
java -jar bfg.jar --replace-text secrets.txt --no-blob-protection

echo.
echo Step 3: Cleaning git refs and garbage collecting...
git reflog expire --expire=now --all
git gc --prune=now --aggressive

echo.
echo Step 4: Verifying cleanup...
echo Checking for .claude/settings.local.json in history...
git log --all --full-history -- ".claude/settings.local.json" --oneline

echo.
echo Checking for database credentials in history...
git log --all -S "jY%26%23DvbBo2a" --source --oneline

echo.
echo ========================================
echo Cleanup Complete!
echo ========================================
echo.
echo NEXT STEPS:
echo 1. Review the output above - should show no results
echo 2. Force-push to remote: git push --force --all
echo 3. Notify all collaborators to delete and re-clone
echo 4. Complete manual key rotation (see SECURITY_ROTATION_GUIDE.md)
echo.
pause
