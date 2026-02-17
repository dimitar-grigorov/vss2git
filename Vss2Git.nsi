; Vss2Git NSIS Installer Script
; Per-user install (no admin required), supports GUI and CLI component selection

!define PRODUCT_NAME "Vss2Git"
!define PRODUCT_VERSION "1.1.0"
!define PRODUCT_PUBLISHER "Dimitar Grigorov"
!define PRODUCT_WEB_SITE "https://github.com/dimitar-grigorov/vss2git"
!define PRODUCT_REGISTRY_KEY "Software\Vss2Git"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\Vss2Git.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"

; Build output directories
!define GUI_BUILD_DIR "Vss2Git\bin\Release\net8.0-windows"
!define CLI_BUILD_DIR "Vss2Git.Cli\bin\Release\net8.0"

; No admin required
RequestExecutionLevel user

!include MUI2.nsh
!include Sections.nsh

; MUI Settings
!define MUI_ABORTWARNING
!define MUI_ICON "${NSISDIR}\Contrib\Graphics\Icons\modern-install.ico"
!define MUI_UNICON "${NSISDIR}\Contrib\Graphics\Icons\modern-uninstall.ico"

; Variables
Var guiInstalled

; Welcome page
!insertmacro MUI_PAGE_WELCOME
; License page
!insertmacro MUI_PAGE_LICENSE "LICENSE.txt"
; Components page
!insertmacro MUI_PAGE_COMPONENTS
; Directory page
!insertmacro MUI_PAGE_DIRECTORY
; Instfiles page
!insertmacro MUI_PAGE_INSTFILES
; Finish page
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "Launch Vss2Git (GUI)"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchGui
!define MUI_PAGE_CUSTOMFUNCTION_SHOW FinishPageShow
!insertmacro MUI_PAGE_FINISH

; Uninstaller pages
!insertmacro MUI_UNPAGE_INSTFILES

; Language files
!insertmacro MUI_LANGUAGE "English"

; MUI end ------

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "Vss2GitSetup-${PRODUCT_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Vss2Git"
InstallDirRegKey HKCU "${PRODUCT_REGISTRY_KEY}" "InstallPath"
ShowInstDetails show
ShowUnInstDetails show

; ---- Init ----

Function .onInit
  StrCpy $guiInstalled "0"

  ; Check for .NET 8.0 Desktop Runtime via registry
  ; Keys are in WOW6432Node — NSIS (32-bit) reads them natively without SetRegView 64
  StrCpy $0 0
  dotnet_loop:
    EnumRegValue $1 HKLM "SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App" $0
    StrCmp $1 "" dotnet_notfound
    StrCpy $2 $1 2
    StrCmp $2 "8." dotnet_found
    IntOp $0 $0 + 1
    Goto dotnet_loop
  dotnet_found:
    Goto dotnet_done
  dotnet_notfound:
    MessageBox MB_YESNO|MB_ICONQUESTION \
      "Microsoft .NET 8.0 Desktop Runtime is required but was not detected.$\n$\n\
      Click Yes to open the download page in your browser.$\n\
      Click No to continue installation anyway." \
      IDYES dotnet_download
    Goto dotnet_done
  dotnet_download:
    ExecShell "open" "https://dotnet.microsoft.com/download/dotnet/8.0"
    MessageBox MB_YESNO|MB_ICONINFORMATION \
      "After installing .NET 8.0 Desktop Runtime, you can re-run this installer.$\n$\n\
      Continue installation anyway?" \
      IDYES dotnet_done
    Abort
  dotnet_done:
FunctionEnd

; ---- Install Sections ----

