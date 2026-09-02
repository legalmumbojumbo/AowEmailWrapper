; Inno Setup script for the Age of Wonders Email Wrapper.
; Build with Installer\build-installer.ps1, which publishes the app (framework-dependent, win-x64)
; and then runs this script. Requires Inno Setup 6.1 or later.

#ifndef MyAppVersion
  #define MyAppVersion "2.0.0"
#endif
#define MyAppName "Age of Wonders Email Wrapper"
#define MyAppExeName "AowEmailWrapper.exe"
#define MyAppUrl "https://github.com/davidhoness/AowEmailWrapper"
#define PublishDir "..\publish\win-x64"
#define DotNetRuntimeUrl "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
#define DotNetRuntimeFile "windowsdesktop-runtime-8-win-x64.exe"

[Setup]
AppId={{B7C1A0F3-6E1B-4A62-9C0D-2B2E4A6F1D21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=Age of Wonders PBEM community
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
AppUpdatesURL={#MyAppUrl}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
; Per-user install, no administrator prompt (the .NET runtime installer prompts on its own if needed)
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE
OutputDir=..\publish
OutputBaseFilename=AowEmailWrapper-{#MyAppVersion}-setup
SetupIconFile=..\Projects\AowEmailWrapper\online48.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\Docs\*.html"; DestDir: "{app}\Docs"; Flags: ignoreversion
Source: "..\Docs\images\*"; DestDir: "{app}\Docs\images"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Quick Start Guide"; Filename: "{app}\Docs\QuickStart.html"
Name: "{group}\Wrapper Manual"; Filename: "{app}\Docs\Manual.html"
Name: "{group}\Uninstall the Wrapper"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
; The Wrapper's own update check runs this setup silently with /RELAUNCH=1 so the program comes back afterwards
Filename: "{app}\{#MyAppExeName}"; Flags: nowait; Check: RelaunchRequested

[Code]
const
  RuntimeMajor = '8.';
  RuntimeRegKey = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValue = 'Age of Wonders Email Wrapper';
  AppDataFolder = 'AowEmailWrapper';

var
  DownloadPage: TDownloadWizardPage;

{ ---------------------------------------------------------------- .NET 8 Desktop Runtime }

function HasDesktopRuntime(RootKey: Integer): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(RootKey, RuntimeRegKey, Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Pos(RuntimeMajor, Names[I]) = 1 then
      begin
        Result := True;
        exit;
      end;
end;

function IsDesktopRuntimeInstalled: Boolean;
begin
  Result := HasDesktopRuntime(HKLM32) or HasDesktopRuntime(HKLM64);
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if ProgressMax <> 0 then
    Log(Format('  %d of %d bytes done.', [Progress, ProgressMax]));
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
end;

{ Downloads and installs the .NET 8 Desktop Runtime when it is missing. Returning a non-empty
  string aborts setup with that message. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Installer: String;
  ResultCode: Integer;
begin
  Result := '';

  if IsDesktopRuntimeInstalled then
  begin
    Log('.NET 8 Desktop Runtime already installed.');
    exit;
  end;

  Log('.NET 8 Desktop Runtime missing, downloading.');
  DownloadPage.Clear;
  DownloadPage.Add('{#DotNetRuntimeUrl}', '{#DotNetRuntimeFile}', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      if DownloadPage.AbortedByUser then
        Result := 'The download of the .NET 8 Desktop Runtime was cancelled. The Wrapper needs it to run.'
      else
        Result := 'The .NET 8 Desktop Runtime could not be downloaded: ' + GetExceptionMessage + #13#10#13#10 +
                  'Install it from https://dotnet.microsoft.com/download/dotnet/8.0 (Desktop Runtime, x64) and run this setup again.';
      exit;
    end;
  finally
    DownloadPage.Hide;
  end;

  Installer := ExpandConstant('{tmp}\{#DotNetRuntimeFile}');
  if not ShellExec('', Installer, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'The .NET 8 Desktop Runtime installer could not be started.';
    exit;
  end;

  { 0 = installed, 1638 = a newer version is already present, 3010 = installed but Windows wants a restart later }
  if (ResultCode <> 0) and (ResultCode <> 1638) and (ResultCode <> 3010) then
    Result := Format('The .NET 8 Desktop Runtime installer failed (code %d). Install it from https://dotnet.microsoft.com/download/dotnet/8.0 and run this setup again.', [ResultCode]);
end;

{ ---------------------------------------------------------------- running instance }

procedure CloseRunningWrapper;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#MyAppExeName} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeSetup: Boolean;
begin
  CloseRunningWrapper;
  Result := True;
end;

{ True when setup was started by the Wrapper's update check (AowEmailWrapper.exe passes /RELAUNCH=1) }
function RelaunchRequested: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;

function InitializeUninstall: Boolean;
begin
  CloseRunningWrapper;
  Result := True;
end;

{ ---------------------------------------------------------------- uninstall clean-up }

{ The Wrapper points each game at its local SMTP listener; clear that so the games do not try to
  send turns to a program that is no longer there. }
procedure BlankGameEmailSettings;
var
  Games: TArrayOfString;
  Key: String;
  I: Integer;
begin
  SetArrayLength(Games, 4);
  Games[0] := 'Age of Wonders';
  Games[1] := 'Age of Wonders II';
  Games[2] := 'Age of Wonders Shadow Magic';
  Games[3] := 'AoW - MP Evolution';

  for I := 0 to GetArrayLength(Games) - 1 do
  begin
    Key := 'Software\Triumph Studios\' + Games[I] + '\Email';
    if RegKeyExists(HKCU, Key) then
    begin
      RegWriteStringValue(HKCU, Key, 'Attachment Directory', '');
      RegWriteStringValue(HKCU, Key, 'SMTP Server', '');
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Folder: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKCU, RunKey, RunValue);
    BlankGameEmailSettings;

    Folder := ExpandConstant('{userappdata}\') + AppDataFolder;
    if DirExists(Folder) and not UninstallSilent then
      if MsgBox('Also remove your Wrapper settings, saved sign-ins and turn history?' + #13#10#13#10 + Folder,
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(Folder, True, True, True);
  end;
end;
