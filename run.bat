@echo off
title FileFlow Studio - Build & Run
echo =========================================
echo   FileFlow Studio - Build & Run Script
echo =========================================
echo.
echo Compilando la solucion FileFlow.slnx...
dotnet build FileFlow.slnx -c Debug
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] La compilacion fallo. Revisa los errores.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Compilacion exitosa. Iniciando FileFlow Studio...
start "" "FileFlow.App\bin\Debug\net9.0-windows\FileFlow.App.exe"