Section "-Common Libraries" SEC_COMMON
  SetOutPath "$INSTDIR"
  SetOverwrite ifnewer

  ; Core libraries (sourced from CLI build output, identical in both)
  File "${CLI_BUILD_DIR}\Vss2Git.Core.dll"
  File "${CLI_BUILD_DIR}\Hpdi.VssLogicalLib.dll"
  File "${CLI_BUILD_DIR}\Hpdi.VssPhysicalLib.dll"
  File "${CLI_BUILD_DIR}\Hpdi.HashLib.dll"
  File "${CLI_BUILD_DIR}\LibGit2Sharp.dll"
  File "${CLI_BUILD_DIR}\Mapster.dll"
  File "${CLI_BUILD_DIR}\Mapster.Core.dll"
  File "${CLI_BUILD_DIR}\System.Text.Encoding.CodePages.dll"

  ; Native runtimes for LibGit2Sharp (Windows platforms only)
  SetOutPath "$INSTDIR\runtimes\win-x64\native"
  File "${CLI_BUILD_DIR}\runtimes\win-x64\native\git2-a418d9d.dll"

  SetOutPath "$INSTDIR\runtimes\win-x86\native"
  File "${CLI_BUILD_DIR}\runtimes\win-x86\native\git2-a418d9d.dll"

  SetOutPath "$INSTDIR\runtimes\win-arm64\native"
  File "${CLI_BUILD_DIR}\runtimes\win-arm64\native\git2-a418d9d.dll"

  ; Runtime-specific managed library
  SetOutPath "$INSTDIR\runtimes\win\lib\net8.0"
  File "${CLI_BUILD_DIR}\runtimes\win\lib\net8.0\System.Text.Encoding.CodePages.dll"
SectionEnd

Section "GUI Application" SEC_GUI
  SetOutPath "$INSTDIR"
  SetOverwrite ifnewer

  File "${GUI_BUILD_DIR}\Vss2Git.exe"
  File "${GUI_BUILD_DIR}\Vss2Git.dll"
  File "${GUI_BUILD_DIR}\Vss2Git.dll.config"
  File "${GUI_BUILD_DIR}\Vss2Git.runtimeconfig.json"
  File "${GUI_BUILD_DIR}\Vss2Git.deps.json"

  ; Shortcuts
  CreateDirectory "$SMPROGRAMS\Vss2Git"
  CreateShortCut "$SMPROGRAMS\Vss2Git\Vss2Git.lnk" "$INSTDIR\Vss2Git.exe"
  CreateShortCut "$DESKTOP\Vss2Git.lnk" "$INSTDIR\Vss2Git.exe"

  StrCpy $guiInstalled "1"
SectionEnd

Section "CLI Application" SEC_CLI
  SetOutPath "$INSTDIR"
  SetOverwrite ifnewer

  File "${CLI_BUILD_DIR}\Vss2Git.Cli.exe"
  File "${CLI_BUILD_DIR}\Vss2Git.Cli.dll"
  File "${CLI_BUILD_DIR}\Vss2Git.Cli.runtimeconfig.json"
  File "${CLI_BUILD_DIR}\Vss2Git.Cli.deps.json"
  File "${CLI_BUILD_DIR}\CommandLine.dll"
SectionEnd

Section "-Documentation" SEC_DOCS
  SetOutPath "$INSTDIR"
  SetOverwrite ifnewer

  File "LICENSE.html"
  File "LICENSE.txt"
  File "Vss2Git.html"
  File "Vss2Git.png"
SectionEnd

Section -AdditionalIcons
  WriteIniStr "$INSTDIR\${PRODUCT_NAME}.url" "InternetShortcut" "URL" "${PRODUCT_WEB_SITE}"
  CreateDirectory "$SMPROGRAMS\Vss2Git"
  CreateShortCut "$SMPROGRAMS\Vss2Git\Website.lnk" "$INSTDIR\${PRODUCT_NAME}.url"
SectionEnd

Section -Post
  WriteUninstaller "$INSTDIR\uninst.exe"
  WriteRegStr HKCU "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\Vss2Git.exe"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\Vss2Git.exe"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"

  ; Store install path for future reference
  WriteRegStr HKCU "${PRODUCT_REGISTRY_KEY}" "InstallPath" "$INSTDIR"
SectionEnd

; ---- Component selection validation ----
; (must be after sections so ${SEC_GUI} / ${SEC_CLI} are defined)

Function .onSelChange
  ; Ensure at least one of GUI or CLI is selected
  SectionGetFlags ${SEC_GUI} $0
  IntOp $0 $0 & ${SF_SELECTED}
  SectionGetFlags ${SEC_CLI} $1
  IntOp $1 $1 & ${SF_SELECTED}
  IntOp $0 $0 | $1
  IntCmp $0 0 none_selected done done
  none_selected:
    SectionGetFlags ${SEC_GUI} $0
    IntOp $0 $0 | ${SF_SELECTED}
    SectionSetFlags ${SEC_GUI} $0
    MessageBox MB_OK|MB_ICONINFORMATION "At least one application (GUI or CLI) must be selected."
  done:
FunctionEnd

; ---- Finish page callbacks ----

