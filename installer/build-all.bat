@echo off
REM Script rápido para compilar todos los instaladores (Windows + Linux)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-all.ps1" %*
pause
