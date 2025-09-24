; Inno Setup Script for VoiceLite

#define MyAppName "VoiceLite"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Your Company"
#define MyAppURL "https://yourwebsite.com"
#define MyAppExeName "VoiceLite.exe"

[Setup]
AppId={{PUT-GUID-HERE}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\Output
OutputBaseFilename=VoiceLiteSetup
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
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
; Main application files
Source: "..\VoiceLite\bin\Release\net8.0-windows\VoiceLite.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\VoiceLite\bin\Release\net8.0-windows\*.json"; DestDir: "{app}"; Flags: ignoreversion

; Whisper files
Source: "..\VoiceLite\whisper\*"; DestDir: "{app}\whisper"; Flags: ignoreversion recursesubdirs

; Icon file
Source: "..\VoiceLite\VoiceLite.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{localappdata}\VoiceLite"

[Code]
// Check for .NET 8.0 Desktop Runtime
function IsDotNet8DesktopRuntimeInstalled: Boolean;
var
  ResultCode: Integer;
begin
  Result := False;

  // Check if dotnet is installed and has the right version
  if Exec('cmd.exe', '/c dotnet --list-runtimes | findstr /C:"Microsoft.WindowsDesktop.App 8."', '',
          SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := (ResultCode = 0);
  end;
end;

function InitializeSetup: Boolean;
var
  ErrMsg: String;
  ResultCode: Integer;
begin
  Result := True;

  if not IsDotNet8DesktopRuntimeInstalled then
  begin
    ErrMsg := 'VoiceLite requires .NET Desktop Runtime 8.0 or later.'#13#10#13#10 +
              'Would you like to download it now?'#13#10 +
              '(Setup will exit after download)';

    if MsgBox(ErrMsg, mbConfirmation, MB_YESNO) = IDYES then
    begin
      // Open download page in browser
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0/runtime', '', '', SW_SHOW, ewNoWait, ResultCode);
      Result := False; // Cancel installation
      MsgBox('Please install .NET Desktop Runtime 8.0 and run this setup again.', mbInformation, MB_OK);
    end
    else
    begin
      Result := False;
    end;
  end;
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