Function LaunchGui
  StrCmp $guiInstalled "1" 0 +2
    Exec "$INSTDIR\Vss2Git.exe"
FunctionEnd

Function FinishPageShow
  ; Hide "Launch GUI" checkbox if GUI was not installed
  StrCmp $guiInstalled "1" finish_done
    ShowWindow $mui.FinishPage.Run 0 ; SW_HIDE
  finish_done:
FunctionEnd

; ---- Component Descriptions ----

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_GUI} "Windows Forms GUI for interactive VSS to Git migration."
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_CLI} "Command-line tool for scripted/automated VSS to Git migration."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ---- Uninstaller ----

Function un.onInit
  MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 \
    "Are you sure you want to completely remove $(^Name) and all of its components?" IDYES +2
  Abort
FunctionEnd

Function un.onUninstSuccess
  HideWindow
  MessageBox MB_ICONINFORMATION|MB_OK "$(^Name) was successfully removed from your computer."
FunctionEnd

Section Uninstall
  ; GUI files
  Delete "$INSTDIR\Vss2Git.exe"
  Delete "$INSTDIR\Vss2Git.dll"
  Delete "$INSTDIR\Vss2Git.dll.config"
  Delete "$INSTDIR\Vss2Git.runtimeconfig.json"
  Delete "$INSTDIR\Vss2Git.deps.json"

  ; CLI files
  Delete "$INSTDIR\Vss2Git.Cli.exe"
  Delete "$INSTDIR\Vss2Git.Cli.dll"
  Delete "$INSTDIR\Vss2Git.Cli.runtimeconfig.json"
  Delete "$INSTDIR\Vss2Git.Cli.deps.json"
  Delete "$INSTDIR\CommandLine.dll"

  ; Common libraries
  Delete "$INSTDIR\Vss2Git.Core.dll"
  Delete "$INSTDIR\Hpdi.VssLogicalLib.dll"
  Delete "$INSTDIR\Hpdi.VssPhysicalLib.dll"
  Delete "$INSTDIR\Hpdi.HashLib.dll"
  Delete "$INSTDIR\LibGit2Sharp.dll"
  Delete "$INSTDIR\Mapster.dll"
  Delete "$INSTDIR\Mapster.Core.dll"
  Delete "$INSTDIR\System.Text.Encoding.CodePages.dll"

  ; Native runtimes
  Delete "$INSTDIR\runtimes\win-x64\native\git2-a418d9d.dll"
  RMDir "$INSTDIR\runtimes\win-x64\native"
  RMDir "$INSTDIR\runtimes\win-x64"

  Delete "$INSTDIR\runtimes\win-x86\native\git2-a418d9d.dll"
  RMDir "$INSTDIR\runtimes\win-x86\native"
  RMDir "$INSTDIR\runtimes\win-x86"

  Delete "$INSTDIR\runtimes\win-arm64\native\git2-a418d9d.dll"
  RMDir "$INSTDIR\runtimes\win-arm64\native"
  RMDir "$INSTDIR\runtimes\win-arm64"

  Delete "$INSTDIR\runtimes\win\lib\net8.0\System.Text.Encoding.CodePages.dll"
  RMDir "$INSTDIR\runtimes\win\lib\net8.0"
  RMDir "$INSTDIR\runtimes\win\lib"
  RMDir "$INSTDIR\runtimes\win"

  RMDir "$INSTDIR\runtimes"

  ; Documentation
  Delete "$INSTDIR\Vss2Git.html"
  Delete "$INSTDIR\Vss2Git.png"
  Delete "$INSTDIR\LICENSE.html"
  Delete "$INSTDIR\LICENSE.txt"

  ; Other files
  Delete "$INSTDIR\${PRODUCT_NAME}.url"
  Delete "$INSTDIR\uninst.exe"

  ; Shortcuts
  Delete "$SMPROGRAMS\Vss2Git\Vss2Git.lnk"
  Delete "$SMPROGRAMS\Vss2Git\Website.lnk"
  Delete "$DESKTOP\Vss2Git.lnk"

  RMDir "$SMPROGRAMS\Vss2Git"
  RMDir "$INSTDIR"

  ; Registry cleanup
  DeleteRegKey HKCU "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKCU "${PRODUCT_DIR_REGKEY}"
  DeleteRegKey HKCU "${PRODUCT_REGISTRY_KEY}"
  SetAutoClose true
SectionEnd
