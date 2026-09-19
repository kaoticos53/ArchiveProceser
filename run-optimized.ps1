param (
    [switch]$Rebuild,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

$optimizedDir = Join-Path $scriptDir "bin\optimized"
$exePath = Join-Path $optimizedDir "FileFlow.App.exe"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Optimized Launcher   " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

if ($Rebuild -or (-not (Test-Path $exePath))) {
    Write-Host "`nGenerando binarios optimizados ReadyToRun..." -ForegroundColor Yellow
    & (Join-Path $scriptDir "publish-optimized.ps1")
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n[ERROR] No se pudo generar la version optimizada." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path $exePath)) {
    Write-Host "`n[ERROR] No se encontro el ejecutable optimizado en '$exePath'." -ForegroundColor Red
    exit 1
}

Write-Host "`nIniciando FileFlow Studio (Optimized ReadyToRun)..." -ForegroundColor Green

if ($AppArgs -and $AppArgs.Count -gt 0) {
    Start-Process -FilePath $exePath -ArgumentList $AppArgs -WorkingDirectory $optimizedDir
} else {
    Start-Process -FilePath $exePath -WorkingDirectory $optimizedDir
}
