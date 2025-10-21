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
OutputDir=..\..\
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
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

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
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\VoiceLite"; Filename: "{app}\VoiceLite.exe"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\VoiceLite.exe"; Description: "{cm:LaunchProgram,VoiceLite}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{localappdata}\VoiceLite"

[Code]
// Check for Visual C++ Runtime 2015-2022
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

// Check for .NET 8.0 Desktop Runtime
function IsDotNet8DesktopRuntimeInstalled: Boolean;
var
  Version: String;
begin
  Result := False;

  // Check registry for .NET 8.0 Desktop Runtime installation
  // Check multiple possible locations
  if RegQueryStringValue(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost', 'Version', Version) then
  begin
    // If registry key exists, check if version 8.x is installed
    if (Length(Version) > 0) and (Pos('8.', Version) > 0) then
      Result := True;
  end;

  // Also check for the WindowsDesktop runtime specifically
  if not Result then
  begin
    if RegKeyExists(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App\8.0') or
       RegKeyExists(HKLM64, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App\8.0') then
      Result := True;
  end;
end;

function InitializeSetup: Boolean;
var
  NeedsDotNet: Boolean;
  NeedsVCRuntime: Boolean;
  ErrMsg: String;
  ResultCode: Integer;
begin
  Result := True;

  // Check what's missing
  NeedsDotNet := not IsDotNet8DesktopRuntimeInstalled;
  NeedsVCRuntime := not IsVCRuntimeInstalled;

  // Handle missing .NET Runtime
  if NeedsDotNet then
  begin
    ErrMsg := 'VoiceLite requires .NET Desktop Runtime 8.0.'#13#10#13#10;

    if NeedsVCRuntime then
      ErrMsg := ErrMsg + 'Additionally, Microsoft Visual C++ Runtime is required.'#13#10#13#10;

    ErrMsg := ErrMsg + 'Would you like to download the missing components now?'#13#10 +
                       '(Setup will continue after you install them)';

    if MsgBox(ErrMsg, mbConfirmation, MB_YESNO) = IDYES then
    begin
      // Open .NET download page (direct link to Windows x64 Desktop Runtime)
      ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.11-windows-x64-installer', '', '', SW_SHOW, ewNoWait, ResultCode);

      // If VC++ is also needed, open that too
      if NeedsVCRuntime then
      begin
        Sleep(1000); // Small delay to avoid confusion
        ShellExec('open', 'https://aka.ms/vs/17/release/vc_redist.x64.exe', '', '', SW_SHOW, ewNoWait, ResultCode);
      end;

      if NeedsVCRuntime then
        MsgBox('Downloads opened in your browser:' + #13#10#13#10 +
               '1. .NET 8 Desktop Runtime' + #13#10 +
               '2. Visual C++ Runtime' + #13#10 + #13#10 +
               'Click "Install" on each download, then run VoiceLite installer again.' + #13#10#13#10 +
               'Setup will now exit.',
               mbInformation, MB_OK)
      else
        MsgBox('Download page opened in your browser.' + #13#10#13#10 +
               'Click "Install" to download .NET 8 Desktop Runtime.' + #13#10 + #13#10 +
               'After installing, run VoiceLite installer again.' + #13#10#13#10 +
               'Setup will now exit.',
               mbInformation, MB_OK);

      // Exit immediately - don't re-check or confuse the user
      Result := False;
    end
    else
    begin
      Result := False;
    end;
  end
  // Handle missing VC++ Runtime only
  else if NeedsVCRuntime then
  begin
    ErrMsg := 'VoiceLite requires Microsoft Visual C++ Runtime 2015-2022.'#13#10#13#10 +
              'Would you like to download it now?';

    if MsgBox(ErrMsg, mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://aka.ms/vs/17/release/vc_redist.x64.exe', '', '', SW_SHOW, ewNoWait, ResultCode);

      MsgBox('Visual C++ Runtime download started.' + #13#10#13#10 +
             'Run the downloaded installer (vc_redist.x64.exe).' + #13#10 + #13#10 +
             'After installing, run VoiceLite installer again.' + #13#10#13#10 +
             'Setup will now exit.',
             mbInformation, MB_OK);

      // Exit immediately - don't re-check
      Result := False;
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