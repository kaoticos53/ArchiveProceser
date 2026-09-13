<#
.SYNOPSIS
    Compila y empaqueta los instaladores y distribuibles tanto para Windows como para Linux.

.DESCRIPTION
    1. Ejecuta publish-all.ps1 para generar los binarios nativos de Windows (win-x64) y Linux (linux-x64).
    2. Ejecuta installer/build-installer.ps1 para compilar el instalador Inno Setup de Windows (si ISCC está disponible).
    3. Ejecuta installer/build-linux-installer.ps1 para empaquetar el bundle universal .tar.gz (install.sh), árbol Debian .deb y el ejecutable AppImage de Linux.

.PARAMETER Version
    Versión del producto (por defecto: 1.0.0).

.PARAMETER FrameworkDependent
    Si se especifica, compila en modo dependiente del framework .NET 9 (reduciendo el tamaño y número de ficheros).

.EXAMPLE
    ./installer/build-all.ps1 -Version "1.0.0"
    ./installer/build-all.ps1 -Version "1.0.0" -FrameworkDependent
#>
param(
    [string]$Version = "1.0.0",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"

$modeLabel = if ($FrameworkDependent) { "Framework-Dependent" } else { "Self-Contained" }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " 🚀 FileFlow Studio - Generador Universal de Instaladores " -ForegroundColor Cyan
Write-Host " Versión: $Version | Modo: $modeLabel" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$installerDir = $PSScriptRoot

# 1. Compilación cruzada Windows y Linux
Write-Host "`n[1/3] Publicando binarios para Windows y Linux..." -ForegroundColor Yellow
$pubParams = @{
    Configuration = "Release"
    FrameworkDependent = $FrameworkDependent
}
& (Join-Path $root "publish-all.ps1") @pubParams

# 2. Generación del instalador Linux (Tar.gz, Deb tree, AppDir y AppImage)
Write-Host "`n[2/3] Generando paquetes e instaladores para Linux..." -ForegroundColor Yellow
$linuxParams = @{
    Version = $Version
    FrameworkDependent = $FrameworkDependent
}
& (Join-Path $installerDir "build-linux-installer.ps1") @linuxParams

# 3. Generación del instalador Windows
Write-Host "`n[3/3] Generando instalador para Windows (Inno Setup)..." -ForegroundColor Yellow
try {
    $selfContained = -not $FrameworkDependent
    & (Join-Path $installerDir "build-installer.ps1") -Version $Version -SelfContained $selfContained
} catch {
    Write-Warning "No se pudo generar el instalador Inno Setup para Windows: $_"
    Write-Host "Los paquetes de Linux y los binarios de Windows en dist/ siguen estando listos." -ForegroundColor Yellow
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " ✅ Proceso completado. Artefactos listos en:" -ForegroundColor Green
Write-Host "    - Windows Binarios: dist/windows-x64/" -ForegroundColor White
Write-Host "    - Linux Binarios:   dist/linux-x64/" -ForegroundColor White
Write-Host "    - Instaladores:     installer/output/" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green
