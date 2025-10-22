; Inno Setup Script for VoiceLite

[Setup]
AppId={{A06BC0AA-DD0A-4341-9E41-68AC0D6E541E}
AppName=VoiceLite
AppVersion=1.0.13
AppPublisher=VoiceLite
AppPublisherURL=https://voicelite.app
AppSupportURL=https://voicelite.app
AppUpdatesURL=https://voicelite.app
DefaultDirName={autopf}\VoiceLite
DisableProgramGroupPage=yes
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
; Main application files
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\VoiceLite.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\VoiceLite\bin\Release\net8.0-windows\win-x64\publish\*.json"; DestDir: "{app}"; Flags: ignoreversion

; Whisper files (only tiny model for free tier - Pro users download others in-app)
Source: "..\whisper\*.dll"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\whisper\*.exe"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "..\whisper\ggml-tiny.bin"; DestDir: "{app}\whisper"; Flags: ignoreversion

; Icon file
Source: "..\VoiceLite\VoiceLite.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\VoiceLite"; Filename: "{app}\VoiceLite.exe"
Name: "{autodesktop}\VoiceLite"; Filename: "{app}\VoiceLite.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VoiceLite.exe"; Description: "{cm:LaunchProgram,VoiceLite}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{localappdata}\VoiceLite"

[Code]
// Check if .NET 8 Desktop Runtime is installed
function IsDotNet8Installed: Boolean;
var
  Version: String;
begin
  Result := False;

  if RegQueryStringValue(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost', 'Version', Version) then
  begin
    if (Length(Version) > 0) and (Pos('8.', Version) > 0) then
      Result := True;
  end;

  if not Result then
  begin
    if RegKeyExists(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App\8.0') or
       RegKeyExists(HKLM64, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App\8.0') then
      Result := True;
  end;
end;

// Check if Visual C++ Runtime is installed
function IsVCRuntimeInstalled: Boolean;
var
  Installed: Cardinal;
begin
  Result := False;

  if RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) then
  begin
    Result := (Installed = 1);
  end;

  if not Result then
  begin
    if RegQueryDWordValue(HKLM64, 'SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) then
    begin
      Result := (Installed = 1);
    end;
  end;
end;

// Initialize setup - check prerequisites and show friendly message
function InitializeSetup: Boolean;
var
  MissingPrereqs: String;
  NeedsDotNet, NeedsVCRuntime: Boolean;
  Response: Integer;
begin
  NeedsDotNet := not IsDotNet8Installed;
  NeedsVCRuntime := not IsVCRuntimeInstalled;

  // If prerequisites are missing, show information with download links
  if NeedsDotNet or NeedsVCRuntime then
  begin
    MissingPrereqs := 'VoiceLite requires the following software to run:' + #13#10#13#10;

    if NeedsDotNet then
    begin
      MissingPrereqs := MissingPrereqs + '• .NET 8 Desktop Runtime' + #13#10;
      MissingPrereqs := MissingPrereqs + '  https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe' + #13#10#13#10;
    end;

    if NeedsVCRuntime then
    begin
      MissingPrereqs := MissingPrereqs + '• Visual C++ Runtime 2015-2022' + #13#10;
      MissingPrereqs := MissingPrereqs + '  https://aka.ms/vs/17/release/vc_redist.x64.exe' + #13#10#13#10;
    end;

    MissingPrereqs := MissingPrereqs +
      'Click OK to continue installation.' + #13#10 +
      'After installation finishes, please install the required components above.' + #13#10#13#10 +
      'VoiceLite will not run until these are installed.';

    MsgBox(MissingPrereqs, mbInformation, MB_OK);
  end;

  // Always allow installation to continue
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
