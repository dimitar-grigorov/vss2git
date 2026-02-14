@echo off
setlocal enabledelayedexpansion

set "ANALYZE_EXE=C:\Program Files (x86)\Microsoft Visual SourceSafe\analyze.exe"
set "MAX_FIX_PASSES=3"

:: Check for elevation
net session >nul 2>&1
if errorlevel 1 (
    echo ERROR: This script requires Administrator privileges.
    echo Right-click and select "Run as administrator".
    echo.
    pause
    exit /b 1
)

if "%~1"=="" (
    echo Usage: %~nx0 "path\to\vss\database"
    echo.
    echo   The path should point to the VSS database root directory
    echo   ^(the folder containing srcsafe.ini^).
    echo.
    echo Example:
    echo   %~nx0 "D:\Projects_Deploy\VSS_Libs - Copy"
    echo.
    pause
    exit /b 1
)

set "VSS_DB=%~1"

if not exist "%ANALYZE_EXE%" (
    echo ERROR: analyze.exe not found at "%ANALYZE_EXE%"
    pause
    exit /b 1
)
if not exist "%VSS_DB%\srcsafe.ini" (
    echo ERROR: srcsafe.ini not found in "%VSS_DB%" - not a valid VSS database.
    pause
    exit /b 1
)
if not exist "%VSS_DB%\data" (
    echo ERROR: data directory not found in "%VSS_DB%" - not a valid VSS database.
    pause
    exit /b 1
)

echo VSS Database Analyze ^& Repair
echo Database: "%VSS_DB%"
echo.

:: version.dat is required by analyze.exe v8.0 but missing in VSS 6.0 databases
set "VERSION_DAT_COPIED=0"
if not exist "%VSS_DB%\version.dat" (
    if exist "%VSS_DB%\data\version.dat" (
        echo version.dat not found at database root ^(normal for VSS 6.0^).
        copy /y "%VSS_DB%\data\version.dat" "%VSS_DB%\version.dat" >nul 2>&1
        if errorlevel 1 (
            echo ERROR: Failed to copy version.dat from data directory.
            pause
            exit /b 1
        )
        set "VERSION_DAT_COPIED=1"
        echo Copied from data directory.
    ) else (
        echo WARNING: version.dat not found anywhere. analyze.exe may not work.
        call :PromptYesNo "Continue anyway?" continueAnyway y
        if /i not "!continueAnyway!"=="y" (
            pause
            exit /b 0
        )
    )
)
echo.

:: Clear stale logins and temp files
if exist "%VSS_DB%\loggedin" del /q "%VSS_DB%\loggedin\*.*" 2>nul
if exist "%VSS_DB%\temp" del /q "%VSS_DB%\temp\*.*" 2>nul

:: Lock database to prevent new VSS logins during repair
set "LOCK_FILE=%VSS_DB%\data\ADMIN.LCK"
set "LOCK_CREATED=0"
if not exist "%LOCK_FILE%" (
    echo. > "%LOCK_FILE%" 2>nul
    if exist "%LOCK_FILE%" (
        set "LOCK_CREATED=1"
        echo Database locked ^(ADMIN.LCK created^).
    )
)
echo.

:: Step 1: Diagnostic scan (read-only)
echo === Step 1: Diagnostic Scan ===
echo.

"%ANALYZE_EXE%" -v4 -i- "%VSS_DB%"
set "DIAG_EXIT=%ERRORLEVEL%"

set "LOG_FILE=%VSS_DB%\backup\analyze.log"
if exist "%LOG_FILE%" (
    echo.
    type "%LOG_FILE%"
)
echo.

if %DIAG_EXIT%==0 (
    echo No errors found. Database appears healthy.
    goto :Cleanup
)

call :PromptYesNo "Errors found. Run analyze -F to fix?" doFix y
if /i not "!doFix!"=="y" (
    echo Skipping repair.
    goto :Cleanup
)

:: Step 2: Repair loop (may need multiple passes)
set "PASS=0"

:FixLoop
set /a "PASS+=1"
echo.
echo === Step 2: Repair Pass %PASS% of %MAX_FIX_PASSES% ===
echo.

"%ANALYZE_EXE%" -F -V4 -D -i- "%VSS_DB%"
set "FIX_EXIT=!ERRORLEVEL!"

if exist "%LOG_FILE%" (
    echo.
    type "%LOG_FILE%"
)
echo.

if !FIX_EXIT! EQU 0 (
    echo Pass %PASS%: No further errors to fix.
    goto :FixDone
)
if !FIX_EXIT! EQU 1 (
    if %PASS% LSS %MAX_FIX_PASSES% (
        echo Pass %PASS% fixed some errors. Running another pass...
        goto :FixLoop
    ) else (
        echo Reached maximum of %MAX_FIX_PASSES% fix passes.
    )
)

:FixDone

:: Step 3: Verification
echo.
echo === Step 3: Verification ===
echo.

"%ANALYZE_EXE%" -v4 -i- "%VSS_DB%"
set "VERIFY_EXIT=%ERRORLEVEL%"

if exist "%LOG_FILE%" (
    echo.
    type "%LOG_FILE%"
)
echo.

if %VERIFY_EXIT%==0 (
    echo SUCCESS: Database is now clean.
) else (
    echo WARNING: Issues may still remain.
)
echo.

:Cleanup

if "%LOCK_CREATED%"=="1" (
    del /f "%LOCK_FILE%" 2>nul
    if not exist "%LOCK_FILE%" echo Database unlocked.
    echo.
)

if "%VERSION_DAT_COPIED%"=="1" (
    call :PromptYesNo "Delete version.dat from database root?" delVersion y
    if /i "!delVersion!"=="y" (
        del /f "%VSS_DB%\version.dat" 2>nul
        if not exist "%VSS_DB%\version.dat" echo version.dat removed.
    ) else (
        echo version.dat left in place.
    )
    echo.
)

echo Done.
echo.
pause
exit /b 0

:PromptYesNo
set "prompt_msg=%~1"
set "var_name=%~2"
set "default_val=%~3"
set "user_input="
set /p "user_input=%prompt_msg% (Y/n): "
if "!user_input!"=="" set "user_input=!default_val!"
set "%var_name%=!user_input!"
exit /b
