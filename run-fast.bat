@echo off
REM FileFlow Studio - Host Uno (WinUI 3) Launcher rapido para Windows: no compila.
setlocal
set CONFIG=Debug
set EXE=FileFlow.App.Uno\bin\%CONFIG%\net10.0-windows10.0.19041.0\FileFlow.App.Uno.exe

echo =========================================
echo   FileFlow Studio - Host Uno (WinUI 3)
echo =========================================

if not exist "%EXE%" (
    echo [ERROR] No se encontro el ejecutable en '%EXE%'.
    echo Compila primero con: run.bat o run.ps1
    exit /b 1
)

start "" "%EXE%" %*
endlocal
