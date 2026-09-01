<#
.SYNOPSIS
	Genera la versión portable de FileFlow Studio comprimida en formato ZIP.

.DESCRIPTION
	1. Ejecuta installer/publish.ps1 para generar los binarios de FileFlow.App y plugins.
	2. Crea el archivo marcador 'portable.dat' y la carpeta 'data/' preconfigurada.
	3. Incluye la carpeta 'Config/' con los presets de fábrica y la documentación/manual PDF si existe.
	4. Comprime todo en installer/output/FileFlowStudio-v<Version>-Portable-<Runtime>.zip.

.PARAMETER Version
	Versión a incluir en el nombre del paquete zip generado. Por defecto: 1.0.0.

.PARAMETER Runtime
	RID de destino. Por defecto: win-x64.

.PARAMETER SelfContained
	Si es $true (por defecto), incluye el runtime de .NET 9.

.EXAMPLE
	./installer/build-portable.ps1
	./installer/build-portable.ps1 -Version "1.2.0" -Runtime "win-x64"
#>
param(
	[string]$Version = "1.0.0",
	[string]$Runtime = "win-x64",
	$SelfContained = $true
)

$ErrorActionPreference = "Stop"
$isSelfContained = if ($SelfContained -is [bool]) { $SelfContained } else { [System.Convert]::ToBoolean($SelfContained) }

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $PSScriptRoot "publish\$Runtime"
$outputDir = Join-Path $PSScriptRoot "output"
$portableTempDir = Join-Path $PSScriptRoot "publish\FileFlowStudio-Portable"
$zipName = "FileFlowStudio-v$Version-Portable-$Runtime.zip"
$zipPath = Join-Path $outputDir $zipName

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " FileFlow Studio - Generador Portable" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Versión:        $Version" -ForegroundColor Gray
Write-Host "Runtime:        $Runtime" -ForegroundColor Gray
Write-Host "SelfContained:  $isSelfContained" -ForegroundColor Gray
Write-Host "Destino ZIP:    $zipPath" -ForegroundColor Gray
Write-Host ""

# 1. Compilar y publicar binarios
Write-Host "==> [Paso 1/4] Publicando binarios con publish.ps1..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "publish.ps1") -Configuration "Release" -Runtime $Runtime -SelfContained $isSelfContained -SingleFile $true

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $publishDir)) {
	throw "La publicación de binarios falló."
}

# 2. Preparar carpeta temporal portable
Write-Host "==> [Paso 2/4] Estructurando directorio portable..." -ForegroundColor Cyan
if (Test-Path $portableTempDir) {
	Remove-Item $portableTempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $portableTempDir -Force | Out-Null

# Copiar todos los ficheros publicados
Copy-Item -Path "$publishDir\*" -Destination $portableTempDir -Recurse -Force

# Crear archivo marcador portable.dat
$portableDatPath = Join-Path $portableTempDir "portable.dat"
Set-Content -Path $portableDatPath -Value "FileFlow Studio Portable Mode Active" -Encoding UTF8

# Crear subcarpeta data/ con estructura estándar
$dataDir = Join-Path $portableTempDir "data"
$subDirs = @("config", "themes", "presets", "samples", "scripts", "logs", "tools")
foreach ($sub in $subDirs) {
	New-Item -ItemType Directory -Path (Join-Path $dataDir $sub) -Force | Out-Null
}

# Incluir manuales en PDF si existen
$pdfManuals = Get-ChildItem -Path (Join-Path $repoRoot "docs") -Filter "*.pdf" -ErrorAction SilentlyContinue
if ($pdfManuals -and $pdfManuals.Count -gt 0) {
	$docsDest = Join-Path $portableTempDir "docs"
	New-Item -ItemType Directory -Path $docsDest -Force | Out-Null
	foreach ($pdf in $pdfManuals) {
		Copy-Item -Path $pdf.FullName -Destination (Join-Path $docsDest $pdf.Name) -Force
	}
}

# 3. Crear ZIP final
Write-Host "==> [Paso 3/4] Comprimiendo paquete ZIP portable..." -ForegroundColor Cyan
if (-not (Test-Path $outputDir)) {
	New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}
if (Test-Path $zipPath) {
	Remove-Item $zipPath -Force
}

Compress-Archive -Path "$portableTempDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

# 4. Resumen
Write-Host "==> [Paso 4/4] Limpiando temporales..." -ForegroundColor Cyan
Remove-Item $portableTempDir -Recurse -Force

$zipItem = Get-Item $zipPath
$zipSizeMb = [math]::Round($zipItem.Length / 1MB, 2)

Write-Host ""
Write-Host "=========================================" -ForegroundColor Green
Write-Host " ¡Paquete Portable Creado con Éxito!" -ForegroundColor Green
Write-Host " Archivo: $zipPath ($zipSizeMb MB)" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
