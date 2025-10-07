; Simple Inno Setup Script for VoiceLite
; v1.0.60: Bundled VC++ Runtime - true offline installation
; Windows 10/11 (64-bit) compatible - detects VC++ Runtime if needed

[Setup]
AppId={{A06BC0AA-DD0A-4341-9E41-68AC0D6E541E}
AppName=VoiceLite
AppVersion=1.0.60
AppPublisher=VoiceLite
AppPublisherURL=https://voicelite.app
AppSupportURL=https://voicelite.app
AppUpdatesURL=https://voicelite.app
DefaultDirName={autopf}\VoiceLite
DisableProgramGroupPage=yes
OutputDir=..\..\
OutputBaseFilename=VoiceLite-Setup-1.0.60
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

; Whisper files (Small + Tiny models ONLY - Small is new free tier default, Tiny is legacy fallback)
; CRITICAL FIX: Only include Small + Tiny models to keep installer size ~540MB (not 2.6GB with all models)
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\ggml-small.bin"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\ggml-tiny.bin"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\whisper.exe"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\whisper.dll"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\server.exe"; DestDir: "{app}\whisper"; Flags: ignoreversion
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
Name: "{autodesktop}\Fix Antivirus Issues"; Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Add-VoiceLite-Exclusion.ps1"""; Comment: "Add VoiceLite to Windows Defender exclusions"

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

  // Check if VC++ Runtime is already installed
  if not IsVCRuntimeInstalled then
  begin
    MsgBox('VoiceLite requires Microsoft Visual C++ Runtime 2015-2022.' + #13#10#13#10 +
           'The installer will now install this required component.' + #13#10 +
           'Note: A system restart may be required after installation.',
           mbInformation, MB_OK);
  end;
end;

procedure InstallVCRuntimeIfNeeded;
var
  ResultCode: Integer;
  VCInstaller: String;
  RetryCount: Integer;
  Success: Boolean;
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
    MsgBox('CRITICAL ERROR: Visual C++ Runtime installer is missing from setup package.' + #13#10#13#10 +
           'VoiceLite cannot run without this component.' + #13#10#13#10 +
           'Please download the complete installer from:' + #13#10 +
           'https://github.com/mikha08-rgb/VoiceLite/releases',
           mbCriticalError, MB_OK);
    // Block installation
    WizardForm.Close;
    Exit;
  end;

  // Retry up to 2 times
  RetryCount := 0;
  Success := False;

  while (RetryCount < 2) and (not Success) do
  begin
    // Install silently without restart
    if Exec(VCInstaller, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if (ResultCode = 0) or (ResultCode = 3010) then
      begin
        Log('VC++ Runtime installed successfully (exit code: ' + IntToStr(ResultCode) + ')');
        Success := True;

        if ResultCode = 3010 then
        begin
          MsgBox('Microsoft Visual C++ Runtime installed successfully.' + #13#10#13#10 +
                 'IMPORTANT: A system restart is REQUIRED for VoiceLite to work properly.' + #13#10#13#10 +
                 'Please restart your computer after installation completes.',
                 mbInformation, MB_OK);
        end;
      end
      else if ResultCode = 1638 then
      begin
        Log('VC++ Runtime already installed (detected by installer)');
        Success := True;
      end
      else
      begin
        Inc(RetryCount);
        if RetryCount < 2 then
        begin
          if MsgBox('Visual C++ Runtime installation failed (error code: ' + IntToStr(ResultCode) + ')' + #13#10#13#10 +
                    'Would you like to retry?',
                    mbError, MB_YESNO) = IDNO then
          begin
            Break;
          end;
        end;
      end;
    end
    else
    begin
      Inc(RetryCount);
      if RetryCount < 2 then
      begin
        if MsgBox('Failed to run Visual C++ Runtime installer.' + #13#10#13#10 +
                  'Would you like to retry?',
                  mbError, MB_YESNO) = IDNO then
        begin
          Break;
        end;
      end;
    end;
  end;

  // If installation failed after retries, block VoiceLite installation
  if not Success and not IsVCRuntimeInstalled then
  begin
    MsgBox('INSTALLATION BLOCKED: Visual C++ Runtime installation failed.' + #13#10#13#10 +
           'VoiceLite cannot function without this component.' + #13#10#13#10 +
           'Please install it manually from:' + #13#10 +
           'https://aka.ms/vs/17/release/vc_redist.x64.exe' + #13#10#13#10 +
           'Then restart your computer and run this installer again.',
           mbCriticalError, MB_OK);
    WizardForm.Close;
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

  // Check for Small model (Pro - 466MB)
  SmallModelPath := AppPath + '\whisper\ggml-small.bin';
  if FileExists(SmallModelPath) then
  begin
    SmallModelSize := GetFileSize(SmallModelPath);
    // Small model should be ~460-470 MB (460MB = 482344960 bytes)
    if (SmallModelSize < 400000000) or (SmallModelSize > 550000000) then
    begin
      Log('WARNING: Small model file size is suspicious: ' + IntToStr(SmallModelSize) + ' bytes (expected ~466MB)');
      Result := False;
    end
    else
    begin
      Log('Small model verified: ' + IntToStr(SmallModelSize) + ' bytes');
    end;
  end
  else
  begin
    Log('Small model not found (this is OK if Lite installer)');
  end;

  // Check for Tiny model (Lite - 75MB)
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