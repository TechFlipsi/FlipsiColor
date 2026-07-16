; FlipsiColor — NSIS Installer (Windows)
; PUBLISH_DIR wird via makensis /DPUBLISH_DIR="..." übergeben
!define APPNAME "FlipsiColor"
!define APPVERSION "0.5.3"

!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "publish"
!endif

Name "${APPNAME} ${APPVERSION}"
OutFile "FlipsiColor-v${APPVERSION}-Windows-x64-Installer.exe"
InstallDir "$LOCALAPPDATA\${APPNAME}"
RequestExecutionLevel user

!include "MUI2.nsh"

; ── Icons ──
!define MUI_ICON "${PUBLISH_DIR}\flipsicolor.ico"
!define MUI_UNICON "${PUBLISH_DIR}\flipsicolor.ico"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "German"

Section "${APPNAME} ${APPVERSION}" SecMain
    SectionIn RO
    SetOutPath "$INSTDIR"
    File /r "${PUBLISH_DIR}\*.*"
    WriteRegStr HKCU "Software\${APPNAME}" "InstallDir" "$INSTDIR"
    WriteRegStr HKCU "Software\${APPNAME}" "Version" "${APPVERSION}"
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    CreateDirectory "$SMPROGRAMS\${APPNAME}"
    CreateShortCut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\FlipsiColor.exe" "" "$INSTDIR\FlipsiColor.exe" 0
    CreateShortCut "$SMPROGRAMS\${APPNAME}\Deinstallieren.lnk" "$INSTDIR\Uninstall.exe"
    CreateShortCut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\FlipsiColor.exe" "" "$INSTDIR\FlipsiColor.exe" 0
SectionEnd

Section "Uninstall"
    RMDir /r "$INSTDIR"
    Delete "$SMPROGRAMS\${APPNAME}\*.*"
    RMDir "$SMPROGRAMS\${APPNAME}"
    Delete "$DESKTOP\${APPNAME}.lnk"
    DeleteRegKey HKCU "Software\${APPNAME}"
SectionEnd