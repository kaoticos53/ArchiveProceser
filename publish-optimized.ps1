param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Native Optimizer     " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "`nPublicando FileFlow Studio con precompilacion nativa ReadyToRun (R2R)..." -ForegroundColor Yellow
Write-Host "Configuracion: $Configuration | Runtime: $Runtime" -ForegroundColor Gray

$outputDir = Join-Path $scriptDir "bin\optimized"
$projectPath = Join-Path $scriptDir "FileFlow.App\FileFlow.App.csproj"

$sw = [System.Diagnostics.Stopwatch]::StartNew()

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -p:PublishReadyToRun=true `
    -p:PublishReadyToRunComposite=false `
    -o $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n[ERROR] La publicacion optimizada fallo." -ForegroundColor Red
    exit $LASTEXITCODE
}

$sw.Stop()
$elapsedSec = [Math]::Round($sw.Elapsed.TotalSeconds, 1)

Write-Host "`n[OK] Publicacion optimizada completada en ${elapsedSec}s." -ForegroundColor Green
Write-Host "Ubicacion del ejecutable: $(Join-Path $outputDir 'FileFlow.App.exe')" -ForegroundColor Cyan
Write-Host "Para ejecutar la version optimizada, usa: .\run-optimized.ps1" -ForegroundColor Yellow
