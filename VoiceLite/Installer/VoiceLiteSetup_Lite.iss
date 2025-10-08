; Lite Inno Setup Script for VoiceLite
; v1.0.62-lite: Fixed VC++ Runtime installation timing - moved to [Run] section
; Windows 10/11 (64-bit) compatible - auto-installs VC++ Runtime if needed
; Pro model (466MB) can be downloaded from Settings after installation

[Setup]
AppId={{A06BC0AA-DD0A-4341-9E41-68AC0D6E541E}
AppName=VoiceLite
AppVersion=1.0.62
AppPublisher=VoiceLite
AppPublisherURL=https://voicelite.app
AppSupportURL=https://voicelite.app
AppUpdatesURL=https://voicelite.app
DefaultDirName={autopf}\VoiceLite
DisableProgramGroupPage=yes
OutputDir=..\..\
OutputBaseFilename=VoiceLite-Setup-Lite-1.0.62
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

; Whisper files (Tiny model only for lite installer)
Source: "..\whisper_installer_lite\*"; DestDir: "{app}\whisper"; Flags: ignoreversion recursesubdirs

; Icon file
Source: "..\VoiceLite\VoiceLite.ico"; DestDir: "{app}"; Flags: ignoreversion

; VC++ Redistributable installer (bundled for offline installation)
Source: "..\dependencies\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

; Antivirus exclusion PowerShell script
Source: "..\Installer\Add-VoiceLite-Exclusion.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\VoiceLite"; Filename: "{app}\VoiceLite.exe"
Name: "{autodesktop}\VoiceLite"; Filename: "{app}\VoiceLite.exe"; Tasks: desktopicon
Name: "{autodesktop}\Fix Antivirus Issues"; Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Add-VoiceLite-Exclusion.ps1"""; Comment: "Add VoiceLite to Windows Defender exclusions"

[Run]
; Install VC++ Runtime BEFORE running VoiceLite (only if not already installed)
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft Visual C++ Runtime..."; Check: not IsVCRuntimeInstalled; Flags: waituntilterminated
; Launch VoiceLite after installation
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

  // Check if VC++ Runtime is already installed
  if not IsVCRuntimeInstalled then
  begin
    MsgBox('VoiceLite requires Microsoft Visual C++ Runtime 2015-2022.' + #13#10#13#10 +
           'This required component will be installed automatically during setup.' + #13#10 +
           'Note: A system restart may be required after installation.',
           mbInformation, MB_OK);
  end;
end;

// VC++ Runtime installation is now handled by [Run] section (lines 57-58)
// This ensures vc_redist.x64.exe exists in {tmp} before installation attempts
// Old InstallVCRuntimeIfNeeded() function removed in v1.0.62 to eliminate dead code

function IsRestartPending: Boolean;
var
  PendingFileRenameOps: String;
begin
  Result := False;

  // Check for pending file rename operations (common after VC++ install)
  if RegQueryMultiStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager',
                                'PendingFileRenameOperations', PendingFileRenameOps) then
  begin
    Result := (Length(PendingFileRenameOps) > 0);
    if Result then
      Log('Restart pending: PendingFileRenameOperations detected');
  end;
end;

function GetFileSize(FileName: String): Int64;
var
  FindRec: TFindRec;
begin
  Result := 0;
  if FindFirst(FileName, FindRec) then
  begin
    try
      Result := (FindRec.SizeHigh shl 32) or FindRec.SizeLow;
    finally
      FindClose(FindRec);
    end;
  end;
end;

function VerifyModelFiles: Boolean;
var
  AppPath: String;
  TinyModelPath: String;
  TinyModelSize: Int64;
begin
  Result := True;
  AppPath := ExpandConstant('{app}');

  // Lite installer only includes Tiny model
  TinyModelPath := AppPath + '\whisper\ggml-tiny.bin';
  if FileExists(TinyModelPath) then
  begin
    TinyModelSize := GetFileSize(TinyModelPath);
    // Tiny model should be ~74-76 MB (75MB = 78643200 bytes)
    if (TinyModelSize < 60000000) or (TinyModelSize > 90000000) then
    begin
      Log('WARNING: Tiny model file size is suspicious: ' + IntToStr(TinyModelSize) + ' bytes (expected ~75MB)');
      Result := False;
    end
    else
    begin
      Log('Tiny model verified: ' + IntToStr(TinyModelSize) + ' bytes');
    end;
  end
  else
  begin
    Log('WARNING: Tiny model not found - required for Lite installer');
    Result := False;
  end;
end;

function RunWhisperSmokeTest: Boolean;
var
  ResultCode: Integer;
  WhisperExePath: String;
begin
  Result := False;

  try
    WhisperExePath := ExpandConstant('{app}\whisper\whisper.exe');

    if not FileExists(WhisperExePath) then
    begin
      Log('ERROR: whisper.exe not found at: ' + WhisperExePath);
      Exit;
    end;

    Log('Running whisper.exe smoke test...');

    // Run whisper.exe --help (should exit with code 0 or 1)
    if Exec(WhisperExePath, '--help', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Log('Whisper smoke test exit code: ' + IntToStr(ResultCode));

      // Exit codes 0 or 1 are both valid for --help (depends on version)
      if (ResultCode = 0) or (ResultCode = 1) then
      begin
        Log('Whisper smoke test PASSED');
        Result := True;
      end
      else
      begin
        Log('Whisper smoke test FAILED: unexpected exit code ' + IntToStr(ResultCode));
      end;
    end
    else
    begin
      Log('Whisper smoke test FAILED: could not execute whisper.exe');
    end;
  except
    Log('Whisper smoke test FAILED: exception during execution');
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AppDataDir: String;
  LogsDir: String;
begin
  // [Run] section now handles VC++ Runtime installation automatically


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

    // Verify model files were installed correctly
    if not VerifyModelFiles then
    begin
      MsgBox('Warning: AI model files may be corrupted or incomplete.' + #13#10#13#10 +
             'If VoiceLite fails to transcribe audio, please:' + #13#10 +
             '1. Uninstall VoiceLite' + #13#10 +
             '2. Re-download the installer from GitHub' + #13#10 +
             '3. Verify SHA256 hash matches before installing',
             mbInformation, MB_OK);
    end;

    // Final verification that VC++ Runtime is now installed
    if not IsVCRuntimeInstalled then
    begin
      MsgBox('CRITICAL: Visual C++ Runtime installation failed or incomplete.' + #13#10#13#10 +
             'VoiceLite CANNOT run without this component!' + #13#10#13#10 +
             'REQUIRED STEPS:' + #13#10 +
             '1. RESTART your computer NOW' + #13#10 +
             '2. If still failing, download VC++ Runtime manually:' + #13#10 +
             '   https://aka.ms/vs/17/release/vc_redist.x64.exe' + #13#10 +
             '3. Install it and restart again',
             mbCriticalError, MB_OK);
    end
    else
    begin
      // Check if system restart is pending after VC++ installation
      if IsRestartPending then
      begin
        MsgBox('IMPORTANT: A system restart is required.' + #13#10#13#10 +
               'Visual C++ Runtime was installed successfully, but Windows needs to restart.' + #13#10#13#10 +
               'Please RESTART your computer before launching VoiceLite.' + #13#10#13#10 +
               'If you launch VoiceLite without restarting, it may fail with "missing DLL" errors.',
               mbInformation, MB_OK);
      end;

      // VC++ verified - now run whisper.exe smoke test
      if not RunWhisperSmokeTest then
      begin
        MsgBox('CRITICAL: Whisper AI engine failed verification test.' + #13#10#13#10 +
               'This may be caused by:' + #13#10 +
               '• Missing DLL files (corrupted download)' + #13#10 +
               '• Antivirus blocking whisper.exe' + #13#10 +
               '• Incompatible system configuration' + #13#10#13#10 +
               'SOLUTIONS:' + #13#10 +
               '1. Run the desktop shortcut "Fix Antivirus Issues"' + #13#10 +
               '2. Restart your computer' + #13#10 +
               '3. Re-download installer and verify SHA256 hash',
               mbCriticalError, MB_OK);
      end;
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
