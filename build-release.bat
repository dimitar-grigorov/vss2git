@echo off
REM Vss2Git Release Build Script
REM Builds both CLI and GUI applications for release

setlocal enabledelayedexpansion

set VERSION=1.0.14
set OUTPUT_DIR=release

echo ===================================
echo === Vss2Git Release Build v%VERSION% ===
echo ===================================
echo.

REM Clean previous builds
echo Cleaning previous builds...
if exist %OUTPUT_DIR% (
    rmdir /s /q %OUTPUT_DIR%
)
mkdir %OUTPUT_DIR%

REM Build CLI application
echo.
echo Building CLI application...
set CLI_DIR=%OUTPUT_DIR%\Vss2Git.Cli-%VERSION%
dotnet publish Vss2Git.Cli\Vss2Git.Cli.csproj ^
    --configuration Release ^
    --output %CLI_DIR% ^
    --self-contained false ^
    --runtime win-x64

if errorlevel 1 (
    echo ERROR: CLI build failed!
    exit /b 1
)

REM Build GUI application
echo.
echo Building GUI application...
set GUI_DIR=%OUTPUT_DIR%\Vss2Git-%VERSION%
dotnet publish Vss2Git\Vss2Git.csproj ^
    --configuration Release ^
    --output %GUI_DIR% ^
    --self-contained false ^
    --runtime win-x64

if errorlevel 1 (
    echo ERROR: GUI build failed!
    exit /b 1
)

REM Copy documentation files
echo.
echo Copying documentation...
copy README.md %CLI_DIR%\ >nul 2>&1
copy LICENSE.md %CLI_DIR%\ >nul 2>&1
copy ARCHITECTURE.md %CLI_DIR%\ >nul 2>&1
copy CODE_ANALYSIS.md %CLI_DIR%\ >nul 2>&1

copy README.md %GUI_DIR%\ >nul 2>&1
copy LICENSE.md %GUI_DIR%\ >nul 2>&1
copy ARCHITECTURE.md %GUI_DIR%\ >nul 2>&1
copy CODE_ANALYSIS.md %GUI_DIR%\ >nul 2>&1

REM Create ZIP archives using PowerShell
echo.
echo Creating release archives...
powershell -Command "Compress-Archive -Path '%CLI_DIR%' -DestinationPath '%OUTPUT_DIR%\Vss2Git.Cli-%VERSION%-win-x64.zip' -Force"
powershell -Command "Compress-Archive -Path '%GUI_DIR%' -DestinationPath '%OUTPUT_DIR%\Vss2Git-%VERSION%-win-x64.zip' -Force"

REM Display results
echo.
echo ===================================
echo === Build Complete! ===
echo ===================================
echo.
echo Release packages created in: %OUTPUT_DIR%\
dir %OUTPUT_DIR%\*.zip
echo.
echo Next steps:
echo   1. Test the release packages
echo   2. Create a git tag: git tag -a v%VERSION% -m "Release v%VERSION%"
echo   3. Push the tag: git push origin v%VERSION%
echo   4. Go to GitHub and create a new release from the tag
echo   5. Upload the ZIP files to the GitHub release
echo.

endlocal
