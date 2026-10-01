param(
    [string]$Configuration = "Release",
    [switch]$WindowsOnly,
    [switch]$LinuxOnly,
    [switch]$MacOnly,
    [switch]$WebOnly,
    [switch]$IncludeMac,
    [switch]$IncludeWeb,
    [switch]$FrameworkDependent,
    [switch]$KeepDebugPdb,
    [switch]$Clean = $true
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

$modeName = if ($FrameworkDependent) { "Framework-Dependent (requiere .NET 10 en el sistema)" } else { "Self-Contained (Autocontenido)" }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Publicación Multiplataforma (.NET 10) " -ForegroundColor Cyan
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
$sdkConfigDir = Join-Path $scriptDir "FileFlow.Sdk\Config"

$isSelfContained = -not $FrameworkDependent
$scBoolStr = $isSelfContained.ToString().ToLower()

$pdbFlags = if ($KeepDebugPdb) { @() } else { @("-p:DebugType=none", "-p:DebugSymbols=false") }

$buildWindows = (-not $LinuxOnly) -and (-not $MacOnly) -and (-not $WebOnly)
$buildLinux   = (-not $WindowsOnly) -and (-not $MacOnly) -and (-not $WebOnly)
$buildMac     = $MacOnly -or $IncludeMac
$buildWeb     = $WebOnly -or $IncludeWeb

# --- 1. Publicación para Windows x64 ---
if ($buildWindows) {
    $winDist = Join-Path $distDir "windows-x64"
    Write-Host "`n📦 Publicando FileFlow Studio para Windows x64 ($Configuration)..." -ForegroundColor Yellow
    
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
        Write-Host "✅ Binarios de Windows generados con éxito en: $winDist ($winFilesCount archivos)" -ForegroundColor Green
    } else {
        Write-Host "❌ Fallo al compilar versión de Windows." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# --- 2. Publicación para Linux x64 ---
if ($buildLinux) {
    $linuxDist = Join-Path $distDir "linux-x64"
    Write-Host "`n📦 Publicando FileFlow Studio (GUI y Motor) para Linux x64 ($Configuration)..." -ForegroundColor Yellow
    
    $linuxArgs = @(
        "publish", $appProject,
        "-c", $Configuration,
        "-r", "linux-x64",
        "--self-contained", $scBoolStr,
        "-o", $linuxDist,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true"
    ) + $pdbFlags
    
    & dotnet @linuxArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Fallo al compilar FileFlow.App para Linux." -ForegroundColor Red
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
    Write-Host "✅ Binarios de Linux generados con éxito en: $linuxDist ($linuxFilesCount archivos)" -ForegroundColor Green
}

# --- 3. Publicación para macOS (Apple Silicon y x64) ---
if ($buildMac) {
    foreach ($macRid in @("osx-arm64", "osx-x64")) {
        $macDist = Join-Path $distDir $macRid
        Write-Host "`n📦 Publicando FileFlow Studio para macOS ($macRid, $Configuration)..." -ForegroundColor Yellow
        
        $macArgs = @(
            "publish", $appProject,
            "-c", $Configuration,
            "-r", $macRid,
            "--self-contained", $scBoolStr,
            "-o", $macDist,
            "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true",
            "-p:EnableCompressionInSingleFile=true"
        ) + $pdbFlags
        
        & dotnet @macArgs
        if ($LASTEXITCODE -eq 0) {
            $macConfigDest = Join-Path $macDist "Config"
            if (Test-Path $sdkConfigDir) {
                New-Item -ItemType Directory -Path $macConfigDest -Force | Out-Null
                Copy-Item -Path "$sdkConfigDir\*" -Destination $macConfigDest -Recurse -Force
            }
            if (-not $KeepDebugPdb) {
                Get-ChildItem -Path $macDist -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
            }
            $macCount = (Get-ChildItem -Path $macDist -Recurse -File).Count
            Write-Host "✅ Binarios de macOS ($macRid) generados con éxito en: $macDist ($macCount archivos)" -ForegroundColor Green
        } else {
            Write-Warning "Fallo al compilar versión de macOS ($macRid)."
        }
    }
}

# --- 4. Publicación para Web (WebAssembly / Avalonia.Browser) ---
if ($buildWeb) {
    $webDist = Join-Path $distDir "web-wasm"
    Write-Host "`n📦 Publicando FileFlow Studio para Web (WebAssembly / browser-wasm, $Configuration)..." -ForegroundColor Yellow
    
    $browserSln = Join-Path $scriptDir "FileFlow.Browser.slnx"
    $webArgs = @(
        "publish", $browserSln,
        "-c", $Configuration
    )
    
    & dotnet @webArgs
    $wasmPublishDir = Join-Path $scriptDir "FileFlow.App.Browser\bin\$Configuration\net10.0\browser-wasm\publish\wwwroot"
    if (Test-Path $wasmPublishDir) {
        New-Item -ItemType Directory -Path $webDist -Force | Out-Null
        Copy-Item -Path "$wasmPublishDir\*" -Destination $webDist -Recurse -Force
        $webFilesCount = (Get-ChildItem -Path $webDist -Recurse -File).Count
        Write-Host "✅ Artefactos WebAssembly generados con éxito en: $webDist ($webFilesCount archivos)" -ForegroundColor Green
    } else {
        Write-Warning "No se encontró el directorio de salida WebAssembly en '$wasmPublishDir'."
    }
}

$totalFiles = (Get-ChildItem -Path $distDir -Recurse -File).Count
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  Proceso de publicación completado con éxito!             " -ForegroundColor Green
Write-Host "  Salida generada en: $distDir ($totalFiles archivos totales) " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
