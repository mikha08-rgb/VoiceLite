; Simple Inno Setup Script for VoiceLite
; v1.0.74: Critical fix - isTranscribing stuck state bug
; Just copy files, inform user of dependencies, and launch

[Setup]
AppId={{A06BC0AA-DD0A-4341-9E41-68AC0D6E541E}
AppName=VoiceLite
AppVersion=1.2.0.1
AppPublisher=VoiceLite
AppPublisherURL=https://voicelite.app
AppSupportURL=https://voicelite.app
AppUpdatesURL=https://voicelite.app
DefaultDirName={autopf}\VoiceLite
DisableProgramGroupPage=yes
OutputDir=..\..\
OutputBaseFilename=VoiceLite-Setup-1.2.0.1
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

; Whisper files (Base model bundled - 78MB Q8_0 quantized)
; CRITICAL FIX v1.2.0.1: ggml-base.bin is the new default bundled model (was tiny)
; Copy from publish directory (consistent with .exe/.dll copying above)
; Tiny model is downloadable via AI Models tab for users with slow PCs
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\whisper\*"; DestDir: "{app}\whisper"; Flags: ignoreversion recursesubdirs

; Icon file
Source: "..\VoiceLite\VoiceLite.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\VoiceLite"; Filename: "{app}\VoiceLite.exe"
Name: "{autodesktop}\VoiceLite"; Filename: "{app}\VoiceLite.exe"; Tasks: desktopicon

[Run]
; Launch VoiceLite after installation
Filename: "{app}\VoiceLite.exe"; Description: "{cm:LaunchProgram,VoiceLite}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{localappdata}\VoiceLite"

[Code]
var
  DependencyPage: TWizardPage;

procedure LinkClick(Sender: TObject);
var
  ErrorCode: Integer;
  URL: String;
begin
  // Get URL from the label's hint
  URL := TNewStaticText(Sender).Hint;
  ShellExec('open', URL, '', '', SW_SHOW, ewNoWait, ErrorCode);
end;

procedure InitializeWizard;
var
  Y: Integer;
  Lbl: TNewStaticText;
begin
  // Create one page with clickable links
  DependencyPage := CreateCustomPage(wpWelcome, 'Requirements',
    'VoiceLite requires the following to run:');

  Y := 10;

  // VC++ section
  Lbl := TNewStaticText.Create(DependencyPage);
  Lbl.Parent := DependencyPage.Surface;
  Lbl.Caption := '1. Microsoft Visual C++ Runtime 2015-2022 (x64)';
  Lbl.Top := Y;

  Y := Y + 25;
  Lbl := TNewStaticText.Create(DependencyPage);
  Lbl.Parent := DependencyPage.Surface;
  Lbl.Caption := 'https://aka.ms/vs/17/release/vc_redist.x64.exe';
  Lbl.Top := Y;
  Lbl.Font.Color := clBlue;
  Lbl.Font.Style := [fsUnderline];
  Lbl.Cursor := crHand;
  Lbl.Hint := 'https://aka.ms/vs/17/release/vc_redist.x64.exe';
  Lbl.OnClick := @LinkClick;

  Y := Y + 40;

  // .NET section
  Lbl := TNewStaticText.Create(DependencyPage);
  Lbl.Parent := DependencyPage.Surface;
  Lbl.Caption := '2. .NET 8.0 Desktop Runtime (x64)';
  Lbl.Top := Y;

  Y := Y + 25;
  Lbl := TNewStaticText.Create(DependencyPage);
  Lbl.Parent := DependencyPage.Surface;
  Lbl.Caption := 'https://dotnet.microsoft.com/download/dotnet/8.0';
  Lbl.Top := Y;
  Lbl.Font.Color := clBlue;
  Lbl.Font.Style := [fsUnderline];
  Lbl.Cursor := crHand;
  Lbl.Hint := 'https://dotnet.microsoft.com/download/dotnet/8.0';
  Lbl.OnClick := @LinkClick;

  Y := Y + 40;

  // Footer text
  Lbl := TNewStaticText.Create(DependencyPage);
  Lbl.Parent := DependencyPage.Surface;
  Lbl.Caption := 'If you don''t have these installed, VoiceLite will not launch.' + #13#10 +
                 'Click the links above to download, then click Next to continue.';
  Lbl.Top := Y;
  Lbl.AutoSize := True;
end;

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
    if MsgBox('Do you want to remove VoiceLite settings and transcription history?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\VoiceLite'), True, True, True);
    end;
  end;
end;
