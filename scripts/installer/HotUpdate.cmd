@echo off
title BIM TOOL - Hot Reload (Cap Nhat Truc Tiep Khong Can Tat Revit)
color 0B
cd /d "%~dp0"

echo =========================================================================
echo       BIM TOOL - HOT RELOAD (CAP NHAT KHONG CAN TAT REVIT)
echo =========================================================================
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -Path '%~dp0HotUpdate.ps1' -ErrorAction SilentlyContinue; & '%~dp0HotUpdate.ps1'"

echo.
echo =========================================================================
pause
