param (
    [Parameter(Position = 0, ValueFromPipeline = $true)]
    [string]$Option,
    [string]$Target,
    [switch]$NoBuild,
    [switch]$Fast,
    [switch]$NoMenu,
    [switch]$DevFrontend,
    [string]$Configuration = "Debug",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

function Launch-Wpf([bool]$SkipBuild) {
    Write-Host "`n-----------------------------------------" -ForegroundColor Cyan
    Write-Host "  FileFlow Studio - WPF (Desktop)" -ForegroundColor Cyan
    Write-Host "-----------------------------------------" -ForegroundColor Cyan

    if (-not $SkipBuild) {
        Write-Host "`nCompilando la solución FileFlow.slnx ($Configuration)..." -ForegroundColor Yellow
        dotnet build (Join-Path $scriptDir "FileFlow.slnx") -c $Configuration

        if ($LASTEXITCODE -ne 0) {
            Write-Host "`n[ERROR] La compilación falló. Revisa los errores." -ForegroundColor Red
            exit $LASTEXITCODE
        }
        Write-Host "`nCompilación exitosa." -ForegroundColor Green
    } else {
        Write-Host "`n[Modo Rápido] Omitiendo compilación..." -ForegroundColor Yellow
    }

    $exePath = Join-Path $scriptDir "FileFlow.App\bin\$Configuration\net9.0-windows\FileFlow.App.exe"
    if (-not (Test-Path $exePath)) {
        $fallbackConfig = if ($Configuration -eq "Debug") { "Release" } else { "Debug" }
        $fallbackPath = Join-Path $scriptDir "FileFlow.App\bin\$fallbackConfig\net9.0-windows\FileFlow.App.exe"
        if (Test-Path $fallbackPath) {
            $exePath = $fallbackPath
            $Configuration = $fallbackConfig
        } else {
            Write-Host "`n[ERROR] No se encontró el ejecutable en '$exePath'." -ForegroundColor Red
            Write-Host "Ejecuta con compilación primero." -ForegroundColor Gray
            exit 1
        }
    }

    Write-Host "Iniciando FileFlow Studio ($Configuration)..." -ForegroundColor Green
    if ($AppArgs -and $AppArgs.Count -gt 0) {
        Start-Process -FilePath $exePath -ArgumentList $AppArgs -WorkingDirectory (Split-Path -Parent $exePath)
    } else {
        Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath)
    }
}

# Si se pasó explícitamente Target o NoMenu por línea de comandos, saltar el menú interactivo
if ($NoMenu -or $Target -or $PSBoundParameters.ContainsKey('NoBuild') -or $PSBoundParameters.ContainsKey('Fast')) {
    $targetNorm = if ($Target) { $Target.ToLower() } else { "wpf" }
    $shouldSkip = $NoBuild -or $Fast

    switch ($targetNorm) {
        "wpf" {
            Launch-Wpf -SkipBuild $shouldSkip
            exit 0
        }
        "web" {
            $webScript = Join-Path $scriptDir "run-web.ps1"
            $params = @{ Configuration = $Configuration }
            if ($shouldSkip) { $params["NoBuild"] = $true }
            if ($DevFrontend) { $params["DevFrontend"] = $true }
            & $webScript @params @AppArgs
            exit $LASTEXITCODE
        }
        "photino" {
            $photinoScript = Join-Path $scriptDir "run-photino.ps1"
            $params = @{ Configuration = $Configuration }
            if ($shouldSkip) { $params["NoBuild"] = $true }
            & $photinoScript @params @AppArgs
            exit $LASTEXITCODE
        }
        "webdev" {
            $webdevScript = Join-Path $scriptDir "run-webdev.ps1"
            & $webdevScript
            exit $LASTEXITCODE
        }
        default {
            Write-Host "Target desconocido: $Target. Opciones: wpf, web, photino, webdev" -ForegroundColor Red
            exit 1
        }
    }
}

# =========================================================================
# MENÚ INTERACTIVO
# =========================================================================
try { Clear-Host } catch { }

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "                FILEFLOW STUDIO - MENÚ DE EJECUCIÓN                     " -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  [1] WPF (Escritorio Windows Nativo)                  " -NoNewline -ForegroundColor White
Write-Host "[POR DEFECTO]" -ForegroundColor Green
Write-Host "  [2] WPF (Escritorio Windows) - Rápido (Sin compilar)" -ForegroundColor Gray
Write-Host ""
Write-Host "  [3] Web Server (ASP.NET Core + React) - Compilación Completa" -ForegroundColor White
Write-Host "  [4] Web Server (ASP.NET Core + React) - Rápido (Sin compilar)" -ForegroundColor Gray
Write-Host "  [5] Web Server + Frontend Dev (Vite Hot-Reload en vivo)" -ForegroundColor Magenta
Write-Host ""
Write-Host "  [6] Photino (Escritorio Híbrido Web) - Compilación Completa" -ForegroundColor White
Write-Host "  [7] Photino (Escritorio Híbrido Web) - Rápido (Sin compilar)" -ForegroundColor Gray
Write-Host ""
Write-Host "  [8] Frontend Web Dev (Solo Vite Dev Server)" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "  [9] Salir" -ForegroundColor DarkGray
Write-Host "========================================================================" -ForegroundColor Cyan

$selection = $Option
if ([string]::IsNullOrWhiteSpace($selection)) {
    $selection = Read-Host "`nSelecciona una opción [1-9] (Enter para opción por defecto: 1)"
}

if ([string]::IsNullOrWhiteSpace($selection)) {
    $selection = "1"
}

switch ($selection.Trim()) {
    "1" {
        Launch-Wpf -SkipBuild $false
    }
    "2" {
        Launch-Wpf -SkipBuild $true
    }
    "3" {
        & (Join-Path $scriptDir "run-web.ps1") -Configuration $Configuration @AppArgs
    }
    "4" {
        & (Join-Path $scriptDir "run-web.ps1") -NoBuild -Configuration $Configuration @AppArgs
    }
    "5" {
        & (Join-Path $scriptDir "run-web.ps1") -DevFrontend -Configuration $Configuration @AppArgs
    }
    "6" {
        & (Join-Path $scriptDir "run-photino.ps1") -Configuration $Configuration @AppArgs
    }
    "7" {
        & (Join-Path $scriptDir "run-photino.ps1") -NoBuild -Configuration $Configuration @AppArgs
    }
    "8" {
        & (Join-Path $scriptDir "run-webdev.ps1")
    }
    "9" {
        Write-Host "`nOperación cancelada." -ForegroundColor Yellow
        exit 0
    }
    default {
        Write-Host "`nOpción no válida ('$selection'). Iniciando opción por defecto [1] WPF..." -ForegroundColor Yellow
        Launch-Wpf -SkipBuild $false
    }
}
