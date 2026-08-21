@echo off
chcp 65001 > nul
title FileFlow Studio - Test Runner

echo =========================================
echo    FileFlow Studio - Suite de Pruebas
echo =========================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0test.ps1" %*

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Ocurrió un fallo durante la ejecución de las pruebas.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Presione cualquier tecla para salir...
pause > nul
