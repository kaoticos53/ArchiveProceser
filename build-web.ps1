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
    if (-not (Test-Path $serverWwwroot)) {
        New-Item -ItemType Directory -Path $serverWwwroot -Force | Out-Null
    }
    Write-Host "  -> Copiando dist/ a FileFlow.Server/wwwroot..." -ForegroundColor DarkGray
    Copy-Item -Path "$distDir\*" -Destination $serverWwwroot -Recurse -Force
    Write-Host "Frontend web empaquetado con exito en: $serverWwwroot" -ForegroundColor Green
}
