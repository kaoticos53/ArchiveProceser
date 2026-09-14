@echo off
title FileFlow Studio - Photino (Hybrid Desktop)
echo ============================================
echo   FileFlow Studio - Photino (Hybrid Desktop)
echo ============================================
echo.

if /i "%1"=="nobuild" goto launch
if /i "%1"=="-nobuild" goto launch
if /i "%1"=="--nobuild" goto launch
if /i "%1"=="fast" goto launch

echo Compilando FileFlow.Web...
pushd FileFlow.Web
call npm run build
popd
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Fallo al compilar FileFlow.Web.
    pause
    exit /b %ERRORLEVEL%
)

echo Copiando frontend a Photino wwwroot...
if not exist "FileFlow.App.Photino\wwwroot" mkdir "FileFlow.App.Photino\wwwroot"
xcopy /s /y /q "FileFlow.Web\dist\*" "FileFlow.App.Photino\wwwroot\"

echo Compilando FileFlow.App.Photino...
dotnet build FileFlow.App.Photino\FileFlow.App.Photino.csproj -c Debug
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] La compilacion fallo. Revisa los errores.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Compilacion exitosa.

:launch
set EXE_DEBUG=FileFlow.App.Photino\bin\Debug\net9.0-windows\FileFlow.App.Photino.exe
set EXE_RELEASE=FileFlow.App.Photino\bin\Release\net9.0-windows\FileFlow.App.Photino.exe

if exist "%EXE_DEBUG%" (
    echo Iniciando FileFlow Studio Photino (Debug)...
    start "" /d "FileFlow.App.Photino\bin\Debug\net9.0-windows" "%EXE_DEBUG%" %*
    exit /b 0
)

if exist "%EXE_RELEASE%" (
    echo Iniciando FileFlow Studio Photino (Release)...
    start "" /d "FileFlow.App.Photino\bin\Release\net9.0-windows" "%EXE_RELEASE%" %*
    exit /b 0
)

echo [ERROR] No se encontro el ejecutable compilado.
echo Ejecuta run-photino.bat sin parametros para compilar primero.
pause
exit /b 1
