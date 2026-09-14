@echo off
title FileFlow Studio - Web Server
echo ==========================================
echo   FileFlow Studio - Web Server Launcher
echo ==========================================
echo.

if /i "%1"=="nobuild" goto nobuild
if /i "%1"=="-nobuild" goto nobuild
if /i "%1"=="--nobuild" goto nobuild
if /i "%1"=="fast" goto nobuild

echo Compilando FileFlow.Server...
dotnet build FileFlow.Server\FileFlow.Server.csproj -c Debug
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] La compilacion fallo. Revisa los errores.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Compilacion exitosa.

:launch
echo.
echo Iniciando FileFlow.Server en http://localhost:5002 ...
echo Presiona Ctrl+C para detener el servidor.
echo.
dotnet run --project FileFlow.Server\FileFlow.Server.csproj -c Debug --launch-profile http
exit /b %ERRORLEVEL%

:nobuild
echo [Modo Rapido] Omitiendo compilacion...
goto launch
