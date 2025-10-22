; Inno Setup Script for VoiceLite

[Setup]
AppId={{A06BC0AA-DD0A-4341-9E41-68AC0D6E541E}
AppName=VoiceLite
AppVersion=1.0.69
AppPublisher=VoiceLite
AppPublisherURL=https://voicelite.app
AppSupportURL=https://voicelite.app
AppUpdatesURL=https://voicelite.app
DefaultDirName={autopf}\VoiceLite
DisableProgramGroupPage=yes
LicenseFile=LICENSE.txt
OutputDir=..\..
OutputBaseFilename=VoiceLite-Setup
SetupIconFile=..\VoiceLite\VoiceLite.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main application files (top-level only - whisper directory handled separately)
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\VoiceLite.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\*.json"; DestDir: "{app}"; Flags: ignoreversion

; Whisper files (only tiny model for free tier - Pro users download others in-app)
Source: "..\whisper\clblast.dll"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\whisper\libopenblas.dll"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\whisper\whisper.dll"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\whisper\whisper.exe"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\whisper\ggml-tiny.bin"; DestDir: "{app}\whisper"; Flags: ignoreversion

; Icon file
Source: "..\VoiceLite\VoiceLite.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\VoiceLite"; Filename: "{app}\VoiceLite.exe"
Name: "{autodesktop}\VoiceLite"; Filename: "{app}\VoiceLite.exe"; Tasks: desktopicon

[Run]
; Auto-launch removed - users should install .NET first if they don't have it
; They can launch VoiceLite from desktop icon after installing prerequisites
; Filename: "{app}\VoiceLite.exe"; Description: "{cm:LaunchProgram,VoiceLite}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{localappdata}\VoiceLite"

[Code]
// Show prerequisite message to EVERYONE before installation
function InitializeSetup: Boolean;
begin
  MsgBox(
    'VoiceLite requires the following to run:' + #13#10#13#10 +
    '• .NET 8 Desktop Runtime' + #13#10 +
    '  https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe' + #13#10#13#10 +
    '• Visual C++ Runtime 2015-2022' + #13#10 +
    '  https://aka.ms/vs/17/release/vc_redist.x64.exe' + #13#10#13#10 +
    'Please install these if you don''t have them before launching VoiceLite.' + #13#10#13#10 +
    'Click OK to continue to the license agreement.',
    mbInformation, MB_OK);

  Result := True;
end;

// Clean up settings on uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('Do you want to remove VoiceLite settings and temporary files?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\VoiceLite'), True, True, True);
    end;
  end;
end;
