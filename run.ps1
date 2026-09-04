param (
    [switch]$NoBuild,
    [switch]$Fast,
    [string]$Configuration = "Debug",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

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

$exePath = Join-Path $scriptDir "FileFlow.App\bin\$Configuration\net9.0-windows\FileFlow.App.exe"

if (-not (Test-Path $exePath)) {
    $fallbackConfig = if ($Configuration -eq "Debug") { "Release" } else { "Debug" }
    $fallbackPath = Join-Path $scriptDir "FileFlow.App\bin\$fallbackConfig\net9.0-windows\FileFlow.App.exe"
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

if ($AppArgs -and $AppArgs.Count -gt 0) {
    Start-Process -FilePath $exePath -ArgumentList $AppArgs -WorkingDirectory (Split-Path -Parent $exePath)
} else {
    Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath)
}

