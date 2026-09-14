param(
    [switch]$InstallDeps = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

$webDir = Join-Path $scriptDir "FileFlow.Web"
$serverWwwroot = Join-Path $scriptDir "FileFlow.Server\wwwroot"

Write-Host "Compilando FileFlow.Web (React 19 + Vite + React Flow)..." -ForegroundColor Cyan

if ($InstallDeps -or (-not (Test-Path (Join-Path $webDir "node_modules")))) {
    Write-Host "  -> Instalando dependencias de Node.js en FileFlow.Web..." -ForegroundColor DarkGray
    Push-Location $webDir
    & npm install
    Pop-Location
}

Push-Location $webDir
& npm run build
Pop-Location

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error compilando FileFlow.Web" -ForegroundColor Red
    exit $LASTEXITCODE
}

$distDir = Join-Path $webDir "dist"
if (Test-Path $distDir) {
    # 1. Copiar a FileFlow.Server/wwwroot
    if (-not (Test-Path $serverWwwroot)) {
        New-Item -ItemType Directory -Path $serverWwwroot -Force | Out-Null
    }
    Copy-Item -Path "$distDir\*" -Destination $serverWwwroot -Recurse -Force

    # 2. Copiar a FileFlow.App.Photino/wwwroot
    $photinoWwwroot = Join-Path $scriptDir "FileFlow.App.Photino\wwwroot"
    if (-not (Test-Path $photinoWwwroot)) {
        New-Item -ItemType Directory -Path $photinoWwwroot -Force | Out-Null
    }
    Copy-Item -Path "$distDir\*" -Destination $photinoWwwroot -Recurse -Force

    # 3. Copiar a bin de Photino si existe
    $photinoBinDir = Join-Path $scriptDir "FileFlow.App.Photino\bin\Debug\net9.0-windows\wwwroot"
    if (Test-Path (Split-Path -Parent $photinoBinDir)) {
        if (-not (Test-Path $photinoBinDir)) { New-Item -ItemType Directory -Path $photinoBinDir -Force | Out-Null }
        Copy-Item -Path "$distDir\*" -Destination $photinoBinDir -Recurse -Force
    }

    Write-Host "Frontend web empaquetado con exito en Server y Photino!" -ForegroundColor Green
}
