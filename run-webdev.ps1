param (
    [switch]$WithServer,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Web Dev (Vite + React)   " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

$webDir = Join-Path $scriptDir "FileFlow.Web"

# Instalar dependencias si faltan
$needsInstall = (-not (Test-Path (Join-Path $webDir "node_modules\.bin\vite.cmd"))) -and (-not (Test-Path (Join-Path $webDir "node_modules\.bin\vite")))
if ($needsInstall) {
    Write-Host "`n  -> Instalando dependencias de Node.js..." -ForegroundColor DarkGray
    Push-Location $webDir
    & npm install
    Pop-Location
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Fallo al instalar dependencias." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# Lanzar servidor .NET en segundo plano si se pidió
if ($WithServer) {
    $serverProject = Join-Path $scriptDir "FileFlow.Server\FileFlow.Server.csproj"
    Write-Host "`nIniciando FileFlow.Server en segundo plano..." -ForegroundColor Yellow
    Start-Process -FilePath "dotnet" `
        -ArgumentList "run --project `"$serverProject`" -c $Configuration --launch-profile http" `
        -WorkingDirectory $scriptDir
    Start-Sleep -Seconds 2
    Write-Host "  -> Servidor iniciado en http://localhost:5002" -ForegroundColor Green
}

# Lanzar Vite dev server en primer plano
Write-Host "`nIniciando Vite dev server (hot reload)..." -ForegroundColor Magenta
Write-Host "  Presiona Ctrl+C para detener." -ForegroundColor DarkGray
Write-Host ""

Push-Location $webDir
& npm run dev
Pop-Location
