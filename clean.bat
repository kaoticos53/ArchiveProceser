@echo off
title FileFlow Studio - Limpieza Integral
echo =========================================
echo   FileFlow Studio - Limpieza de Repositorio
echo =========================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0clean.ps1" %*
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [AVISO] El proceso de limpieza finalizo con codigo %ERRORLEVEL%.
)
