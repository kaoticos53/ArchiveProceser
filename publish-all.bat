@echo off
REM Publica FileFlow Studio para Windows y Linux (.NET 9)
REM Uso: publish-all.bat [-FrameworkDependent] [-WindowsOnly] [-LinuxOnly]
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-all.ps1" %*
