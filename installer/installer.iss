; FlipsiColor Installer (Inno Setup) — v0.4.0
#define AppName "FlipsiColor"
#define AppExeName "FlipsiColor.exe"
#define AppPublisher "TechFlipsi"
#define AppURL "https://github.com/TechFlipsi/FlipsiColor"
#ifndef AppVersion
  #define AppVersion "0.4.0"
#endif

[Setup]
AppId={{B8C4D5E6-F7A8-9012-BCDE-2345678901CD}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputBaseFilename=FlipsiColor-{#AppVersion}-setup
Compression=lzma2/Ultra
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExeName}
; VersionInfo aktualisiert auf 0.4.0
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