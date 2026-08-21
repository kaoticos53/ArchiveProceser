# =========================================================
#  FileFlow Studio - Code Coverage Report Generator Script
# =========================================================

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Code Coverage Report " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# 0. Stop any running instance of FileFlow.App to prevent file locks on DLLs
Get-Process -Name "FileFlow.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# 1. Check & Install ReportGenerator Tool if missing
if (-not (Get-Command "reportgenerator" -ErrorAction SilentlyContinue)) {
    Write-Host "Instalando herramienta ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-reportgenerator-globaltool
}

# 2. Clean previous TestResults if exist
if (Test-Path "TestResults") {
    Remove-Item "TestResults" -Recurse -Force -ErrorAction SilentlyContinue
}

# 3. Run Tests & Collect Cobertura XML Coverage
Write-Host "`nEjecutando pruebas y recopilando cobertura de código..." -ForegroundColor Green
dotnet test FileFlow.Tests/FileFlow.Tests.csproj --collect:"XPlat Code Coverage" --results-directory ./TestResults

# 4. Generate HTML Dashboard
$coverageFile = Get-ChildItem -Path "TestResults" -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1

if ($coverageFile) {
    Write-Host "`nGenerando informe gráfico HTML en ./coverage-report ..." -ForegroundColor Green
    reportgenerator -reports:"$($coverageFile.FullName)" -targetdir:"coverage-report" -reporttypes:"Html"

    $htmlIndex = Join-Path (Get-Location) "coverage-report\index.html"
    Write-Host "¡Informe de Cobertura generado exitosamente!" -ForegroundColor Green
    Write-Host "Abriendo informe en el navegador: $htmlIndex" -ForegroundColor Cyan
    Start-Process $htmlIndex
} else {
    Write-Host "`n[ERROR] No se encontró ningún archivo coverage.cobertura.xml." -ForegroundColor Red
}
