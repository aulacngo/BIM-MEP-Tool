@echo off
title Cai Dat BIM TOOL - Revit Add-in
color 0A
cd /d "%~dp0"

echo =========================================================================
echo                DANG CAI DAT BIM TOOL CHO REVIT...
echo =========================================================================
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -Path '%~dp0Install-BIN-Tool.ps1' -ErrorAction SilentlyContinue; & '%~dp0Install-BIN-Tool.ps1'"

echo.
echo =========================================================================
pause
