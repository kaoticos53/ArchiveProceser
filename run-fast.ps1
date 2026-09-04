param (
    [string]$Configuration = "Debug",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

$exePath = Join-Path $scriptDir "FileFlow.App\bin\$Configuration\net9.0-windows\FileFlow.App.exe"

# Si no se encuentra en la configuracion solicitada, probar la otra configuracion (Debug/Release)
if (-not (Test-Path $exePath)) {
    $fallbackConfig = if ($Configuration -eq "Debug") { "Release" } else { "Debug" }
    $fallbackPath = Join-Path $scriptDir "FileFlow.App\bin\$fallbackConfig\net9.0-windows\FileFlow.App.exe"
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

if ($AppArgs -and $AppArgs.Count -gt 0) {
    Start-Process -FilePath $exePath -ArgumentList $AppArgs -WorkingDirectory (Split-Path -Parent $exePath)
} else {
    Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath)
}
