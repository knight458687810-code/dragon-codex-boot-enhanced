@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install-start-menu.ps1" -Action Restore
pause
