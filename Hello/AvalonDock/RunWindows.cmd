@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0BuildAndTest.ps1" -Configuration Release
exit /b %ERRORLEVEL%
