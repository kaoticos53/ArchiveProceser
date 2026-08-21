# =========================================================
#   FileFlow Studio - Test Runner Automation Script (PS1)
# =========================================================

param(
    [string]$Mode = "all",           # Options: "all", "unit", "integration", "performance", "coverage"
    [switch]$Watch = $false,         # Hot-reload test runner mode
    [switch]$Help = $false
)

if ($Help) {
    Write-Host "Uso de test.ps1:" -ForegroundColor Cyan
    Write-Host "  .\test.ps1                     Executa todos los tests" -ForegroundColor Yellow
    Write-Host "  .\test.ps1 -Mode unit          Ejecuta solo tests unitarios" -ForegroundColor Yellow
    Write-Host "  .\test.ps1 -Mode integration   Ejecuta solo tests de integración" -ForegroundColor Yellow
    Write-Host "  .\test.ps1 -Mode performance   Ejecuta solo tests de estrés/rendimiento" -ForegroundColor Yellow
    Write-Host "  .\test.ps1 -Mode coverage      Ejecuta cobertura y abre informe HTML" -ForegroundColor Yellow
    Write-Host "  .\test.ps1 -Watch              Ejecuta los tests en modo vigía (hot reload)" -ForegroundColor Yellow
    exit 0
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "   FileFlow Studio - Suite de Pruebas    " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# 0. Cerrar instancias de FileFlow.App en ejecución para evitar bloqueos de archivos DLL
Get-Process -Name "FileFlow.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$testProject = "FileFlow.Tests/FileFlow.Tests.csproj"
$sw = [System.Diagnostics.Stopwatch]::StartNew()

if ($Watch) {
    Write-Host "`nIniciando corredor de pruebas en modo Vigía (Watch)..." -ForegroundColor Yellow
    dotnet watch test $testProject --configuration Debug
    exit $LASTEXITCODE
}

switch ($Mode.ToLower()) {
    "unit" {
        Write-Host "`n[1/1] Ejecutando Pruebas Unitarias..." -ForegroundColor Yellow
        dotnet test $testProject --configuration Debug --filter "FullyQualifiedName~Unit"
    }
    "integration" {
        Write-Host "`n[1/1] Ejecutando Pruebas de Integración..." -ForegroundColor Yellow
        dotnet test $testProject --configuration Debug --filter "FullyQualifiedName~Integration"
    }
    "performance" {
        Write-Host "`n[1/1] Ejecutando Pruebas de Estrés y Rendimiento..." -ForegroundColor Yellow
        dotnet test $testProject --configuration Debug --filter "FullyQualifiedName~Performance"
    }
    "coverage" {
        Write-Host "`nEjecutando Cobertura de Código..." -ForegroundColor Yellow
        .\coverage.ps1
        exit 0
    }
    default {
        Write-Host "`nEjecutando la Suite Completa de Pruebas (Unit + Integration + Performance)..." -ForegroundColor Green
        dotnet test $testProject --configuration Debug --verbosity normal
    }
}

$sw.Stop()
$elapsed = [math]::Round($sw.Elapsed.TotalSeconds, 2)

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n=========================================" -ForegroundColor Green
    Write-Host " ¡TODAS LAS PRUEBAS PASARON EXITOSAMENTE! " -ForegroundColor Green
    Write-Host " Tiempo total de ejecución: $elapsed s" -ForegroundColor Cyan
    Write-Host "=========================================`n" -ForegroundColor Green
} else {
    Write-Host "`n=========================================" -ForegroundColor Red
    Write-Host " [FALLO] Ocurrieron errores en los tests. " -ForegroundColor Red
    Write-Host "=========================================`n" -ForegroundColor Red
    exit $LASTEXITCODE
}
