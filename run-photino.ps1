param (
    [switch]$NoBuild,
    [switch]$Fast,
    [switch]$SkipWeb,
    [string]$Configuration = "Debug",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Photino (Hybrid Desktop)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$skipBuild = $NoBuild -or $Fast

# --- 1. Compilar frontend web y copiar a wwwroot de Photino ---
if (-not $skipBuild -and -not $SkipWeb) {
    $webDir = Join-Path $scriptDir "FileFlow.Web"
    $photinoWwwroot = Join-Path $scriptDir "FileFlow.App.Photino\wwwroot"

    $needsInstall = (-not (Test-Path (Join-Path $webDir "node_modules\.bin\tsc.cmd"))) -and (-not (Test-Path (Join-Path $webDir "node_modules\.bin\tsc")))
    if ($needsInstall) {
        Write-Host "`n  -> Instalando dependencias de Node.js..." -ForegroundColor DarkGray
        Push-Location $webDir
        & npm install
        Pop-Location
    }

    Write-Host "`nCompilando FileFlow.Web (React + Vite)..." -ForegroundColor Yellow
    Push-Location $webDir
    & npm run build
    Pop-Location

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n[ERROR] Fallo al compilar FileFlow.Web." -ForegroundColor Red
        exit $LASTEXITCODE
    }

    $webDistDir = Join-Path $webDir "dist"
    if (Test-Path $webDistDir) {
        if (-not (Test-Path $photinoWwwroot)) {
            New-Item -ItemType Directory -Path $photinoWwwroot -Force | Out-Null
        }
        Copy-Item -Path "$webDistDir\*" -Destination $photinoWwwroot -Recurse -Force
        Write-Host "  -> Frontend copiado a FileFlow.App.Photino/wwwroot" -ForegroundColor DarkGray
    }
}

# --- 2. Compilar proyecto .NET de Photino ---
if (-not $skipBuild) {
    $photinoProject = Join-Path $scriptDir "FileFlow.App.Photino\FileFlow.App.Photino.csproj"
    Write-Host "`nCompilando FileFlow.App.Photino ($Configuration)..." -ForegroundColor Yellow
    dotnet build $photinoProject -c $Configuration

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n[ERROR] La compilación de Photino falló." -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "`nCompilación exitosa." -ForegroundColor Green
} else {
    Write-Host "`n[Modo Rápido] Omitiendo compilación (-NoBuild)..." -ForegroundColor Yellow
}

# --- 3. Lanzar ejecutable ---
$exePath = Join-Path $scriptDir "FileFlow.App.Photino\bin\$Configuration\net9.0-windows\FileFlow.App.Photino.exe"

if (-not (Test-Path $exePath)) {
    $fallbackConfig = if ($Configuration -eq "Debug") { "Release" } else { "Debug" }
    $fallbackPath = Join-Path $scriptDir "FileFlow.App.Photino\bin\$fallbackConfig\net9.0-windows\FileFlow.App.Photino.exe"
    if (Test-Path $fallbackPath) {
        $exePath = $fallbackPath
        $Configuration = $fallbackConfig
    } else {
        Write-Host "`n[ERROR] No se encontró el ejecutable en '$exePath'." -ForegroundColor Red
        Write-Host "Ejecuta '.\run-photino.ps1' sin -NoBuild para compilar primero." -ForegroundColor Gray
        exit 1
    }
}

Write-Host "Iniciando FileFlow Studio Photino ($Configuration)..." -ForegroundColor Green

if ($AppArgs -and $AppArgs.Count -gt 0) {
    Start-Process -FilePath $exePath -ArgumentList $AppArgs -WorkingDirectory (Split-Path -Parent $exePath)
} else {
    Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath)
}
