; Per-user Inno Setup 6 installer for Loadout (formerly Startup Profiles), from CodePrint's templates/dotnet/installer.
; build-installer.ps1 supplies the version and the publish folder. It installs to the same folder as
; install/install.ps1, so either one upgrades the other. The in-app updater runs this setup with
; /SILENT; setup then closes the running app, replaces it, and starts it again.

#define MyAppName "Loadout"
#define MyAppPublisher "Lukas Krejci"
#define MyAppExeName "Loadout.exe"
#define OldAppExeName "StartupProfiles.exe"
#define OldAppFolderName "StartupProfiles"
#define MyAppProcessName "Loadout"
#define MyArtifactName "Loadout"
#define MyAppUrl "https://github.com/lukr-99/startup-profiles"

#ifndef MyAppVersion
  #error MyAppVersion must be supplied by build-installer.ps1
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by build-installer.ps1
#endif
#ifndef MyVersionInfoVersion
  #error MyVersionInfoVersion must be supplied by build-installer.ps1
#endif

[Setup]
AppId={{605888E1-75BE-4488-BAC1-E31A221A8618}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
; Same place install.ps1 uses, so the two never leave two copies behind. AppId stays the same as the old
; Startup Profiles installer, so this setup upgrades it in place. UsePreviousAppDir=no sends the files to the
; new folder instead of the old one; [InstallDelete] removes the old folder.
DefaultDirName={localappdata}\Programs\Loadout
UsePreviousAppDir=no
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=dist
OutputBaseFilename={#MyArtifactName}-Setup-{#MyAppVersion}
SetupIconFile=..\src\Loadout.App\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
; The old exe is closed too, so an upgrade over a running Startup Profiles works.
CloseApplicationsFilter={#MyAppExeName},{#OldAppExeName}
VersionInfoVersion={#MyVersionInfoVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
LicenseFile=..\LICENSE.md

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Offered on a first install only. An upgrade never moves startup apps; the app's "New startup apps"
; window asks about later ones instead.
Name: "takeover"; Description: "Start my current startup apps from the Everything profile instead of Windows (switch them off in Windows; uninstall switches them back on)"; GroupDescription: "Startup apps:"; Check: IsFirstInstall
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
; The agent skill, as install.ps1 installs it.
Source: "..\skills\loadout\*"; DestDir: "{%USERPROFILE}\.claude\skills\loadout"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Comment: "Loadout"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; The app owns its registry formats (see ARCHITECTURE.md), so registration runs through its own commands.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-login"; Flags: runhidden waituntilterminated; StatusMsg: "Starting Loadout at login..."
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-protocol"; Flags: runhidden waituntilterminated; StatusMsg: "Registering loadout:// links..."
Filename: "{app}\{#MyAppExeName}"; Parameters: "--take-over-startup"; Flags: runhidden waituntilterminated; Tasks: takeover; StatusMsg: "Moving startup apps into the Everything profile..."
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
; A silent run is the in-app updater: start the updated app again.
Filename: "{app}\{#MyAppExeName}"; Flags: nowait; Check: WizardSilent

[UninstallRun]
; CloseApplications only applies to Setup. The uninstaller closes nothing, so a running tray app
; would keep its files open. Stop only the copy that runs from {app}, and wait for it to exit.
; Inno Setup turns {{ into { in a parameter but leaves }} unchanged, so the script block closes
; with a single }.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ""$p = @(Get-Process -Name '{#MyAppProcessName}' -ErrorAction SilentlyContinue | Where-Object {{ $_.Path -eq '{app}\{#MyAppExeName}' }); $p | Stop-Process -Force; $p | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue"""; Flags: runhidden waituntilterminated; RunOnceId: "StopRunningApp"
; Switch back on the startup apps the app switched off, then remove its login and protocol entries.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--restore-startup"; Flags: runhidden waituntilterminated; RunOnceId: "RestoreStartup"
Filename: "{app}\{#MyAppExeName}"; Parameters: "--unregister-login"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterLogin"
Filename: "{app}\{#MyAppExeName}"; Parameters: "--unregister-protocol"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterProtocol"

[InstallDelete]
; An upgrade over Startup Profiles: remove the old program folder, shortcuts and agent skill. The data folder
; %APPDATA%\StartupProfiles is not touched; the app copies it to %APPDATA%\Loadout on its first start.
Type: filesandordirs; Name: "{localappdata}\Programs\{#OldAppFolderName}"
Type: files; Name: "{autoprograms}\Startup Profiles.lnk"
Type: files; Name: "{autodesktop}\Startup Profiles.lnk"
Type: filesandordirs; Name: "{%USERPROFILE}\.claude\skills\startup-profiles"

[UninstallDelete]
Type: filesandordirs; Name: "{%USERPROFILE}\.claude\skills\loadout"

[Code]
var
  FirstInstall: Boolean;

{ Decided before any file is copied: true when neither Loadout nor Startup Profiles is installed yet. }
function InitializeSetup(): Boolean;
begin
  FirstInstall := not FileExists(ExpandConstant('{localappdata}\Programs\Loadout\{#MyAppExeName}')) and
                  not FileExists(ExpandConstant('{localappdata}\Programs\{#OldAppFolderName}\{#OldAppExeName}'));
  Result := True;
end;

function IsFirstInstall(): Boolean;
begin
  Result := FirstInstall;
end;
