; Simple Inno Setup Script for VoiceLite
; v2.0.0.0: Parakeet v3 engine (Sherpa-ONNX). Speech model downloaded on first launch (~640MB).
; No manual downloads required - .NET bundled (self-contained), VC++ auto-installed

#define MyAppVersion "2.3.0"

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

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  // Check if VoiceLite is currently running
  if CheckForMutexes(AppMutexName) then
  begin
    Result := 'VoiceLite is currently running. Please close it before continuing.';
  end
  else
  begin
    Result := '';
  end;
end;

function InitializeUninstall(): Boolean;
begin
  // Same running-check on uninstall: removing files while VoiceLite holds them
  // locked leaves a partially-deleted install (exe gone, DLLs behind, or vice
  // versa). Abort cleanly instead.
  Result := True;
  if CheckForMutexes(AppMutexName) then
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
  if CurStep = ssPostInstall then
  begin
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
