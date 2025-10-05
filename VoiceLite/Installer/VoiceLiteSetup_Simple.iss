; Simple Inno Setup Script for VoiceLite
; v1.0.32: Critical bug fixes - 23 bugs resolved (race conditions, memory leaks, CSRF protection)
; Windows 10/11 (64-bit) compatible - auto-installs VC++ Runtime if needed

[Setup]
AppId={{A06BC0AA-DD0A-4341-9E41-68AC0D6E541E}
AppName=VoiceLite
AppVersion=1.0.32
AppPublisher=VoiceLite
AppPublisherURL=https://voicelite.app
AppSupportURL=https://voicelite.app
AppUpdatesURL=https://voicelite.app
DefaultDirName={autopf}\VoiceLite
DisableProgramGroupPage=yes
OutputDir=..\..\
OutputBaseFilename=VoiceLite-Setup-1.0.32
SetupIconFile=..\VoiceLite\VoiceLite.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\..\EULA.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main application files
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\VoiceLite.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\*.json"; DestDir: "{app}"; Flags: ignoreversion

; Whisper files (Small + Tiny models - Small is new free tier default, Tiny is legacy fallback)
Source: "..\whisper_installer\*"; DestDir: "{app}\whisper"; Flags: ignoreversion recursesubdirs

; Icon file
Source: "..\VoiceLite\VoiceLite.ico"; DestDir: "{app}"; Flags: ignoreversion

; VC++ Redistributable installer (bundled for offline installation)
Source: "..\dependencies\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\VoiceLite"; Filename: "{app}\VoiceLite.exe"
Name: "{autodesktop}\VoiceLite"; Filename: "{app}\VoiceLite.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VoiceLite.exe"; Description: "{cm:LaunchProgram,VoiceLite}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{localappdata}\VoiceLite"

[Code]
// Check for Visual C++ Runtime 2015-2022 (Windows 10/11 compatible)
function IsVCRuntimeInstalled: Boolean;
var
  Installed: Cardinal;
begin
  Result := False;

  // Check for VC++ 2015-2022 x64 Runtime
  if RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
                         'Installed', Installed) then
  begin
    Result := (Installed = 1);
  end;

  // Also check alternative location for newer versions
  if not Result then
  begin
    if RegQueryDWordValue(HKLM64, 'SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
                           'Installed', Installed) then
    begin
      Result := (Installed = 1);
    end;
  end;
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  // VC++ Runtime check moved to InstallVCRuntimeIfNeeded (called during installation)
  // This allows the bundled installer to run automatically
end;

procedure InstallVCRuntimeIfNeeded;
var
  ResultCode: Integer;
  VCInstaller: String;
begin
  // Skip if already installed
  if IsVCRuntimeInstalled then
  begin
    Log('VC++ Runtime already installed, skipping');
    Exit;
  end;

  Log('VC++ Runtime not detected, installing bundled version...');

  VCInstaller := ExpandConstant('{tmp}\vc_redist.x64.exe');

  if not FileExists(VCInstaller) then
  begin
    MsgBox('Visual C++ Runtime installer is missing from setup package.' + #13#10 +
           'Please download it manually from:' + #13#10 +
           'https://aka.ms/vs/17/release/vc_redist.x64.exe',
           mbError, MB_OK);
    Exit;
  end;

  // Install silently without restart
  if Exec(VCInstaller, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if (ResultCode = 0) or (ResultCode = 3010) then
    begin
      Log('VC++ Runtime installed successfully (exit code: ' + IntToStr(ResultCode) + ')');

      if ResultCode = 3010 then
      begin
        MsgBox('Microsoft Visual C++ Runtime installed successfully.' + #13#10#13#10 +
               'Note: A system restart may be required for changes to take full effect.',
               mbInformation, MB_OK);
      end;
    end
    else if ResultCode = 1638 then
    begin
      Log('VC++ Runtime already installed (detected by installer)');
    end
    else
    begin
      MsgBox('Visual C++ Runtime installation completed with code: ' + IntToStr(ResultCode) + #13#10 +
             'VoiceLite may not work correctly. Please restart your computer after installation.',
             mbInformation, MB_OK);
    end;
  end
  else
  begin
    MsgBox('Failed to install Visual C++ Runtime.' + #13#10 +
           'Please install it manually from:' + #13#10 +
           'https://aka.ms/vs/17/release/vc_redist.x64.exe',
           mbError, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AppDataDir: String;
  LogsDir: String;
begin
  if CurStep = ssInstall then
  begin
    // Install VC++ Runtime BEFORE installing VoiceLite files
    InstallVCRuntimeIfNeeded;
  end;

  if CurStep = ssPostInstall then
  begin
    // Create AppData directories for settings and logs
    AppDataDir := ExpandConstant('{userappdata}\VoiceLite');
    LogsDir := AppDataDir + '\logs';

    try
      if not DirExists(AppDataDir) then
        CreateDir(AppDataDir);

      if not DirExists(LogsDir) then
        CreateDir(LogsDir);
    except
      // Silently fail - the app will create directories on first run if needed
    end;

    // Final verification that VC++ Runtime is now installed
    if not IsVCRuntimeInstalled then
    begin
      MsgBox('Warning: Visual C++ Runtime may not be properly installed.' + #13#10#13#10 +
             'If VoiceLite fails to start, please:' + #13#10 +
             '1. Restart your computer' + #13#10 +
             '2. Download VC++ Runtime from: https://aka.ms/vs/17/release/vc_redist.x64.exe' + #13#10 +
             '3. Install it and restart again',
             mbInformation, MB_OK);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('Do you want to remove VoiceLite settings and temporary files?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\VoiceLite'), True, True, True);
    end;
  end;
end;