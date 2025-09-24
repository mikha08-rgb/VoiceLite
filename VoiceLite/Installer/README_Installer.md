# Creating VoiceLite Installer

## Quick Start with Inno Setup (Recommended)

1. **Download Inno Setup** from https://jrsoftware.org/isdl.php

2. **Build your application in Release mode:**
   ```powershell
   cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v2.9\VoiceLite"
   dotnet publish VoiceLite\VoiceLite.csproj -c Release -r win-x64 --self-contained false
   ```

3. **Generate GUIDs** for the installer:
   - Open PowerShell and run: `[guid]::NewGuid()`
   - Replace `PUT-GUID-HERE` in VoiceLiteSetup.iss with the generated GUID

4. **Compile the installer:**
   - Open VoiceLiteSetup.iss in Inno Setup Compiler
   - Click Build → Compile
   - Your installer will be in the Output folder

## Alternative: MSI with WiX Toolset

1. **Install WiX Toolset** from https://wixtoolset.org/

2. **Install WiX VS Extension** for Visual Studio integration

3. **Generate GUIDs** and replace placeholders in Product.wxs

4. **Build MSI:**
   ```powershell
   candle Product.wxs
   light Product.wixobj -o VoiceLiteSetup.msi
   ```

## Alternative: Visual Studio Installer

1. In Visual Studio, install "Microsoft Visual Studio Installer Projects" extension

2. Add new project: Other Project Types → Visual Studio Installer → Setup Project

3. Add Primary Output from VoiceLite project

4. Configure:
   - Set ProductName: VoiceLite
   - Set Manufacturer: Your Company
   - Add shortcuts to Desktop and Start Menu
   - Set icon
   - Add launch condition for .NET 8.0

5. Build to create MSI file

## Prerequisites Handling

The installer checks for:
- **.NET Desktop Runtime 8.0** (required)
- **Windows 10 version 1903+** (recommended)
- **Visual C++ Redistributables** (for whisper.exe)

If missing, it prompts users to download from:
- .NET 8: https://dotnet.microsoft.com/download/dotnet/8.0/runtime
- VC++ Redist: https://aka.ms/vs/17/release/vc_redist.x64.exe

## Post-Installation

The installer creates:
- Program Files folder: `C:\Program Files\VoiceLite\`
- Start Menu shortcut
- Desktop shortcut (optional)
- Uninstall entry in Control Panel
- Temp folder: `%LOCALAPPDATA%\VoiceLite\temp\`

## Testing the Installer

1. Test on a clean Windows VM without .NET installed
2. Verify prerequisites check works
3. Test installation, shortcuts, and uninstallation
4. Check antivirus doesn't flag the installer
5. Test upgrade scenario (install over existing version)