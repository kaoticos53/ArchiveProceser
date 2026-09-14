@echo off
title FileFlow Studio - Web Dev (Vite + React)
echo =============================================
echo   FileFlow Studio - Web Dev (Vite + React)
echo =============================================
echo.

cd /d FileFlow.Web

if not exist "node_modules" (
    echo Instalando dependencias de Node.js...
    call npm install
)

echo Iniciando Vite dev server (hot reload)...
echo Presiona Ctrl+C para detener.
echo.
call npm run dev
