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
    Write-Host ("Limpiando directorio de distribución anterior ({0})..." -f $distDir) -ForegroundColor Gray
    Remove-Item -Recurse -Force $distDir -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

$appProject = Join-Path $scriptDir "FileFlow.App\FileFlow.App.csproj"
$coreProject = Join-Path $scriptDir "FileFlow.Core\FileFlow.Core.csproj"
$sdkConfigDir = Join-Path $scriptDir "FileFlow.Sdk\Config"

$isSelfContained = -not $FrameworkDependent
$scBoolStr = $isSelfContained.ToString().ToLower()

$pdbFlags = if ($KeepDebugPdb) { @() } else { @("-p:DebugType=none", "-p:DebugSymbols=false") }

# --- 1. Publicación para Windows x64 ---
if (-not $LinuxOnly) {
    $winDist = Join-Path $distDir "windows-x64"
    Write-Host ("`n📦 Publicando FileFlow Studio para Windows x64 ({0})..." -f $Configuration) -ForegroundColor Yellow
    
    $winArgs = @(
        "publish", $appProject,
        "-c", $Configuration,
        "-r", "win-x64",
        "--self-contained", $scBoolStr,
        "-o", $winDist,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true"
    ) + $pdbFlags
    
    & dotnet @winArgs
    if ($LASTEXITCODE -eq 0) {
        # Asegurar Config/ en Windows
        $winConfigDest = Join-Path $winDist "Config"
        if (Test-Path $sdkConfigDir) {
            if (-not (Test-Path $winConfigDest)) { New-Item -ItemType Directory -Path $winConfigDest -Force | Out-Null }
            Copy-Item -Path "$sdkConfigDir\*" -Destination $winConfigDest -Recurse -Force
        }

        if (-not $KeepDebugPdb) {
            Get-ChildItem -Path $winDist -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
            Get-ChildItem -Path $winDist -Filter "*.lib" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
        }
        $winFilesCount = (Get-ChildItem -Path $winDist -Recurse -File).Count
        Write-Host ("✅ Binarios de Windows generados con éxito en: {0} ({1} archivos)" -f $winDist, $winFilesCount) -ForegroundColor Green
    } else {
        Write-Host "❌ Fallo al compilar versión de Windows." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# --- 2. Publicación de Aplicación, Motor y Plugins para Linux x64 ---
if (-not $WindowsOnly) {
    $linuxDist = Join-Path $distDir "linux-x64"
    Write-Host ("`n📦 Publicando FileFlow Studio y Plugins para Linux x64 ({0})..." -f $Configuration) -ForegroundColor Yellow
    
    $linuxArgs = @(
        "publish", $appProject,
        "-c", $Configuration,
        "-r", "linux-x64",
        "--self-contained", $scBoolStr,
        "-o", $linuxDist
    ) + $pdbFlags
    
    & dotnet @linuxArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Fallo al compilar la aplicación para Linux." -ForegroundColor Red
        exit $LASTEXITCODE
    }

    # Copiar Config/ global a Linux
    $linuxConfigDest = Join-Path $linuxDist "Config"
    if (Test-Path $sdkConfigDir) {
        New-Item -ItemType Directory -Path $linuxConfigDest -Force | Out-Null
        Copy-Item -Path "$sdkConfigDir\*" -Destination $linuxConfigDest -Recurse -Force
    }

    # Publicar cada plugin en su carpeta dedicada Plugins/{PluginName}/
    $pluginsLinuxTarget = Join-Path $linuxDist "Plugins"
    New-Item -ItemType Directory -Path $pluginsLinuxTarget -Force | Out-Null
    
    $pluginProjects = Get-ChildItem -Path (Join-Path $scriptDir "FileFlow.Plugin.*") -Filter "*.csproj" -Recurse
    foreach ($plugin in $pluginProjects) {
        $pluginName = $plugin.BaseName
        $pluginDest = Join-Path $pluginsLinuxTarget $pluginName
        New-Item -ItemType Directory -Path $pluginDest -Force | Out-Null
        
        Write-Host "  -> Publicando plugin $pluginName..." -ForegroundColor DarkGray
        $pluginArgs = @(
            "publish", $plugin.FullName,
            "-c", $Configuration,
            "-r", "linux-x64",
            "--self-contained", "false",
            "-o", $pluginDest
        ) + $pdbFlags
        
        & dotnet @pluginArgs | Out-Null

        # Si el plugin contiene una carpeta Config/, asegurar copia
        $pluginSrcConfig = Join-Path (Split-Path -Parent $plugin.FullName) "Config"
        if (Test-Path $pluginSrcConfig) {
            $pluginDestConfig = Join-Path $pluginDest "Config"
            New-Item -ItemType Directory -Path $pluginDestConfig -Force | Out-Null
            Copy-Item -Path "$pluginSrcConfig\*" -Destination $pluginDestConfig -Recurse -Force
        }
    }

    # Copiar scripts y assets para Linux
    $launcherSrc = Join-Path $scriptDir "installer\linux\fileflow.sh"
    if (Test-Path $launcherSrc) {
        Copy-Item $launcherSrc (Join-Path $linuxDist "fileflow.sh") -Force
    }

    $iconPng = Join-Path $scriptDir "assets\FileFlow.png"
    if (Test-Path $iconPng) {
        Copy-Item $iconPng (Join-Path $linuxDist "fileflow.png") -Force
    }

    # Limpieza de archivos .pdb en Linux si no se solicitaron
    if (-not $KeepDebugPdb) {
        Get-ChildItem -Path $linuxDist -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
    }

    $linuxFilesCount = (Get-ChildItem -Path $linuxDist -Recurse -File).Count
    Write-Host ("✅ Binarios de Linux generados con éxito en: {0} ({1} archivos)" -f $linuxDist, $linuxFilesCount) -ForegroundColor Green
}

$totalFiles = (Get-ChildItem -Path $distDir -Recurse -File).Count
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  Proceso de publicación completado con éxito!             " -ForegroundColor Green
Write-Host ("  Salida generada en: {0} ({1} archivos totales) " -f $distDir, $totalFiles) -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
