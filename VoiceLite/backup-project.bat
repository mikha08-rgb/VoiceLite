@echo off
echo Creating backup of VoiceLite project...

set DATETIME=%date:~-4%%date:~4,2%%date:~7,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set DATETIME=%DATETIME: =0%

set BACKUP_NAME=VoiceLite_Backup_%DATETIME%

echo Creating backup folder: %BACKUP_NAME%
mkdir "..\%BACKUP_NAME%"

echo Copying source files...
xcopy /E /I /Q /H /Y "VoiceLite" "..\%BACKUP_NAME%\VoiceLite"
copy "*.sln" "..\%BACKUP_NAME%\"
copy "*.md" "..\%BACKUP_NAME%\"
copy "*.bat" "..\%BACKUP_NAME%\"
copy "*.ps1" "..\%BACKUP_NAME%\"
copy ".gitignore" "..\%BACKUP_NAME%\"

echo.
echo Backup created successfully at: ..\%BACKUP_NAME%
echo.
pause