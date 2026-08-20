Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Build & Run Script   " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "`nCompilando la solución FileFlow.slnx..." -ForegroundColor Yellow
dotnet build FileFlow.slnx -c Debug

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n[ERROR] La compilación falló. Revisa los errores." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`nCompilación exitosa. Ejecutando FileFlow Studio..." -ForegroundColor Green
Start-Process "FileFlow.App\bin\Debug\net9.0-windows\FileFlow.App.exe"
