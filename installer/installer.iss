; FlipsiColor Installer (Inno Setup) — v0.5.3
#define AppName "FlipsiColor"
#define AppExeName "FlipsiColor.exe"
#define AppPublisher "TechFlipsi"
#define AppURL "https://github.com/TechFlipsi/FlipsiColor"
#ifndef AppVersion
  #define AppVersion "0.5.3"
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
SetupIconFile=..\flipsicolor.ico
; VersionInfo aktualisiert auf 0.5.3
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
// BONUS Issue #9: Write the installer language to settings.json so the app
// starts in the language the user chose during installation.
// ActiveLanguage returns 'german' or 'english' -> map to 'de' or 'en'.
procedure CurStepChanged(CurStep: TSetupStep);
var
  SettingsPath: String;
  SettingsContent: String;
  AppLang: String;
  Dummy: Boolean;
begin
  if CurStep = ssPostInstall then
  begin
    SettingsPath := ExpandConstant('{localappdata}\FlipsiColor\settings.json');

    // Map installer language to app language code
    if ActiveLanguage = 'english' then
      AppLang := 'en'
    else
      AppLang := 'de';

    // If settings.json already exists, update the Sprache field;
    // otherwise create a minimal settings file with the chosen language.
    if FileExists(SettingsPath) then
    begin
      if LoadStringFromFile(SettingsPath, SettingsContent) then
      begin
        if Pos('"Sprache": "' + AppLang + '"', SettingsContent) = 0 then
        begin
          // Replace existing Sprache value
          StringChangeEx(SettingsContent, '"Sprache": "de"', '"Sprache": "' + AppLang + '"', True);
          StringChangeEx(SettingsContent, '"Sprache": "en"', '"Sprache": "' + AppLang + '"', True);
          Dummy := SaveStringToFile(SettingsPath, SettingsContent, False);
        end;
      end;
    end
    else
    begin
      // Create minimal settings.json with the installer language
      SettingsContent := '{' + #13#10 + '  "Sprache": "' + AppLang + '",' + #13#10 + '  "Theme": "System",' + #13#10 + '  "AutoUpdatePruefen": true' + #13#10 + '}';
      Dummy := ForceDirectories(ExtractFilePath(SettingsPath));
      Dummy := SaveStringToFile(SettingsPath, SettingsContent, False);
    end;
  end;
end;