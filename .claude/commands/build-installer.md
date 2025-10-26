---
description: Build VoiceLite installer for distribution
---

# Build VoiceLite Installer

Complete installer build process:

## Pre-checks
!`tasklist | grep -i VoiceLite.exe || echo "✓ No VoiceLite running"`

## Build Steps
1. Clean and rebuild in Release mode:
   ```
   dotnet clean VoiceLite/VoiceLite.sln
   dotnet build VoiceLite/VoiceLite.sln -c Release
   ```

2. Publish self-contained:
   ```
   dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
   ```

3. Verify whisper model present:
   - Check VoiceLite/whisper/ggml-tiny.bin exists (42MB)

4. Build installer:
   ```
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /Q VoiceLite/Installer/VoiceLiteSetup.iss
   ```

5. Report installer size and location