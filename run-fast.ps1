param (
    [switch]$SelfCheck,
    [string]$Configuration = "Debug",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

# La sonda de autorrevisión del host (--selfcheck), sin compilar: mide el lienzo con puntero inyectado y el
# veredicto es el código de salida del proceso (el informe queda en selfcheck-report.txt, junto al ejecutable).
if ($SelfCheck) { $AppArgs = @("--selfcheck") + $AppArgs }

$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

$exePath = Join-Path $scriptDir "FileFlow.App\bin\$Configuration\net10.0\FileFlow.App.exe"

# Si no se encuentra en la configuracion solicitada, probar la otra configuracion (Debug/Release)
if (-not (Test-Path $exePath)) {
    $fallbackConfig = if ($Configuration -eq "Debug") { "Release" } else { "Debug" }
    $fallbackPath = Join-Path $scriptDir "FileFlow.App\bin\$fallbackConfig\net10.0\FileFlow.App.exe"
    if (Test-Path $fallbackPath) {
        $exePath = $fallbackPath
        $Configuration = $fallbackConfig
    }
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Fast Launch (NoBuild)" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

if (-not (Test-Path $exePath)) {
    Write-Host "`n[AVISO] No se encontro el ejecutable compilado en:" -ForegroundColor Yellow
    Write-Host "  $exePath" -ForegroundColor White
    Write-Host "`nPor favor, compila la solucion al menos una vez ejecutando: .\run.ps1" -ForegroundColor Gray
    exit 1
}

Write-Host "`n[OK] Iniciando FileFlow Studio ($Configuration)..." -ForegroundColor Green

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
