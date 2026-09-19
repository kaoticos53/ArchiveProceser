param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }
$repoRoot = Split-Path -Parent $scriptDir

$modeLabel = if ($FrameworkDependent) { "Framework-Dependent" } else { "Self-Contained" }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Generador de Instaladores para Linux  " -ForegroundColor Cyan
Write-Host "  Versión: $Version | Modo: $modeLabel | Arch: linux-x64  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$outputDir = Join-Path $scriptDir "output"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$workDir = Join-Path $scriptDir "temp_linux_build"
if (Test-Path $workDir) {
    Remove-Item -Recurse -Force $workDir -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $workDir -Force | Out-Null

$appPayloadDir = Join-Path $workDir "app"
New-Item -ItemType Directory -Path $appPayloadDir -Force | Out-Null

$scParam = if ($FrameworkDependent) { @("--self-contained", "false") } else { @("--self-contained", "true") }
$pdbParams = @("-p:DebugType=none", "-p:DebugSymbols=false")

# --- 1. Compilación y Publicación de Componentes Linux ---
Write-Host "`n[1/4] Compilando motor de FileFlow para Linux ($Configuration, $modeLabel)..." -ForegroundColor Yellow
$coreProject = Join-Path $repoRoot "FileFlow.Core\FileFlow.Core.csproj"
& dotnet publish $coreProject -c $Configuration -r linux-x64 @scParam @pdbParams -o "$appPayloadDir/engine" | Out-Null

# Copiar Config/ global de SDK a la app payload
$sdkConfigDir = Join-Path $repoRoot "FileFlow.Sdk\Config"
if (Test-Path $sdkConfigDir) {
    $appConfigDest = Join-Path $appPayloadDir "Config"
    $engineConfigDest = Join-Path $appPayloadDir "engine\Config"
    New-Item -ItemType Directory -Path $appConfigDest -Force | Out-Null
    Copy-Item -Path "$sdkConfigDir\*" -Destination $appConfigDest -Recurse -Force
    New-Item -ItemType Directory -Path $engineConfigDest -Force | Out-Null
    Copy-Item -Path "$sdkConfigDir\*" -Destination $engineConfigDest -Recurse -Force
}

Write-Host "[2/4] Compilando plugins para Linux ($Configuration)..." -ForegroundColor Yellow
$pluginsTarget = Join-Path $appPayloadDir "Plugins"
New-Item -ItemType Directory -Path $pluginsTarget -Force | Out-Null

$pluginProjects = Get-ChildItem -Path (Join-Path $repoRoot "FileFlow.Plugin.*") -Filter "*.csproj" -Recurse
foreach ($plugin in $pluginProjects) {
    $pluginName = $plugin.BaseName
    $pluginDest = Join-Path $pluginsTarget $pluginName
    New-Item -ItemType Directory -Path $pluginDest -Force | Out-Null
    
    Write-Host "  -> Publicando plugin $pluginName..." -ForegroundColor DarkGray
    & dotnet publish $plugin.FullName -c $Configuration -r linux-x64 --self-contained false @pdbParams -o $pluginDest | Out-Null

    # Copiar Config/ de cada plugin si existe
    $pluginSrcConfig = Join-Path (Split-Path -Parent $plugin.FullName) "Config"
    if (Test-Path $pluginSrcConfig) {
        $pluginDestConfig = Join-Path $pluginDest "Config"
        New-Item -ItemType Directory -Path $pluginDestConfig -Force | Out-Null
        Copy-Item -Path "$pluginSrcConfig\*" -Destination $pluginDestConfig -Recurse -Force
    }
}

# Limpieza de PDBs residuales
Get-ChildItem -Path $appPayloadDir -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

# Copiar script launcher a la carpeta app
$launcherSrc = Join-Path $scriptDir "linux\fileflow.sh"
if (Test-Path $launcherSrc) {
    Copy-Item $launcherSrc "$appPayloadDir/fileflow.sh" -Force
}

$iconPng = Join-Path $repoRoot "assets\FileFlow.png"

# Función segura para empaquetar con tar nativo de Windows o 7-Zip
function Compress-TarGz {
    param([string]$SourceDir, [string]$OutputFile)
    $systemTar = "C:\Windows\System32\tar.exe"
    if (Test-Path $systemTar) {
        & $systemTar -czf $OutputFile $SourceDir
    } else {
        tar -czf $OutputFile $SourceDir
    }
}

# --- 2. Crear Estructura de Bundle Universal (.tar.gz) ---
Write-Host "`n[3/4] Generando paquetes de distribución (Tar.gz, Debian Tree y AppDir)..." -ForegroundColor Yellow
$bundleDir = Join-Path $workDir "fileflow-linux-x64-$Version"
New-Item -ItemType Directory -Path $bundleDir -Force | Out-Null

Copy-Item -Recurse $appPayloadDir "$bundleDir/app" -Force
Copy-Item (Join-Path $scriptDir "linux\install.sh") "$bundleDir/install.sh" -Force
Copy-Item (Join-Path $scriptDir "linux\uninstall.sh") "$bundleDir/uninstall.sh" -Force
Copy-Item (Join-Path $scriptDir "linux\fileflow.desktop") "$bundleDir/fileflow.desktop" -Force

$assetsBundle = Join-Path $bundleDir "assets"
New-Item -ItemType Directory -Path $assetsBundle -Force | Out-Null
if (Test-Path $iconPng) {
    Copy-Item $iconPng "$assetsBundle/fileflow.png" -Force
}

$tarGzName = "fileflow-linux-x64-v$Version.tar.gz"
$tarGzOutput = Join-Path $outputDir $tarGzName
if (Test-Path $tarGzOutput) { Remove-Item $tarGzOutput -Force }

Write-Host "  -> Creando bundle universal: $tarGzName..." -ForegroundColor DarkGray
Push-Location $workDir
try {
    Compress-TarGz "fileflow-linux-x64-$Version" $tarGzOutput
} finally {
    Pop-Location
}

if (Test-Path $tarGzOutput) {
    $tarSize = [math]::Round(((Get-Item $tarGzOutput).Length / 1MB), 2)
    Write-Host "  [OK] Bundle universal generado: $tarGzName ($tarSize MB)" -ForegroundColor Green
}

# --- 3. Crear Estructura de Paquete Debian (.deb) ---
$debRoot = Join-Path $workDir "fileflow_${Version}_amd64"
New-Item -ItemType Directory -Path "$debRoot/DEBIAN" -Force | Out-Null
New-Item -ItemType Directory -Path "$debRoot/usr/bin" -Force | Out-Null
New-Item -ItemType Directory -Path "$debRoot/usr/lib/fileflow" -Force | Out-Null
New-Item -ItemType Directory -Path "$debRoot/usr/share/applications" -Force | Out-Null
New-Item -ItemType Directory -Path "$debRoot/usr/share/icons/hicolor/256x256/apps" -Force | Out-Null

Copy-Item -Recurse "$appPayloadDir/*" "$debRoot/usr/lib/fileflow/" -Force
Copy-Item (Join-Path $scriptDir "linux\fileflow.desktop") "$debRoot/usr/share/applications/fileflow.desktop" -Force
if (Test-Path $iconPng) {
    Copy-Item $iconPng "$debRoot/usr/share/icons/hicolor/256x256/apps/fileflow.png" -Force
}

$debLauncherContent = "#!/usr/bin/env bash`nexec /usr/lib/fileflow/fileflow.sh `"`$@`"`n"
$debLauncherFile = "$debRoot/usr/bin/fileflow"
[System.IO.File]::WriteAllText($debLauncherFile, $debLauncherContent, (New-Object System.Text.UTF8Encoding($false)))

$debControl = @"
Package: fileflow
Version: $Version
Section: utils
Priority: optional
Architecture: amd64
Maintainer: FileFlow Studio Team <info@fileflowstudio.com>
Description: FileFlow Studio - Advanced DAG-based batch processing, OCR, AI and automation platform.
 FileFlow Studio is a modular workflow automation suite built on .NET 9,
 featuring high-performance DAG pipeline execution, AI integrations,
 resilient archive processing and multimodal vision models.
"@
[System.IO.File]::WriteAllText("$debRoot/DEBIAN/control", $debControl, (New-Object System.Text.UTF8Encoding($false)))

$debPostInst = "#!/bin/sh`nset -e`nchmod +x /usr/lib/fileflow/fileflow.sh 2>/dev/null || true`nchmod +x /usr/bin/fileflow 2>/dev/null || true`nif command -v update-desktop-database >/dev/null 2>&1; then`n    update-desktop-database /usr/share/applications || true`nfi`nif command -v gtk-update-icon-cache >/dev/null 2>&1; then`n    gtk-update-icon-cache -f -t /usr/share/icons/hicolor || true`nfi`nexit 0`n"
[System.IO.File]::WriteAllText("$debRoot/DEBIAN/postinst", $debPostInst, (New-Object System.Text.UTF8Encoding($false)))

$debPostRm = "#!/bin/sh`nset -e`nif command -v update-desktop-database >/dev/null 2>&1; then`n    update-desktop-database /usr/share/applications || true`nfi`nexit 0`n"
[System.IO.File]::WriteAllText("$debRoot/DEBIAN/postrm", $debPostRm, (New-Object System.Text.UTF8Encoding($false)))

$debTarOutput = Join-Path $outputDir "fileflow_${Version}_amd64_deb_tree.tar.gz"
Push-Location $workDir
try {
    Compress-TarGz "fileflow_${Version}_amd64" $debTarOutput
} finally {
    Pop-Location
}

if (Test-Path $debTarOutput) {
    $debSize = [math]::Round(((Get-Item $debTarOutput).Length / 1MB), 2)
    Write-Host "  [OK] Paquete Debian (.deb tree) generado: fileflow_${Version}_amd64_deb_tree.tar.gz ($debSize MB)" -ForegroundColor Green
}

# Compilación directa de archivo .deb si dpkg-deb está disponible (nativamente o vía WSL)
$debPackageFile = Join-Path $outputDir "fileflow_${Version}_amd64.deb"
$wslAvailable = $false
try {
    $wslCheck = wsl uname 2>$null
    if ($wslCheck -like "*Linux*") { $wslAvailable = $true }
} catch {
    $wslAvailable = $false
}

if (Get-Command "dpkg-deb" -ErrorAction SilentlyContinue) {
    & dpkg-deb -b "$debRoot" "$debPackageFile" 2>$null
} elseif ($wslAvailable) {
    $wslDebRoot = "/mnt/" + $debRoot.Substring(0,1).ToLower() + $debRoot.Substring(2).Replace('\', '/')
    $wslDebOut = "/mnt/" + $debPackageFile.Substring(0,1).ToLower() + $debPackageFile.Substring(2).Replace('\', '/')
    & wsl dpkg-deb -b "$wslDebRoot" "$wslDebOut" 2>$null
}

if (Test-Path $debPackageFile) {
    $debPkgSize = [math]::Round(((Get-Item $debPackageFile).Length / 1MB), 2)
    Write-Host "  [OK] Paquete Debian (.deb) generado: fileflow_${Version}_amd64.deb ($debPkgSize MB)" -ForegroundColor Green
}

# --- 4. Crear Estructura y Paquete AppImage (.AppDir & .AppImage) ---
Write-Host "`n[4/4] Generando estructura y ejecutable AppImage..." -ForegroundColor Yellow
$appDir = Join-Path $workDir "FileFlow.AppDir"
New-Item -ItemType Directory -Path "$appDir/usr/bin" -Force | Out-Null
New-Item -ItemType Directory -Path "$appDir/usr/lib/fileflow" -Force | Out-Null
New-Item -ItemType Directory -Path "$appDir/usr/share/applications" -Force | Out-Null
New-Item -ItemType Directory -Path "$appDir/usr/share/icons/hicolor/256x256/apps" -Force | Out-Null

Copy-Item -Recurse "$appPayloadDir/*" "$appDir/usr/lib/fileflow/" -Force
Copy-Item (Join-Path $scriptDir "linux\AppRun") "$appDir/AppRun" -Force
Copy-Item (Join-Path $scriptDir "linux\fileflow.desktop") "$appDir/fileflow.desktop" -Force
Copy-Item (Join-Path $scriptDir "linux\fileflow.desktop") "$appDir/usr/share/applications/fileflow.desktop" -Force
Copy-Item (Join-Path $scriptDir "linux\build-appimage.sh") "$appDir/build-appimage.sh" -Force

if (Test-Path $iconPng) {
    Copy-Item $iconPng "$appDir/fileflow.png" -Force
    Copy-Item $iconPng "$appDir/.DirIcon" -Force
    Copy-Item $iconPng "$appDir/usr/share/icons/hicolor/256x256/apps/fileflow.png" -Force
}

$appDirTarOutput = Join-Path $outputDir "fileflow-linux-x64-v${Version}.AppDir.tar.gz"
Push-Location $workDir
try {
    Compress-TarGz "FileFlow.AppDir" $appDirTarOutput
} finally {
    Pop-Location
}

if (Test-Path $appDirTarOutput) {
    $appDirSize = [math]::Round(((Get-Item $appDirTarOutput).Length / 1MB), 2)
    Write-Host "  [OK] Bundle AppDir (.tar.gz) generado: fileflow-linux-x64-v${Version}.AppDir.tar.gz ($appDirSize MB)" -ForegroundColor Green
}

# Compilación directa de .AppImage ejecutable mediante WSL si está presente
$appImageFile = Join-Path $outputDir "FileFlow-v${Version}-x86_64.AppImage"
$flatpakOutFile = Join-Path $outputDir "FileFlow-v${Version}-x86_64.flatpak"

$isNativeLinux = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)
$wslAvailable = $false
if (-not $isNativeLinux) {
    try {
        $wslCheck = wsl uname 2>$null
        if ($wslCheck -like "*Linux*") { $wslAvailable = $true }
    } catch {
        $wslAvailable = $false
    }
}

if ($isNativeLinux) {
    Write-Host "  -> Compilando binario ejecutable .AppImage nativamente en Linux..." -ForegroundColor DarkGray
    $buildScript = Join-Path $scriptDir "linux/build-appimage.sh"
    & bash "$buildScript" "$appDir" "$appImageFile" 2>$null

    if (Test-Path $appImageFile) {
        $appImgSize = [math]::Round(((Get-Item $appImageFile).Length / 1MB), 2)
        Write-Host "  [OK] Ejecutable AppImage generado: FileFlow-v${Version}-x86_64.AppImage ($appImgSize MB)" -ForegroundColor Green
    }

    if (Get-Command "flatpak-builder" -ErrorAction SilentlyContinue) {
        Write-Host "  -> Compilando paquete .flatpak nativamente..." -ForegroundColor DarkGray
        $flatpakScript = Join-Path $scriptDir "linux/flatpak/build-flatpak.sh"
        & bash "$flatpakScript" "$Version" "$flatpakOutFile" 2>$null
        if (Test-Path $flatpakOutFile) {
            $flatpakSize = [math]::Round(((Get-Item $flatpakOutFile).Length / 1MB), 2)
            Write-Host "  [OK] Paquete Flatpak generado: FileFlow-v${Version}-x86_64.flatpak ($flatpakSize MB)" -ForegroundColor Green
        }
    }
} elseif ($wslAvailable) {
    Write-Host "  -> Compilando binario ejecutable .AppImage vía subsistema Linux (WSL)..." -ForegroundColor DarkGray
    $wslWorkDir = "/mnt/" + $workDir.Substring(0,1).ToLower() + $workDir.Substring(2).Replace('\', '/')
    $wslAppDir = "$wslWorkDir/FileFlow.AppDir"
    $wslOutFile = "/mnt/" + $appImageFile.Substring(0,1).ToLower() + $appImageFile.Substring(2).Replace('\', '/')
    $wslBuildScript = "/mnt/" + (Join-Path $scriptDir "linux\build-appimage.sh").Substring(0,1).ToLower() + (Join-Path $scriptDir "linux\build-appimage.sh").Substring(2).Replace('\', '/')

    & wsl bash "$wslBuildScript" "$wslAppDir" "$wslOutFile" 2>$null

    if (Test-Path $appImageFile) {
        $appImgSize = [math]::Round(((Get-Item $appImageFile).Length / 1MB), 2)
        Write-Host "  [OK] Ejecutable AppImage generado: FileFlow-v${Version}-x86_64.AppImage ($appImgSize MB)" -ForegroundColor Green
    }

    # Compilación de Flatpak (.flatpak) si flatpak-builder está instalado en WSL
    $wslFlatpakScript = "/mnt/" + (Join-Path $scriptDir "linux\flatpak\build-flatpak.sh").Substring(0,1).ToLower() + (Join-Path $scriptDir "linux\flatpak\build-flatpak.sh").Substring(2).Replace('\', '/')
    $wslFlatpakOut = "/mnt/" + $flatpakOutFile.Substring(0,1).ToLower() + $flatpakOutFile.Substring(2).Replace('\', '/')
    
    $checkFlatpak = wsl which flatpak-builder 2>$null
    if (-not [string]::IsNullOrWhiteSpace($checkFlatpak)) {
        Write-Host "  -> Compilando paquete .flatpak vía subsistema Linux (WSL)..." -ForegroundColor DarkGray
        & wsl bash "$wslFlatpakScript" "$Version" "$wslFlatpakOut" 2>$null
        if (Test-Path $flatpakOutFile) {
            $flatpakSize = [math]::Round(((Get-Item $flatpakOutFile).Length / 1MB), 2)
            Write-Host "  [OK] Paquete Flatpak generado: FileFlow-v${Version}-x86_64.flatpak ($flatpakSize MB)" -ForegroundColor Green
        }
    }
}

# Limpiar temporales de compilación
Remove-Item -Recurse -Force $workDir -ErrorAction SilentlyContinue

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  Instaladores y Paquetes de Linux generados con éxito!   " -ForegroundColor Green
Write-Host "  Directorio de salida: $outputDir                        " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
