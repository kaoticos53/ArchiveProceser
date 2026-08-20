# FileFlow Studio - Coverage Report Generator Script
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Code Coverage Report " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# 1. Check & Install ReportGenerator Tool if missing
if (-not (Get-Command "reportgenerator" -ErrorAction SilentlyContinue)) {
    Write-Host "Instalando herramienta ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-reportgenerator-globaltool
}

# 2. Run Tests & Collect Cobertura XML Coverage
Write-Host "Ejecutando pruebas y recopilando cobertura..." -ForegroundColor Green
dotnet test FileFlow.slnx --collect:"XPlat Code Coverage"

# 3. Generate HTML Dashboard
$coverageFile = Get-ChildItem -Path "FileFlow.Tests/TestResults" -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1

if ($coverageFile) {
    Write-Host "Generando informe gráfico HTML en ./coverage-report ..." -ForegroundColor Green
    reportgenerator -reports:"$($coverageFile.FullName)" -targetdir:"coverage-report" -reporttypes:"Html"

    $htmlIndex = Join-Path (Get-Location) "coverage-report\index.html"
    Write-Host "¡Informe generado exitosamente!" -ForegroundColor Green
    Write-Host "Abriendo informe en el navegador: $htmlIndex" -ForegroundColor Cyan
    Start-Process $htmlIndex
} else {
    Write-Host "No se encontró ningún archivo coverage.cobertura.xml." -ForegroundColor Red
}
