; FlipsiColor Installer (Inno Setup) — v0.6.1
#define AppName "FlipsiColor"
#define AppExeName "FlipsiColor.exe"
#define AppPublisher "TechFlipsi"
#define AppURL "https://github.com/TechFlipsi/FlipsiColor"
#ifndef AppVersion
  #define AppVersion "0.6.1"
#endif

[Setup]
; AppId must stay the same across versions — enables upgrade detection
AppId={{B8C4D5E6-F7A8-9012-BCDE-2345678901CD}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
; Issue #10: Allow destination directory customization (default is 'no' = show page)
DisableDirPage=no
; Issue #10: Admin rights for proper Program Files installation
PrivilegesRequired=admin
; Issue #10: Prevent installation while app is running
AppMutex=FlipsiColorAppMutex
; Use previous install directory if found (upgrade detection)
UsePreviousAppDir=yes
UsePreviousTasks=yes
UsePreviousLanguage=yes
OutputBaseFilename=FlipsiColor-{#AppVersion}-setup
Compression=lzma2/Ultra
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=..\flipsicolor.ico
; VersionInfo aktualisiert auf 0.6.1
VersionInfoVersion={#AppVersion}.0
VersionInfoProductVersion={#AppVersion}.0

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Source-Pfad nach Architektur-Bereinigung: WPF publish unter FlipsiColor/publish/win-x64/
Source: "..\FlipsiColor\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; VC++ Redistributable (optional — nur wenn im publish-Verzeichnis vorhanden)
Source: "..\FlipsiColor\publish\win-x64\VC_redist.x64.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
; Start-Menü Eintrag
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
; Desktop-Verknüpfung (optional via Task)
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Install VC++ Redistributable if not present (silent install)
Filename: "{app}\VC_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Visual C++ Runtime..."; Flags: runhidden skipifdoesntexist
; Launch app after installation
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Issue #10: Detect existing installation and offer upgrade.
// Inno Setup also handles upgrades automatically via the same AppId.
function InitializeSetup(): Boolean;
var
  Version: String;
  FoundExisting: Boolean;
begin
  Result := True;
  FoundExisting := False;

  // Check HKLM (admin installs — current PrivilegesRequired=admin)
  if RegQueryStringValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B8C4D5E6-F7A8-9012-BCDE-2345678901CD}_is1', 'DisplayVersion', Version) then
    FoundExisting := True
  // Check HKCU (old user-level installs from PrivilegesRequired=lowest in v0.5.4 and earlier)
  else if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B8C4D5E6-F7A8-9012-BCDE-2345678901CD}_is1', 'DisplayVersion', Version) then
    FoundExisting := True;

  if FoundExisting then
  begin
    if MsgBox('FlipsiColor ' + Version + ' is already installed. Do you want to upgrade to version {#AppVersion}?', mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
end;