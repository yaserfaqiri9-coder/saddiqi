@echo off
setlocal

cd /d "%~dp0"

REM ============================================================
REM  PTG Oil System - DEVELOPER WATCH launcher
REM  Auto-rebuilds and reloads on changes to .cshtml / CSS / C#.
REM  Use this while editing the UI. Press Ctrl+C to stop.
REM  (run-system.bat is for normal use; it does NOT auto-reload.)
REM ============================================================

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo ERROR: dotnet SDK was not found.
    echo Please install the .NET 8 SDK and try again.
    echo.
    pause
    exit /b 1
)

REM Refuse to start if something is already listening on port 5000,
REM so the old (non-watch) process cannot keep serving the old version.
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$c = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue; if ($c) { exit 0 } else { exit 1 }"

if not errorlevel 1 (
    echo.
    echo ERROR: Port 5000 is already in use.
    echo Close the running PTG Oil System window first, then run this again.
    echo.
    pause
    exit /b 1
)

if not exist "%~dp0scripts\run-local.ps1" (
    echo.
    echo ERROR: scripts\run-local.ps1 was not found.
    echo.
    pause
    exit /b 1
)

echo Starting PTG Oil System in DEV WATCH mode on http://localhost:5000
echo Changes to .cshtml / CSS / C# will reload automatically. Press Ctrl+C to stop.
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-local.ps1" -Watch
set "RUN_EXIT=%ERRORLEVEL%"

if not "%RUN_EXIT%"=="0" (
    echo.
    echo The watch session ended with exit code %RUN_EXIT%.
    echo.
    pause
    exit /b %RUN_EXIT%
)

echo.
echo The watch session stopped. Press any key to close this window.
pause >nul

endlocal
