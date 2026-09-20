; Simple Inno Setup Script for VoiceLite
; v2.0.0.0: Parakeet v3 engine (Sherpa-ONNX). Speech model downloaded on first launch (~640MB).
; No manual downloads required - .NET bundled (self-contained), VC++ auto-installed

#define MyAppVersion "2.5.1"

[Setup]
AppId={{A06BC0AA-DD0A-4341-9E41-68AC0D6E541E}
AppName=VoiceLite
AppVersion={#MyAppVersion}
AppPublisher=VoiceLite
AppPublisherURL=https://voicelite.app
AppSupportURL=https://voicelite.app
AppUpdatesURL=https://voicelite.app
DefaultDirName={autopf}\VoiceLite
DisableProgramGroupPage=yes
OutputDir=..\..\
OutputBaseFilename=VoiceLite-Setup-{#MyAppVersion}
SetupIconFile=..\VoiceLite\VoiceLite.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\..\EULA.txt
; Always write "Setup Log YYYY-MM-DD #NNN.txt" to %TEMP%. The in-app updater runs
; Setup /VERYSILENT without /LOG, so without this a failed update leaves no evidence.
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main application files
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\VoiceLite.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\*.json"; DestDir: "{app}"; Flags: ignoreversion

; Silero VAD ONNX model (~2.3MB, used for silence trimming before transcription).
; Speech model (Parakeet v3, ~640MB) is downloaded on first launch — not bundled in installer.
; Sherpa-ONNX + ONNX Runtime native DLLs are included in the *.dll wildcard above.
; Explicit single-file copy (not a glob) prevents stale GGML files from leaking into
; the installer if a developer accidentally drops them in publish/whisper/.
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\silero_vad_v5.onnx"; DestDir: "{app}\whisper"; Flags: ignoreversion

; Icon file
Source: "..\VoiceLite\VoiceLite.ico"; DestDir: "{app}"; Flags: ignoreversion

; Third-party license texts (NVIDIA Parakeet CC-BY-4.0 attribution is mandatory)
Source: "..\LICENSES\*"; DestDir: "{app}\LICENSES"; Flags: ignoreversion recursesubdirs

; VC++ Redistributable (bundled for auto-install)
Source: "vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\VoiceLite"; Filename: "{app}\VoiceLite.exe"
Name: "{autodesktop}\VoiceLite"; Filename: "{app}\VoiceLite.exe"; Tasks: desktopicon

[Run]
; Install VC++ Redistributable silently (no restart needed).
; KNOWN LIMITATION: under the default per-user install (PrivilegesRequired=lowest),
; vc_redist.x64.exe requires elevation it doesn't have and fails silently (it is a
; machine-wide MSI installer). `skipifdoesntexist` + the absence of an exit-code
; check make this explicitly non-fatal by design: the app itself probes for the
; VC++ runtime at startup (App.xaml.cs Sherpa-ONNX DLL probe) and shows the user a
; download link if it's missing — that probe is the real backstop. On elevated
; (admin/all-users) installs this line works as intended. Exit code 1638 ("newer
; version already installed") is also a success case, another reason not to hard-fail.
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/quiet /norestart"; StatusMsg: "Installing Visual C++ Runtime..."; Flags: waituntilterminated skipifdoesntexist

; Launch VoiceLite after installation
Filename: "{app}\VoiceLite.exe"; Description: "{cm:LaunchProgram,VoiceLite}"; Flags: nowait postinstall skipifsilent

; In-app updater path: VoiceLite runs this installer with
;   /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /RELAUNCH=1
; (see Services/UpdateInstaller.cs). The postinstall entry above is skipifsilent, so
; without this line a silent upgrade would leave the user with no VoiceLite running.
; runasoriginaluser: if Setup self-elevated (all-users install), still start the app
; as the logged-in user so its %LOCALAPPDATA% settings/license resolve correctly.
Filename: "{app}\VoiceLite.exe"; Flags: nowait skipifnotsilent runasoriginaluser; Check: RelaunchRequested

[UninstallDelete]
; Only delete the install dir's temp folder. User data in {localappdata}\VoiceLite
; (settings, license.dat, downloaded Parakeet model, transcription history) is
; preserved by default and only wiped when /PURGEDATA is passed — see the
; PurgeDataRequested / CurUninstallStepChanged logic below. Listing {localappdata}\VoiceLite
; here would override that policy because [UninstallDelete] entries are processed during
; the file-deletion phase (before usPostUninstall), making the PurgeDataRequested check moot.
Type: filesandordirs; Name: "{app}\temp"

[Code]
// 'VoiceLite_SingleInstance' is the EXACT name of the mutex App.xaml.cs creates
// (session-local, held for the process lifetime). Keep the two in sync — the
// app-side comment points back here.
const
  AppMutexName = 'VoiceLite_SingleInstance';
  AppExeName = 'VoiceLite.exe';

// Outcome of THIS Setup process, read by DeinitializeSetup. When Setup re-launches
// itself elevated (all-users install), the non-elevated parent exits before [Code] is
// loaded, so only the process that actually installs ever evaluates these.
var
  InstallSucceeded: Boolean;  // ssPostInstall reached
  FilesTouched: Boolean;      // ssInstall reached - files in {app} may be partly replaced

// delayload: bind on first call, so a failed import raises a catchable exception
// there instead of stopping Setup (or the uninstaller) at start-up.
function GetCurrentProcessId: DWORD;
  external 'GetCurrentProcessId@kernel32.dll stdcall delayload';
function ProcessIdToSessionId(dwProcessId: DWORD; var pSessionId: DWORD): BOOL;
  external 'ProcessIdToSessionId@kernel32.dll stdcall delayload';

// Set by the in-app updater (/RELAUNCH=1). Read via {param:...} so it works with
// both "/RELAUNCH=1" and "/RELAUNCH 1" forms. Updater-only flag: on a failed silent
// install it also turns on the DeinitializeSetup message box, so IT pushes must not pass it.
function RelaunchRequested(): Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;

// Updater handshake (VoiceLite >= 2.5.2 passes /READYFILE=<path>): creating that file
// tells the app Setup got past UAC/initialisation, so it is safe to shut down. No
// parameter (2.5.0/2.5.1 updater, manual installs, IT pushes) => does nothing.
procedure WriteReadyFile();
var
  ReadyFile: String;
begin
  try
    ReadyFile := ExpandConstant('{param:READYFILE|}');
    if ReadyFile <> '' then
    begin
      if SaveStringToFile(ReadyFile, 'ready', False) then
        Log('Ready file written: ' + ReadyFile)
      else
        Log('Could not write ready file: ' + ReadyFile);
    end;
  except
    Log('Ready file not written: ' + GetExceptionMessage);
  end;
end;

// Process-based running check. CheckForMutexes only sees builds >= 2.2 (the ones
// that create the mutex); v2.1.x installs in the wild have no mutex, so ask tasklist.
// tasklist prints a header line + one line per match, or "INFO: No tasks are
// running..." when nothing matches — the image name never appears in that INFO line.
function IsVoiceLiteProcessRunning(): Boolean;
var
  ResultCode: Integer;
  TmpFile: String;
  Lines: TArrayOfString;
  i: Integer;
begin
  Result := False;
  TmpFile := ExpandConstant('{tmp}\voicelite_tasklist.txt');
  if Exec(ExpandConstant('{cmd}'),
          '/C tasklist /FI "IMAGENAME eq ' + AppExeName + '" /NH > "' + TmpFile + '"',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if LoadStringsFromFile(TmpFile, Lines) then
    begin
      for i := 0 to GetArrayLength(Lines) - 1 do
      begin
        if Pos(Lowercase(AppExeName), Lowercase(Lines[i])) > 0 then
        begin
          Result := True;
          Break;
        end;
      end;
    end;
    DeleteFile(TmpFile);
  end
  else
    Log('tasklist could not be run (code ' + IntToStr(ResultCode) + '); relying on mutex check only');
end;

function IsVoiceLiteRunning(): Boolean;
begin
  Result := IsVoiceLiteProcessRunning() or CheckForMutexes(AppMutexName);
end;

// Waits up to MaxMs for VoiceLite to exit on its own. Returns True when it is gone.
function WaitForVoiceLiteExit(MaxMs: Integer): Boolean;
var
  Waited: Integer;
begin
  Waited := 0;
  while IsVoiceLiteRunning() and (Waited < MaxMs) do
  begin
    Sleep(500);
    Waited := Waited + 500;
  end;
  Result := not IsVoiceLiteRunning();
end;

// Session-scoped variant of IsVoiceLiteRunning, used ONLY to decide whether a
// VoiceLite.exe that survived taskkill really blocks a per-user install. Fails closed:
// returns True ("still running") unless tasklist demonstrably ran with a valid
// session filter (exit code 0, output readable) and listed no VoiceLite.exe.
function IsVoiceLiteRunningInThisSession(): Boolean;
var
  SessionId: DWORD;
  ResultCode: Integer;
  TmpFile: String;
  Lines: TArrayOfString;
  i: Integer;
begin
  Result := True;
  try
    // The app mutex is session-local, so a hit here is an instance in OUR session.
    if CheckForMutexes(AppMutexName) then
      Exit;
    if not ProcessIdToSessionId(GetCurrentProcessId(), SessionId) then
    begin
      Log('ProcessIdToSessionId failed; keeping the machine-wide running check');
      Exit;
    end;
    TmpFile := ExpandConstant('{tmp}\voicelite_tasklist_session.txt');
    if not Exec(ExpandConstant('{cmd}'),
                '/C tasklist /FI "IMAGENAME eq ' + AppExeName + '" /FI "SESSION eq ' +
                IntToStr(SessionId) + '" /NH > "' + TmpFile + '"',
                '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      Exit;
    if ResultCode <> 0 then
      Exit;
    if not LoadStringsFromFile(TmpFile, Lines) then
      Exit;
    DeleteFile(TmpFile);
    for i := 0 to GetArrayLength(Lines) - 1 do
    begin
      if Pos(Lowercase(AppExeName), Lowercase(Lines[i])) > 0 then
        Exit;
    end;
    Result := False;
  except
    Log('Session-scoped running check failed: ' + GetExceptionMessage);
    Result := True;
  end;
end;

// Force-terminates VoiceLite.exe and polls up to ~10s until it is gone.
function CloseRunningVoiceLite(): Boolean;
var
  ResultCode: Integer;
begin
  Log('VoiceLite is running - terminating ' + AppExeName);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM ' + AppExeName + ' /F', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Log('taskkill exit code ' + IntToStr(ResultCode));
  Result := WaitForVoiceLiteExit(10000);
  if Result then
    Log(AppExeName + ' has exited')
  else
  begin
    Log(AppExeName + ' is still running after taskkill');
    // tasklist/taskkill see every session, but a non-elevated taskkill cannot end
    // another user's VoiceLite (Fast User Switching, RDP) - and that process does not
    // lock this user's per-user {app}. Don't abort the install over it. Admin
    // (all-users) installs stay machine-wide: every session runs from the same {app}.
    // An instance of the SAME user in another session was killable above, so it is gone.
    if (not IsAdminInstallMode) and (not IsVoiceLiteRunningInThisSession()) then
    begin
      Log('Remaining ' + AppExeName + ' belongs to another session - continuing');
      Result := True;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  // Must stay first: the updater waits for this file before it shuts the app down.
  WriteReadyFile();
  if not IsVoiceLiteRunning() then
    Exit;

  if WizardSilent then
  begin
    // Silent = in-app updater (or an IT push). The updater shuts VoiceLite down
    // right after launching us; give it time to exit cleanly (settings flush, hotkey
    // and recognizer disposal can add up to ~10s worst case) before falling back
    // to taskkill.
    if WaitForVoiceLiteExit(10000) then
      Exit;
  end
  else
  begin
    if MsgBox('VoiceLite is running.'#13#10#13#10 +
              'Close it and continue with the installation?',
              mbConfirmation, MB_YESNO) <> IDYES then
    begin
      Result := 'VoiceLite is currently running. Please close it (check the system tray) before continuing.';
      Exit;
    end;
  end;

  if not CloseRunningVoiceLite() then
    Result := 'VoiceLite is still running and could not be closed. ' +
              'Please exit it from the system tray and run Setup again.';
end;

function InitializeUninstall(): Boolean;
begin
  // Same running-check on uninstall: removing files while VoiceLite holds them
  // locked leaves a partially-deleted install (exe gone, DLLs behind, or vice
  // versa). Abort cleanly instead. Process check covers pre-mutex (v2.1.x) builds.
  Result := True;
  if IsVoiceLiteRunning() then
  begin
    MsgBox('VoiceLite is currently running.'#13#10#13#10 +
           'Please close it (check the system tray), then run the uninstaller again.',
           mbError, MB_OK);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AppDataDir: String;
  LogsDir: String;
begin
  if CurStep = ssInstall then
    FilesTouched := True;

  if CurStep = ssPostInstall then
  begin
    InstallSucceeded := True;

    // Create AppData directories for settings and logs
    AppDataDir := ExpandConstant('{localappdata}\VoiceLite');
    LogsDir := AppDataDir + '\logs';

    try
      if not DirExists(AppDataDir) then
        CreateDir(AppDataDir);

      if not DirExists(LogsDir) then
        CreateDir(LogsDir);
    except
      // Silently fail - the app will create directories on first run if needed
    end;
  end;
end;

// In-app update that did not complete. The app already exited when it launched us
// (2.5.0/2.5.1 shut down unconditionally), and Setup is /VERYSILENT /SUPPRESSMSGBOXES,
// so without this the user's VoiceLite just vanishes with no explanation.
// MsgBox (unlike SuppressibleMsgBox) is still shown under /SUPPRESSMSGBOXES.
procedure ReportFailedUpdate();
var
  ExePath: String;
  ResultCode: Integer;
  Relaunched: Boolean;
begin
  Relaunched := False;
  if not FilesTouched then
  begin
    Log('Update failed before any file was replaced - the existing install is intact');
    // The old install is intact, so put the user's app back. ExecAsOriginalUser only:
    // an elevated Setup must never start VoiceLite elevated (breaks text injection and
    // resolves the wrong %LOCALAPPDATA%). Skip it if an instance is still running here.
    try
      if not CheckForMutexes(AppMutexName) then
      begin
        ExePath := AddBackslash(WizardDirValue) + AppExeName;
        if FileExists(ExePath) then
          Relaunched := ExecAsOriginalUser(ExePath, '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
      end;
    except
      Log('Could not restart the existing VoiceLite: ' + GetExceptionMessage);
    end;

    if Relaunched then
      MsgBox('VoiceLite could not install the update, so your current version has been restarted. Nothing was changed.'#13#10#13#10 +
             'You can try the update again later, or download the installer from https://voicelite.app.',
             mbInformation, MB_OK)
    else
      MsgBox('VoiceLite could not install the update. Your current version was not changed.'#13#10#13#10 +
             'If VoiceLite is not running, start it from the Start menu. You can try the update again later, ' +
             'or download the installer from https://voicelite.app.',
             mbError, MB_OK);
  end
  else
  begin
    // Setup stopped after it began replacing files. Inno's rollback does not restore
    // files it already overwrote, so the install may be a mix of two versions - do NOT
    // auto-start it and do NOT claim it was restarted.
    Log('Update failed after file replacement began - not restarting VoiceLite');
    if MsgBox('The VoiceLite update did not finish, and VoiceLite may not start until it is reinstalled.'#13#10#13#10 +
              'Please download the installer from https://voicelite.app and run it. ' +
              'Your settings, license and speech model are not affected.'#13#10#13#10 +
              'Open the download page now?',
              mbError, MB_YESNO) = IDYES then
      ShellExecAsOriginalUser('open', 'https://voicelite.app', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
  end;
end;

// Called just before Setup terminates, success or not. Must never raise.
// Gated on /RELAUNCH=1 + silent, i.e. the in-app updater; a plain /VERYSILENT IT push
// that fails stays headless. Not reached for a declined UAC prompt on an all-users
// install: the non-elevated Setup exits before [Code] is loaded (known gap).
procedure DeinitializeSetup();
begin
  try
    if RelaunchRequested() and WizardSilent and (not InstallSucceeded) then
      ReportFailedUpdate();
  except
    Log('DeinitializeSetup: ' + GetExceptionMessage);
  end;
end;

function PurgeDataRequested: Boolean;
var
  i: Integer;
begin
  // Explicit opt-in to wipe user data: pass /PURGEDATA on the uninstaller command line.
  // Without this flag, silent uninstall preserves %LocalAppData%\VoiceLite so that:
  //   - Inno's auto-uninstall during in-place upgrades doesn't destroy settings/license/model
  //   - Intune redeployments preserve the user's 640 MB Parakeet model + Pro license
  // IT admins decommissioning a workstation should pass /PURGEDATA explicitly.
  Result := False;
  for i := 1 to ParamCount do
  begin
    if CompareText(ParamStr(i), '/PURGEDATA') = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if PurgeDataRequested then
    begin
      // Explicit /PURGEDATA flag: full decommission, wipe everything.
      DelTree(ExpandConstant('{localappdata}\VoiceLite'), True, True, True);
    end
    else if not UninstallSilent then
    begin
      // Interactive uninstall: warn clearly about what's being deleted, default to NO.
      // The 640 MB Parakeet speech model and DPAPI-encrypted Pro license live in this dir
      // — losing them is a serious user-visible cost (re-download + re-activate).
      if MsgBox('Also remove your VoiceLite settings, Custom Dictionary entries,'#13#10 +
                'transcription history, Pro license, and the 640 MB speech model?'#13#10#13#10 +
                'Choose NO to keep these (recommended — saves a long re-download if you reinstall).'#13#10 +
                'Choose YES only if you''re sure you won''t reinstall.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(ExpandConstant('{localappdata}\VoiceLite'), True, True, True);
      end;
    end;
    // Default (silent uninstall without /PURGEDATA): preserve all user data.
    // This is the path hit by Inno's upgrade auto-uninstall and by routine Intune redeployments.
  end;
end;
