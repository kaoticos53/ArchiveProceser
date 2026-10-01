<#
.SYNOPSIS
    Compila y empaqueta la distribución WebAssembly (Wasm) de FileFlow Studio.

.DESCRIPTION
    1. Ejecuta dotnet publish sobre FileFlow.Browser.slnx en configuración Release.
    2. Recoge los archivos estáticos generados en wwwroot (dotnet.wasm, runtime JS, HTML y assets precomprimidos con Brotli).
    3. Empaqueta el resultado en un archivo ZIP portable 'FileFlowStudio-Web-v<Version>.zip' listo para desplegar en cualquier servidor HTTP o CDN.

.PARAMETER Version
    Versión a reflejar en el paquete comprimido (por defecto: 1.0.0).

.PARAMETER Configuration
    Configuración de compilación (por defecto: Release).
#>
param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }
$repoRoot = Split-Path -Parent $scriptDir

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Empaquetador WebAssembly (Browser)   " -ForegroundColor Cyan
Write-Host "  Versión: $Version | Config: $Configuration             " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$outputDir = Join-Path $scriptDir "output"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$browserProject = Join-Path $repoRoot "FileFlow.App.Browser\FileFlow.App.Browser.csproj"
Write-Host "`n📦 Compilando y publicando FileFlow WebAssembly..." -ForegroundColor Yellow

& dotnet publish $browserProject -c $Configuration -m:1
if ($LASTEXITCODE -ne 0) {
    throw "La compilación de FileFlow.App.Browser falló con código $LASTEXITCODE"
}

$candidatePaths = @(
    (Join-Path $repoRoot "FileFlow.App.Browser\bin\$Configuration\net10.0\publish\wwwroot"),
    (Join-Path $repoRoot "FileFlow.App.Browser\bin\$Configuration\net10.0\browser-wasm\publish\wwwroot")
)

$wasmPublishDir = $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $wasmPublishDir) {
    throw "No se encontró el directorio de salida wwwroot en ninguna de las rutas esperadas."
}

$webOutputDir = Join-Path $outputDir "web"
if (Test-Path $webOutputDir) { Remove-Item -Recurse -Force $webOutputDir }
New-Item -ItemType Directory -Path $webOutputDir -Force | Out-Null

Copy-Item -Path "$wasmPublishDir\*" -Destination $webOutputDir -Recurse -Force

$zipName = "FileFlowStudio-Web-v$Version.zip"
$zipOutput = Join-Path $outputDir $zipName
if (Test-Path $zipOutput) { Remove-Item $zipOutput -Force }

Write-Host "  -> Generando archivo comprimido: $zipName..." -ForegroundColor DarkGray
Compress-Archive -Path "$webOutputDir\*" -DestinationPath $zipOutput -Force

$zipSize = [math]::Round(((Get-Item $zipOutput).Length / 1MB), 2)
Write-Host "  [OK] Paquete WebAssembly generado: $zipName ($zipSize MB)" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
