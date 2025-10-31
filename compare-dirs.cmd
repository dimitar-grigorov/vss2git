@echo off
REM Fast directory comparison using PowerShell -Command (bypasses all signing restrictions)

if "%~1"=="" (
    echo Usage: compare-dirs.cmd "SourceDir" "TargetDir"
    echo.
    echo Example:
    echo   compare-dirs.cmd "D:\Projects\Packages" "D:\Projects_Deploy\vss-libs-fastimport\packages"
    exit /b 1
)

if "%~2"=="" (
    echo Error: TargetDir parameter is required
    exit /b 1
)

REM Create temp PowerShell script
set "TEMP_PS=%TEMP%\compare-dirs-%RANDOM%.ps1"

REM Write PowerShell script to temp file
(
echo $src='%~1'
echo $tgt='%~2'
echo $excl=@^('! Borland !','! CodeGear - RAD Studio !','ToPAZ','.git','.vs'^)
echo.
echo Write-Host ''
echo Write-Host 'Comparing directory structures...' -ForegroundColor Cyan
echo Write-Host "Source: $src" -ForegroundColor Gray
echo Write-Host "Target: $tgt" -ForegroundColor Gray
echo Write-Host "Excluded: $($excl -join ', ')" -ForegroundColor Gray
echo Write-Host ''
echo.
echo if ^(-not ^(Test-Path $src^)^) {
echo     Write-Error "Source directory does not exist: $src"
echo     exit 1
echo }
echo.
echo if ^(-not ^(Test-Path $tgt^)^) {
echo     Write-Error "Target directory does not exist: $tgt"
echo     exit 1
echo }
echo.
echo Write-Host 'Scanning source directory...' -ForegroundColor Yellow
echo $srcItems = @{}
echo Get-ChildItem -Path $src -Recurse -Force -ErrorAction SilentlyContinue ^| ForEach-Object {
echo     $rel = $_.FullName.Substring^($src.Length + 1^)
echo     $skip = $false
echo     foreach ^($e in $excl^) {
echo         if ^($rel -match "^(^^^^^|\\\\^)$^([regex]::Escape^($e^)^)^(\\\\^|$^)"^) {
echo             $skip = $true
echo             break
echo         }
echo     }
echo     if ^(-not $skip^) {
echo         $srcItems[$rel.ToLower^(^)] = $rel
echo     }
echo }
echo.
echo Write-Host 'Scanning target directory...' -ForegroundColor Yellow
echo $tgtItems = @{}
echo Get-ChildItem -Path $tgt -Recurse -Force -ErrorAction SilentlyContinue ^| ForEach-Object {
echo     $rel = $_.FullName.Substring^($tgt.Length + 1^)
echo     $skip = $false
echo     foreach ^($e in $excl^) {
echo         if ^($rel -match "^(^^^^^|\\\\^)$^([regex]::Escape^($e^)^)^(\\\\^|$^)"^) {
echo             $skip = $true
echo             break
echo         }
echo     }
echo     if ^(-not $skip^) {
echo         $tgtItems[$rel.ToLower^(^)] = $rel
echo     }
echo }
echo.
echo Write-Host ''
echo Write-Host '========================================' -ForegroundColor Cyan
echo Write-Host 'COMPARISON RESULTS' -ForegroundColor Cyan
echo Write-Host '========================================' -ForegroundColor Cyan
echo Write-Host ''
echo Write-Host "Total items in source: $($srcItems.Count)" -ForegroundColor Gray
echo Write-Host "Total items in target: $($tgtItems.Count)" -ForegroundColor Gray
echo Write-Host ''
echo.
echo $missing = @^(^)
echo foreach ^($k in $srcItems.Keys^) {
echo     if ^(-not $tgtItems.ContainsKey^($k^)^) {
echo         $missing += $srcItems[$k]
echo     }
echo }
echo.
echo $extra = @^(^)
echo foreach ^($k in $tgtItems.Keys^) {
echo     if ^(-not $srcItems.ContainsKey^($k^)^) {
echo         $extra += $tgtItems[$k]
echo     }
echo }
echo.
echo if ^($missing.Count -eq 0 -and $extra.Count -eq 0^) {
echo     Write-Host 'SUCCESS: Directory structures match perfectly!' -ForegroundColor Green
echo     Write-Host 'All files and folders present in both directories.' -ForegroundColor Green
echo } else {
echo     if ^($missing.Count -gt 0^) {
echo         Write-Host "Items MISSING in TARGET ($($missing.Count) items):" -ForegroundColor Red
echo         Write-Host 'These items exist in VSS but are MISSING in Git:' -ForegroundColor Red
echo         Write-Host ''
echo         $missing ^| Sort-Object ^| ForEach-Object { Write-Host "MISSING_IN_GIT: $_" }
echo         Write-Host ''
echo     }
echo.
echo     if ^($extra.Count -gt 0^) {
echo         Write-Host "Items ONLY in TARGET ($($extra.Count) items):" -ForegroundColor Magenta
echo         Write-Host 'These items exist in Git but are NOT in VSS source:' -ForegroundColor Magenta
echo         Write-Host ''
echo         $extra ^| Sort-Object ^| ForEach-Object { Write-Host "EXTRA_IN_GIT: $_" }
echo         Write-Host ''
echo     }
echo.
echo     Write-Host '========================================' -ForegroundColor Cyan
echo     Write-Host 'SUMMARY' -ForegroundColor Cyan
echo     Write-Host '========================================' -ForegroundColor Cyan
echo     Write-Host "Missing in Git: $($missing.Count) items" -ForegroundColor Red
echo     Write-Host "Extra in Git:   $($extra.Count) items" -ForegroundColor Magenta
echo }
echo.
echo Write-Host ''
) > "%TEMP_PS%"

REM Execute PowerShell script using -Command with Get-Content (bypasses signing)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ([ScriptBlock]::Create((Get-Content -Path '%TEMP_PS%' -Raw)))"

REM Capture exit code
set RESULT=%ERRORLEVEL%

REM Cleanup temp file
del "%TEMP_PS%" 2>nul

exit /b %RESULT%
