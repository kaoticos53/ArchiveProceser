@echo off
title FileFlow Studio - Launcher
echo =========================================
echo   FileFlow Studio - Launcher Script
echo =========================================
echo.

if /i "%1"=="nobuild" goto launch
if /i "%1"=="fast" goto launch
if /i "%1"=="-nobuild" goto launch
if /i "%1"=="--nobuild" goto launch

echo Compilando la solucion FileFlow.slnx...
dotnet build FileFlow.slnx -c Debug
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] La compilacion fallo. Revisa los errores.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Compilacion exitosa.

:launch
set EXE=FileFlow.App\bin\Debug\net9.0-windows\FileFlow.App.exe
if not exist "%EXE%" set EXE=FileFlow.App\bin\Release\net9.0-windows\FileFlow.App.exe

if not exist "%EXE%" (
    echo [ERROR] No se encontro el ejecutable compilado.
    echo Ejecuta run.bat sin parametros para compilar primero.
    pause
    exit /b 1
)

echo Iniciando FileFlow Studio...
start "" "%EXE%" %*

