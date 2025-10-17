; Simple Inno Setup Script for VoiceLite
; v1.0.68: Free vs Pro license system + performance migration
; Windows 10/11 (64-bit) compatible - detects VC++ Runtime if needed

[Setup]
AppId={{A06BC0AA-DD0A-4341-9E41-68AC0D6E541E}
AppName=VoiceLite
AppVersion=1.0.68
AppPublisher=VoiceLite
AppPublisherURL=https://voicelite.app
AppSupportURL=https://voicelite.app
AppUpdatesURL=https://voicelite.app
DefaultDirName={autopf}\VoiceLite
DisableProgramGroupPage=yes
OutputDir=..\..\
OutputBaseFilename=VoiceLite-Setup-1.0.68
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
Name: "antivirusfix"; Description: "Create 'Fix Antivirus Issues' desktop shortcut"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application files
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\VoiceLite.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\*.json"; DestDir: "{app}"; Flags: ignoreversion

; Whisper files (Tiny model ONLY - free tier default)
; Freemium model: Tiny (75MB, free) ships with installer, Pro (466MB, $20) available via download
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\ggml-tiny.bin"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\whisper.exe"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\whisper.dll"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\*.dll"; DestDir: "{app}\whisper"; Flags: ignoreversion

; Icon file
Source: "..\VoiceLite\VoiceLite.ico"; DestDir: "{app}"; Flags: ignoreversion

; VC++ Redistributable installer (bundled for offline installation)
Source: "..\dependencies\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

; Antivirus exclusion PowerShell script
Source: "..\Installer\Add-VoiceLite-Exclusion.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\VoiceLite"; Filename: "{app}\VoiceLite.exe"
Name: "{autodesktop}\VoiceLite"; Filename: "{app}\VoiceLite.exe"; Tasks: desktopicon
Name: "{autodesktop}\Fix Antivirus Issues"; Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Add-VoiceLite-Exclusion.ps1"""; Comment: "Add VoiceLite to Windows Defender exclusions"; Tasks: antivirusfix

[Run]
; Install VC++ Runtime BEFORE running VoiceLite (only if not already installed)
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft Visual C++ Runtime..."; Check: not IsVCRuntimeInstalled; Flags: waituntilterminated
; Launch VoiceLite after installation
Filename: "{app}\VoiceLite.exe"; Description: "{cm:LaunchProgram,VoiceLite}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{localappdata}\VoiceLite"

[Code]
const
  // File size constants (in bytes)
  MB = 1048576; // 1 MB in bytes

  // Model file size thresholds (in MB)
  SMALL_MODEL_MIN_MB = 400;
  SMALL_MODEL_MAX_MB = 550;
  SMALL_MODEL_EXPECTED_MB = 466;

  TINY_MODEL_MIN_MB = 60;
  TINY_MODEL_MAX_MB = 90;
  TINY_MODEL_EXPECTED_MB = 75;

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

// VC++ Runtime installation is now handled by [Run] section (lines 62-63)
// This ensures vc_redist.x64.exe exists in {tmp} before installation attempts
// Old InstallVCRuntimeIfNeeded() function removed in v1.0.62 to eliminate dead code

function IsRestartPending: Boolean;
var
  PendingFileRenameOps: String;
  UpdateExeVolatile: Cardinal;
begin
  Result := False;

  // Check 1: Pending file rename operations (VC++ Runtime common cause)
  if RegQueryMultiStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager',
                                'PendingFileRenameOperations', PendingFileRenameOps) then
  begin
    if Length(PendingFileRenameOps) > 0 then
    begin
      Log('Restart pending: PendingFileRenameOperations detected');
      Result := True;
      Exit;
    end;
  end;

  // Check 2: Component Based Servicing (Windows Updates)
  if RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') then
  begin
    Log('Restart pending: CBS RebootPending key exists');
    Result := True;
    Exit;
  end;

  // Check 3: Windows Update Auto Update
  if RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired') then
  begin
    Log('Restart pending: WindowsUpdate RebootRequired key exists');
    Result := True;
    Exit;
  end;

  // Check 4: Session Manager PendingFileRenameOperations2 (Windows 10+)
  if RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations2') then
  begin
    Log('Restart pending: PendingFileRenameOperations2 key exists');
    Result := True;
    Exit;
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
  SmallModelPath: String;
  TinyModelPath: String;
  SmallModelSize: Int64;
  TinyModelSize: Int64;
begin
  Result := True;
  AppPath := ExpandConstant('{app}');

  // Check for Tiny model (Free tier - 75MB)
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
    Log('WARNING: Tiny model not found - at least one model is required');
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

function InstallVCRuntimeWithRetry: Boolean;
var
  ResultCode: Integer;
  Attempt: Integer;
  MaxAttempts: Integer;
begin
  MaxAttempts := 3;
  Result := False;

  for Attempt := 1 to MaxAttempts do
  begin
    Log('VC++ Runtime installation attempt ' + IntToStr(Attempt) + '/' + IntToStr(MaxAttempts));

    if Exec(ExpandConstant('{tmp}\vc_redist.x64.exe'), '/install /quiet /norestart',
            '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Log('VC++ Runtime installer exit code: ' + IntToStr(ResultCode));

      if ResultCode = 0 then
      begin
        // Success
        Log('VC++ Runtime installed successfully');
        Result := True;
        Break;
      end
      else if ResultCode = 1638 then
      begin
        // Already installed or newer version present
        Log('VC++ Runtime already installed (exit code 1638)');
        Result := True;
        Break;
      end
      else if ResultCode = 3010 then
      begin
        // Success but restart required
        Log('VC++ Runtime installed successfully (restart required, exit code 3010)');
        Result := True;
        Break;
      end
      else
      begin
        Log('VC++ Runtime installation failed with exit code: ' + IntToStr(ResultCode));
        if Attempt < MaxAttempts then
        begin
          Log('Waiting 2 seconds before retry...');
          Sleep(2000); // Wait 2 seconds before retry
        end;
      end;
    end
    else
    begin
      Log('VC++ Runtime installer failed to execute');
      if Attempt < MaxAttempts then
      begin
        Sleep(2000);
      end;
    end;
  end;

  if not Result then
  begin
    Log('VC++ Runtime installation failed after ' + IntToStr(MaxAttempts) + ' attempts');
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
        if MsgBox('CRITICAL: Whisper AI engine failed verification test.' + #13#10#13#10 +
                   'VoiceLite cannot function without this component!' + #13#10#13#10 +
                   'This may be caused by:' + #13#10 +
                   '• Missing DLL files (corrupted download)' + #13#10 +
                   '• Antivirus blocking whisper.exe' + #13#10 +
                   '• Incompatible system configuration' + #13#10#13#10 +
                   'Do you want to ABORT the installation and try again?' + #13#10 +
                   '(Click YES to abort, NO to continue anyway)',
                   mbCriticalError, MB_YESNO) = IDYES then
        begin
          Log('User chose to abort installation due to Whisper smoke test failure');
          // Abort triggers automatic rollback
          Abort;
        end
        else
        begin
          Log('User chose to continue despite Whisper smoke test failure');
        end;
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
