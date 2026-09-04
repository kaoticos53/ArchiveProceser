@echo off
title FileFlow Studio - Fast Launch (NoBuild)
echo =========================================
echo   FileFlow Studio - Fast Launch (NoBuild)
echo =========================================
echo.

set EXE_DEBUG=FileFlow.App\bin\Debug\net9.0-windows\FileFlow.App.exe
set EXE_RELEASE=FileFlow.App\bin\Release\net9.0-windows\FileFlow.App.exe

if exist "%EXE_DEBUG%" (
    echo [OK] Iniciando FileFlow Studio (Debug)...
    start "" /d "FileFlow.App\bin\Debug\net9.0-windows" "%EXE_DEBUG%" %*
    exit /b 0
)

if exist "%EXE_RELEASE%" (
    echo [OK] Iniciando FileFlow Studio (Release)...
    start "" /d "FileFlow.App\bin\Release\net9.0-windows" "%EXE_RELEASE%" %*
    exit /b 0
)

echo [AVISO] No se encontro el ejecutable compilado.
echo Por favor, compila la solucion ejecutando primero: run.bat o run.ps1
echo.
pause
exit /b 1
