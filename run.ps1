param (
    [switch]$NoBuild,
    [switch]$Fast,
    [switch]$SelfCheck,
    [string]$Configuration = "Debug",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

# La sonda de autorrevisión del host (--selfcheck): corre la aplicación real sobre la plataforma headless con
# Skia real, mide el lienzo con puntero inyectado y ESPERA el proceso — el veredicto es su código de salida
# (0 = verificado), y el informe queda en FileFlow.App\bin\<config>\net10.0\selfcheck-report.txt.
if ($SelfCheck) { $AppArgs = @("--selfcheck") + $AppArgs }

$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Launcher Script      " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$skipBuild = $NoBuild -or $Fast

if (-not $skipBuild) {
    Write-Host "`nCompilando la solución FileFlow.slnx ($Configuration)..." -ForegroundColor Yellow
    dotnet build (Join-Path $scriptDir "FileFlow.slnx") -c $Configuration

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n[ERROR] La compilación falló. Revisa los errores." -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "`nCompilación exitosa." -ForegroundColor Green
} else {
    Write-Host "`n[Modo Rápido] Omitiendo compilación (-NoBuild)..." -ForegroundColor Yellow
}

$exePath = Join-Path $scriptDir "FileFlow.App\bin\$Configuration\net10.0\FileFlow.App.exe"

if (-not (Test-Path $exePath)) {
    $fallbackConfig = if ($Configuration -eq "Debug") { "Release" } else { "Debug" }
    $fallbackPath = Join-Path $scriptDir "FileFlow.App\bin\$fallbackConfig\net10.0\FileFlow.App.exe"
    if (Test-Path $fallbackPath) {
        $exePath = $fallbackPath
        $Configuration = $fallbackConfig
    } else {
        Write-Host "`n[ERROR] No se encontró el ejecutable en '$exePath'." -ForegroundColor Red
        Write-Host "Ejecuta '.\run.ps1' sin el parámetro -NoBuild para compilar primero." -ForegroundColor Gray
        exit 1
    }
}

Write-Host "Iniciando FileFlow Studio ($Configuration)..." -ForegroundColor Green

if ($SelfCheck) {
    # El veredicto de la sonda es su código de salida: el script lo hereda.
    & $exePath @AppArgs
    exit $LASTEXITCODE
}

if ($AppArgs -and $AppArgs.Count -gt 0) {
    Start-Process -FilePath $exePath -ArgumentList $AppArgs -WorkingDirectory (Split-Path -Parent $exePath)
} else {
    Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath)
}

