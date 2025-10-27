; Inno Setup Script for VoiceLite
; Requires Inno Setup 6.2.0 or later

#define MyAppName "VoiceLite"
#define MyAppVersion "1.1.51"
#define MyAppPublisher "VoiceLite Software"
#define MyAppURL "https://voicelite.com"
#define MyAppExeName "VoiceLite.exe"
#define MyAppCopyright "Copyright (c) 2025 VoiceLite Software"

[Setup]
; Application Info
AppId={{E5B3A7C4-9D8F-4A2B-B1E6-7F3C5A9D8E2A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/updates
AppCopyright={#MyAppCopyright}

; Directory Settings
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output Settings
OutputDir=Installer
OutputBaseFilename=VoiceLite-Setup-{#MyAppVersion}
SetupIconFile=VoiceLite\VoiceLite.ico
Compression=lzma2/ultra64
SolidCompression=yes
InternalCompressLevel=ultra64

; Security Settings
; TODO: Uncomment after obtaining EV code signing certificate (see CODE_SIGNING_SETUP.md)
; SignTool=signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /a $f
; SignedUninstaller=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; UI Settings
WizardStyle=modern
; WizardImageFile=installer-wizard.bmp
; WizardSmallImageFile=installer-small.bmp
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
ShowLanguageDialog=no
DisableWelcomePage=no
DisableReadyPage=no
DisableDirPage=no
LicenseFile=..\EULA.txt

; Versioning
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoDescription=Professional Speech-to-Text for Windows
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

; Runtime Requirements
MinVersion=10.0
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Main Application
Source: "VoiceLite\bin\Release\net8.0-windows\VoiceLite.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "VoiceLite\bin\Release\net8.0-windows\VoiceLite.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "VoiceLite\bin\Release\net8.0-windows\VoiceLite.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "VoiceLite\bin\Release\net8.0-windows\VoiceLite.deps.json"; DestDir: "{app}"; Flags: ignoreversion

; Dependencies
Source: "VoiceLite\bin\Release\net8.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion

; Whisper Files (Encrypted models will be included)
Source: "VoiceLite\bin\Release\net8.0-windows\whisper\*"; DestDir: "{app}\whisper"; Flags: ignoreversion recursesubdirs

; Visual C++ Runtime Redistributable (not bundled in lean MVP - users must install separately)
; Source: "vcredist\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

; License Files
Source: "..\EULA.txt"; DestDir: "{app}"; Flags: ignoreversion
; Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startup

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start VoiceLite when Windows starts"; GroupDescription: "Additional options:"; Flags: unchecked

[Registry]
; Application registration
Root: HKLM; Subkey: "Software\{#MyAppPublisher}"; Flags: uninsdeletekeyifempty
Root: HKLM; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"
Root: HKLM; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"

; Windows App Paths
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey

[Run]
; Install Visual C++ Runtime (not bundled in lean MVP)
; Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/quiet /norestart"; StatusMsg: "Installing Visual C++ Runtime..."; Flags: waituntilterminated

; Launch application after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up application data
Type: filesandordirs; Name: "{userappdata}\VoiceLite"
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

// Licensing prompts removed; installer now assumes fully free build

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // No post-install licensing actions required.
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  // Check if application is running
  if CheckForMutexes('VoiceLite_SingleInstance') then
  begin
    Result := 'VoiceLite is currently running. Please close it before continuing.';
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Clean up registry
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\VoiceLite');
    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\VoiceLite Software');

    // Ask if user wants to remove settings
    if MsgBox('Do you want to remove all VoiceLite settings?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\VoiceLite'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\VoiceLite'), True, True, True);
    end;
  end;
end;
