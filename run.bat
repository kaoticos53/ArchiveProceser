@echo off
REM FileFlow Studio - Host Uno (WinUI 3) Launcher para Windows. Hermano .bat de run.ps1.
setlocal
set CONFIG=Debug
set PROJECT=FileFlow.App.Uno\FileFlow.App.Uno.csproj
set EXE=FileFlow.App.Uno\bin\%CONFIG%\net10.0-windows10.0.19041.0\FileFlow.App.Uno.exe

echo =========================================
echo   FileFlow Studio - Host Uno (WinUI 3)
echo =========================================

dotnet build "%PROJECT%" -c %CONFIG% --nologo -v:m
if errorlevel 1 (
    echo [ERROR] La compilacion del host Uno fallo.
    exit /b 1
)

if not exist "%EXE%" (
    echo [ERROR] No se encontro el ejecutable en '%EXE%'.
    exit /b 1
)

start "" "%EXE%" %*
endlocal
