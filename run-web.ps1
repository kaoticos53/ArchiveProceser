param (
    [switch]$NoBuild,
    [switch]$Fast,
    [switch]$DevFrontend,
    [string]$Configuration = "Debug",
    [string]$LaunchProfile = "http",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Web Server Launcher   " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# --- 1. Compilar frontend web si hay cambios ---
$webDir = Join-Path $scriptDir "FileFlow.Web"
$serverWwwroot = Join-Path $scriptDir "FileFlow.Server\wwwroot"
$webDistDir = Join-Path $webDir "dist"

$skipBuild = $NoBuild -or $Fast

if (-not $DevFrontend -and -not $skipBuild) {
    if (-not (Test-Path (Join-Path $webDir "node_modules"))) {
        Write-Host "`n  -> Instalando dependencias de Node.js..." -ForegroundColor DarkGray
        Push-Location $webDir
        & npm install
        Pop-Location
    }

    Write-Host "`nCompilando FileFlow.Web (React + Vite)..." -ForegroundColor Yellow
    Push-Location $webDir
    & npm run build
    Pop-Location

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n[ERROR] Fallo al compilar FileFlow.Web." -ForegroundColor Red
        exit $LASTEXITCODE
    }

    # Copiar dist al wwwroot del servidor
    if (Test-Path $webDistDir) {
        if (-not (Test-Path $serverWwwroot)) {
            New-Item -ItemType Directory -Path $serverWwwroot -Force | Out-Null
        }
        Copy-Item -Path "$webDistDir\*" -Destination $serverWwwroot -Recurse -Force
        Write-Host "  -> Frontend copiado a FileFlow.Server/wwwroot" -ForegroundColor DarkGray
    }
}

# --- 2. Compilar backend .NET ---
if (-not $skipBuild) {
    $serverProject = Join-Path $scriptDir "FileFlow.Server\FileFlow.Server.csproj"
    Write-Host "`nCompilando FileFlow.Server ($Configuration)..." -ForegroundColor Yellow
    dotnet build $serverProject -c $Configuration

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n[ERROR] La compilación del servidor falló." -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "`nCompilación del servidor exitosa." -ForegroundColor Green
} else {
    Write-Host "`n[Modo Rápido] Omitiendo compilación (-NoBuild)..." -ForegroundColor Yellow
}

# --- 3. Lanzar Vite dev server si se pidió ---
if ($DevFrontend) {
    Write-Host "`nIniciando Vite dev server (FileFlow.Web)..." -ForegroundColor Magenta
    if (-not (Test-Path (Join-Path $webDir "node_modules"))) {
        Write-Host "  -> Instalando dependencias de Node.js..." -ForegroundColor DarkGray
        Push-Location $webDir
        & npm install
        Pop-Location
    }
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c cd /d `"$webDir`" && npm run dev" -WorkingDirectory $webDir
    Start-Sleep -Seconds 2
    Write-Host "  -> Vite dev server arrancado (revisa la terminal)." -ForegroundColor Green
}

# --- 4. Lanzar servidor ASP.NET ---
$serverProject = Join-Path $scriptDir "FileFlow.Server\FileFlow.Server.csproj"
Write-Host "`nIniciando FileFlow.Server ($Configuration, perfil: $LaunchProfile)..." -ForegroundColor Green

$runArgs = @("run", "--project", $serverProject, "-c", $Configuration, "--launch-profile", $LaunchProfile)
if ($skipBuild) { $runArgs += "--no-build" }
if ($AppArgs -and $AppArgs.Count -gt 0) { $runArgs += "--"; $runArgs += $AppArgs }

Write-Host "  URL: http://localhost:5002" -ForegroundColor White
Write-Host "  Presiona Ctrl+C para detener el servidor." -ForegroundColor DarkGray
Write-Host ""

& dotnet @runArgs
