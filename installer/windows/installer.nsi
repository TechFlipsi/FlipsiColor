; FlipsiColor — NSIS Installer (Windows)
; PUBLISH_DIR wird via makensis /DPUBLISH_DIR="..." übergeben
!define APPNAME "FlipsiColor"
!define APPVERSION "0.6.0"
!define APP_REGKEY "Software\${APPNAME}"
!define APP_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"

!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "publish"
!endif

Name "${APPNAME} ${APPVERSION}"
OutFile "FlipsiColor-v${APPVERSION}-Windows-x64-Installer.exe"
; Issue #10: Install to Program Files, admin rights required
InstallDir "$PROGRAMFILES64\${APPNAME}"
RequestExecutionLevel admin

!include "MUI2.nsh"

; ── Icons ──
!define MUI_ICON "${PUBLISH_DIR}\flipsicolor.ico"
!define MUI_UNICON "${PUBLISH_DIR}\flipsicolor.ico"

; Issue #10: Store previous install dir in registry for upgrades
InstallDirRegKey HKLM "${APP_REGKEY}" "InstallDir"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "German"

Section "${APPNAME} ${APPVERSION}" SecMain
    SectionIn RO
    SetOutPath "$INSTDIR"
    File /r "${PUBLISH_DIR}\*.*"

    ; Issue #10: Register in HKLM for Add/Remove Programs + upgrade detection
    WriteRegStr HKLM "${APP_REGKEY}" "InstallDir" "$INSTDIR"
    WriteRegStr HKLM "${APP_REGKEY}" "Version" "${APPVERSION}"

    ; Add/Remove Programs entry
    WriteRegStr HKLM "${APP_UNINST_KEY}" "DisplayName" "${APPNAME}"
    WriteRegStr HKLM "${APP_UNINST_KEY}" "DisplayVersion" "${APPVERSION}"
    WriteRegStr HKLM "${APP_UNINST_KEY}" "Publisher" "TechFlipsi"
    WriteRegStr HKLM "${APP_UNINST_KEY}" "DisplayIcon" "$INSTDIR\FlipsiColor.exe"
    WriteRegStr HKLM "${APP_UNINST_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegStr HKLM "${APP_UNINST_KEY}" "URLInfoAbout" "https://github.com/TechFlipsi/FlipsiColor"

    WriteUninstaller "$INSTDIR\Uninstall.exe"
    CreateDirectory "$SMPROGRAMS\${APPNAME}"
    CreateShortCut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\FlipsiColor.exe" "" "$INSTDIR\FlipsiColor.exe" 0
    CreateShortCut "$SMPROGRAMS\${APPNAME}\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
    CreateShortCut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\FlipsiColor.exe" "" "$INSTDIR\FlipsiColor.exe" 0
SectionEnd

Section "Uninstall"
    ; Kill running instance before uninstall (Issue #10)
    ; NSIS doesn't have a built-in mutex, but we clean up thoroughly
    RMDir /r "$INSTDIR"
    Delete "$SMPROGRAMS\${APPNAME}\*.*"
    RMDir "$SMPROGRAMS\${APPNAME}"
    Delete "$DESKTOP\${APPNAME}.lnk"
    DeleteRegKey HKLM "${APP_UNINST_KEY}"
    DeleteRegKey HKLM "${APP_REGKEY}"
SectionEnd