@echo off
setlocal

cd /d "%~dp0"

REM ============================================================
REM  PTG Oil System - NORMAL FAST launcher
REM  Uses one safe incremental build, then runs the compiled DLL.
REM  Database migrations are not applied unless explicitly requested.
REM  Use run-dev.bat only when live watch/reload is required.
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

echo Starting PTG Oil System on http://localhost:5000
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-local.ps1"
set "RUN_EXIT=%ERRORLEVEL%"

if not "%RUN_EXIT%"=="0" (
    echo.
    echo PTG Oil System stopped with exit code %RUN_EXIT%.
    echo.
    pause
    exit /b %RUN_EXIT%
)

endlocal
