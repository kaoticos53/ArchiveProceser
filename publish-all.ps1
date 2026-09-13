param(
    [string]$Configuration = "Release",
    [switch]$WindowsOnly,
    [switch]$LinuxOnly,
    [switch]$FrameworkDependent,
    [switch]$KeepDebugPdb,
    [switch]$Clean = $true
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

$modeName = if ($FrameworkDependent) { "Framework-Dependent (requiere .NET 9 en el sistema)" } else { "Self-Contained (Autocontenido)" }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Publicación Multiplataforma (.NET 9)  " -ForegroundColor Cyan
Write-Host "  Modo: $modeName" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$distDir = Join-Path $scriptDir "dist"
if ($Clean -and (Test-Path $distDir)) {
    Write-Host "Limpiando directorio de distribución anterior ($distDir)..." -ForegroundColor Gray
    Remove-Item -Recurse -Force $distDir -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

$appProject = Join-Path $scriptDir "FileFlow.App\FileFlow.App.csproj"
$coreProject = Join-Path $scriptDir "FileFlow.Core\FileFlow.Core.csproj"

$isSelfContained = -not $FrameworkDependent
$scArg = if ($isSelfContained) { "--self-contained true" } else { "--self-contained false" }
$pdbArg = if ($KeepDebugPdb) { "" } else { "-p:DebugType=none -p:DebugSymbols=false" }

# --- 1. Publicación para Windows x64 ---
if (-not $LinuxOnly) {
    $winDist = Join-Path $distDir "windows-x64"
    Write-Host "`n📦 Publicando FileFlow Studio para Windows x64 ($Configuration)..." -ForegroundColor Yellow
    
    $singleFileArg = if ($isSelfContained) {
        "-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true"
    } else {
        "-p:PublishSingleFile=true"
    }
    
    $winCmd = "dotnet publish `"$appProject`" -c $Configuration -r win-x64 $scArg $singleFileArg $pdbArg -o `"$winDist`""
    Write-Host "Ejecutando: $winCmd" -ForegroundColor DarkGray
    Invoke-Expression $winCmd
    if ($LASTEXITCODE -eq 0) {
        # Limpieza de archivos de desarrollo no requeridos (.pdb, .lib)
        if (-not $KeepDebugPdb) {
            Get-ChildItem -Path $winDist -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
            Get-ChildItem -Path $winDist -Filter "*.lib" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
        }
        $winFilesCount = (Get-ChildItem -Path $winDist -Recurse -File).Count
        Write-Host "✅ Binarios de Windows generados con éxito en: $winDist ($winFilesCount archivos)" -ForegroundColor Green
    } else {
        Write-Host "❌ Fallo al compilar versión de Windows." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# --- 2. Publicación de Motor y Plugins para Linux x64 ---
if (-not $WindowsOnly) {
    $linuxDist = Join-Path $distDir "linux-x64"
    Write-Host "`n📦 Publicando componentes del Motor y Plugins de FileFlow para Linux x64 ($Configuration)..." -ForegroundColor Yellow
    
    $linuxCmd = "dotnet publish `"$coreProject`" -c $Configuration -r linux-x64 $scArg $pdbArg -o `"$linuxDist/engine`""
    Write-Host "Ejecutando: $linuxCmd" -ForegroundColor DarkGray
    Invoke-Expression $linuxCmd
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Fallo al compilar el motor para Linux." -ForegroundColor Red
        exit $LASTEXITCODE
    }

    # Publicar plugins en carpeta Plugins/ de forma limpia (sin duplicar el BCL runtime en cada plugin)
    $pluginsLinuxTarget = Join-Path $linuxDist "Plugins"
    New-Item -ItemType Directory -Path $pluginsLinuxTarget -Force | Out-Null
    
    $pluginProjects = Get-ChildItem -Path (Join-Path $scriptDir "FileFlow.Plugin.*") -Filter "*.csproj" -Recurse
    foreach ($plugin in $pluginProjects) {
        Write-Host "  -> Publicando plugin $($plugin.BaseName)..." -ForegroundColor DarkGray
        dotnet publish $plugin.FullName -c $Configuration -r linux-x64 --self-contained false $pdbArg -o $pluginsLinuxTarget | Out-Null
    }

    # Limpieza de archivos .pdb en Linux si no se solicitaron
    if (-not $KeepDebugPdb) {
        Get-ChildItem -Path $linuxDist -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
    }

    $linuxFilesCount = (Get-ChildItem -Path $linuxDist -Recurse -File).Count
    Write-Host "✅ Binarios de Linux generados con éxito en: $linuxDist ($linuxFilesCount archivos)" -ForegroundColor Green
}

$totalFiles = (Get-ChildItem -Path $distDir -Recurse -File).Count
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  Proceso de publicación completado con éxito!             " -ForegroundColor Green
Write-Host "  Salida generada en: $distDir ($totalFiles archivos totales) " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
