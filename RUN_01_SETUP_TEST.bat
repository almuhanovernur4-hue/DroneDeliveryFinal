@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp001_BUILD_AND_TEST.ps1"
echo.
pause
