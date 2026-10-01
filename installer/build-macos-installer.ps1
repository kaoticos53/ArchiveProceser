<#
.SYNOPSIS
    Genera el paquete de distribución para macOS (.app bundle, ZIP y DMG).

.DESCRIPTION
    1. Publica FileFlow.App para runtime osx-arm64 (Apple Silicon) y/o osx-x64 (Intel).
    2. Construye la estructura canónica del paquete de aplicación 'FileFlow Studio.app':
       - Contents/Info.plist
       - Contents/PkgInfo
       - Contents/MacOS/FileFlow.App
       - Contents/Resources/
    3. Empaqueta el resultado en formato ZIP portable y, si se ejecuta en macOS, genera una imagen .dmg con hdiutil.

.PARAMETER Version
    Versión del producto (por defecto: 1.0.0).

.PARAMETER Runtime
    RID de destino: 'osx-arm64', 'osx-x64' o 'both' (por defecto: 'both').

.PARAMETER Configuration
    Configuración de compilación (Debug o Release, por defecto: Release).
#>
param(
    [string]$Version = "1.0.0",
    [string]$Runtime = "both",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }
$repoRoot = Split-Path -Parent $scriptDir

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Generador de Paquetes para macOS      " -ForegroundColor Cyan
Write-Host "  Versión: $Version | Config: $Configuration | Runtime: $Runtime" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$outputDir = Join-Path $scriptDir "output"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$workDir = Join-Path $scriptDir "temp_macos_build"
if (Test-Path $workDir) {
    Remove-Item -Recurse -Force $workDir -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $workDir -Force | Out-Null

$appProject = Join-Path $repoRoot "FileFlow.App\FileFlow.App.csproj"
$iconPng = Join-Path $repoRoot "assets\FileFlow.png"
$plistSource = Join-Path $scriptDir "macos\Info.plist"

$targetRids = switch ($Runtime.ToLower()) {
    "osx-arm64" { @("osx-arm64") }
    "osx-x64"   { @("osx-x64") }
    default     { @("osx-arm64", "osx-x64") }
}

$isNativeMac = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)

foreach ($rid in $targetRids) {
    Write-Host "`n📦 Compilando y empaquetando para $rid ($Configuration)..." -ForegroundColor Yellow
    
    $publishPayload = Join-Path $workDir "payload_$rid"
    New-Item -ItemType Directory -Path $publishPayload -Force | Out-Null
    
    $pubArgs = @(
        "publish", $appProject,
        "-c", $Configuration,
        "-r", $rid,
        "--self-contained", "true",
        "-o", $publishPayload,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true",
        "-p:DebugType=none",
        "-p:DebugSymbols=false"
    )
    
    & dotnet @pubArgs | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo al compilar FileFlow.App para $rid"
    }

    # Estructurar FileFlow Studio.app
    $bundleAppDir = Join-Path $workDir "FileFlow Studio.app"
    if (Test-Path $bundleAppDir) { Remove-Item -Recurse -Force $bundleAppDir }
    
    $contentsDir = Join-Path $bundleAppDir "Contents"
    $macOsDir = Join-Path $contentsDir "MacOS"
    $resourcesDir = Join-Path $contentsDir "Resources"
    
    New-Item -ItemType Directory -Path $macOsDir -Force | Out-Null
    New-Item -ItemType Directory -Path $resourcesDir -Force | Out-Null
    
    # Copiar binarios y plugins a Contents/MacOS
    Copy-Item -Path "$publishPayload\*" -Destination $macOsDir -Recurse -Force
    
    # PkgInfo
    [System.IO.File]::WriteAllText((Join-Path $contentsDir "PkgInfo"), "APPL????")
    
    # Info.plist actualizado con la versión
    if (Test-Path $plistSource) {
        $plistContent = Get-Content $plistSource -Raw
        $plistContent = $plistContent -replace "1\.0\.0", $Version
        [System.IO.File]::WriteAllText((Join-Path $contentsDir "Info.plist"), $plistContent, (New-Object System.Text.UTF8Encoding($false)))
    }
    
    # Iconos
    if (Test-Path $iconPng) {
        Copy-Item $iconPng (Join-Path $resourcesDir "FileFlow.png") -Force
    }

    # Empaquetar en archivo ZIP
    $zipName = "FileFlowStudio-v$Version-$rid.zip"
    $zipOutput = Join-Path $outputDir $zipName
    if (Test-Path $zipOutput) { Remove-Item $zipOutput -Force }
    
    Write-Host "  -> Generando archivo comprimido: $zipName..." -ForegroundColor DarkGray
    Push-Location $workDir
    try {
        if (Get-Command "tar" -ErrorAction SilentlyContinue) {
            tar -czf (Join-Path $outputDir "FileFlowStudio-v$Version-$rid.tar.gz") "FileFlow Studio.app"
        }
        Compress-Archive -Path "FileFlow Studio.app" -DestinationPath $zipOutput -Force
    } finally {
        Pop-Location
    }
    
    if (Test-Path $zipOutput) {
        $zipSize = [math]::Round(((Get-Item $zipOutput).Length / 1MB), 2)
        Write-Host "  [OK] Paquete macOS generado: $zipName ($zipSize MB)" -ForegroundColor Green
    }

    # Si se corre en macOS, generar imagen DMG nativa
    if ($isNativeMac) {
        $dmgName = "FileFlowStudio-v$Version-$rid.dmg"
        $dmgOutput = Join-Path $outputDir $dmgName
        Write-Host "  -> Generando imagen de disco DMG: $dmgName..." -ForegroundColor DarkGray
        & hdiutil create -volname "FileFlow Studio" -srcfolder "$bundleAppDir" -ov -format UDZO "$dmgOutput" 2>$null
        if (Test-Path $dmgOutput) {
            $dmgSize = [math]::Round(((Get-Item $dmgOutput).Length / 1MB), 2)
            Write-Host "  [OK] Imagen DMG generada: $dmgName ($dmgSize MB)" -ForegroundColor Green
        }
    }
}

Remove-Item -Recurse -Force $workDir -ErrorAction SilentlyContinue
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  Generación de paquetes para macOS completada con éxito!  " -ForegroundColor Green
Write-Host "  Salida generada en: $outputDir" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
