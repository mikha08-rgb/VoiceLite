; Simple Inno Setup Script for VoiceLite
; v2.0.0.0: Parakeet v3 engine (Sherpa-ONNX). Speech model downloaded on first launch (~640MB).
; No manual downloads required - .NET bundled (self-contained), VC++ auto-installed

#define MyAppVersion "2.0.0"

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

; Silero VAD ONNX model (~2.3MB, used for silence trimming before transcription)
; Speech model (Parakeet v3, ~640MB) is downloaded on first launch — not bundled in installer
; Sherpa-ONNX + ONNX Runtime native DLLs are included in the *.dll wildcard above
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\*"; DestDir: "{app}\whisper"; Flags: ignoreversion recursesubdirs

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
; Install VC++ Redistributable silently (no restart needed)
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/quiet /norestart"; StatusMsg: "Installing Visual C++ Runtime..."; Flags: waituntilterminated

; Launch VoiceLite after installation
Filename: "{app}\VoiceLite.exe"; Description: "{cm:LaunchProgram,VoiceLite}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{localappdata}\VoiceLite"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  // Check if VoiceLite is currently running
  if CheckForMutexes('VoiceLite_SingleInstance') then
  begin
    Result := 'VoiceLite is currently running. Please close it before continuing.';
  end
  else
  begin
    Result := '';
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

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Silent uninstall (Intune/managed deployment) — always remove user data.
    // Healthcare deployments don't want transcription history or DPAPI license files
    // lingering on uninstalled workstations.
    if UninstallSilent then
    begin
      DelTree(ExpandConstant('{localappdata}\VoiceLite'), True, True, True);
    end
    else if MsgBox('Do you want to remove VoiceLite settings and transcription history?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\VoiceLite'), True, True, True);
    end;
  end;
end;
