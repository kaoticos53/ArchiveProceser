param (
    [switch]$NoBuild,
    [switch]$SelfCheck,
    [switch]$SelfCheckUia,
    [string]$Configuration = "Debug",
    [string]$MsBuildPath = "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

# =========================================================
#   FileFlow Studio - Host Uno Platform (WinUI 3) Launcher
# =========================================================
# El host Uno NO compila con `dotnet build` (los targets de WinAppSDK exigen MSBuild de Visual
# Studio, la lección del tramo Uno) y su ejecutable vive en net10.0-windows10.0.19041.0.
# Los gemelos del Avalonia son .\run.ps1 y .\run-fast.ps1; este script es el del host Uno.
#
# Uso:
#   .\run-uno.ps1                       compila (MSBuild VS) y lanza la app
#   .\run-uno.ps1 -NoBuild              lanza sin compilar
#   .\run-uno.ps1 -SelfCheck            sondeo interno en runtime (exit 0 = verificado)
#   .\run-uno.ps1 -SelfCheckUia         sondeo UIA EXTERNO (hijo python; exit 0 = verificado)
#   .\run-uno.ps1 -- --selfcheck        (equivalente por argumento directo de la app)

$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

# El modo de sondeo pasa el argumento a la app y ESPERA el proceso (el veredicto es el exit code).
if ($SelfCheck)   { $AppArgs = @("--selfcheck")     + $AppArgs }
if ($SelfCheckUia){ $AppArgs = @("--selfcheck-uia") + $AppArgs }
$waitForExit = $SelfCheck -or $SelfCheckUia

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Host Uno (WinUI 3)   " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$projectDir = Join-Path $scriptDir "FileFlow.App.Uno"
$projectPath = Join-Path $projectDir "FileFlow.App.Uno.csproj"

if (-not $NoBuild) {
    if (-not (Test-Path $MsBuildPath)) {
        Write-Host "`n[ERROR] No se encontro MSBuild de Visual Studio en:" -ForegroundColor Red
        Write-Host "  $MsBuildPath" -ForegroundColor White
        Write-Host "El host Uno exige MSBuild de VS (los targets de WinAppSDK no corren con dotnet build)." -ForegroundColor Gray
        Write-Host "Pasalo a mano con: .\run-uno.ps1 -MsBuildPath <ruta a MSBuild.exe>" -ForegroundColor Gray
        exit 1
    }

    Write-Host "`nCompilando el host Uno ($Configuration) con MSBuild de Visual Studio..." -ForegroundColor Yellow
    & $MsBuildPath $projectPath -p:Configuration=$Configuration -verbosity:quiet -nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n[ERROR] La compilacion del host Uno fallo. Revisa los errores." -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "Compilacion exitosa." -ForegroundColor Green
} else {
    Write-Host "`n[Modo Rapido] Omitiendo compilacion (-NoBuild)..." -ForegroundColor Yellow
}

$exePath = Join-Path $projectDir "bin\$Configuration\net10.0-windows10.0.19041.0\FileFlow.App.Uno.exe"

if (-not (Test-Path $exePath)) {
    $fallbackConfig = if ($Configuration -eq "Debug") { "Release" } else { "Debug" }
    $fallbackPath = Join-Path $projectDir "bin\$fallbackConfig\net10.0-windows10.0.19041.0\FileFlow.App.Uno.exe"
    if (Test-Path $fallbackPath) {
        $exePath = $fallbackPath
        $Configuration = $fallbackConfig
    } else {
        Write-Host "`n[ERROR] No se encontro el ejecutable en '$exePath'." -ForegroundColor Red
        Write-Host "Ejecuta '.\run-uno.ps1' sin el parametro -NoBuild para compilar primero." -ForegroundColor Gray
        exit 1
    }
}

$binDir = Split-Path -Parent $exePath

Write-Host "Iniciando el host Uno ($Configuration)..." -ForegroundColor Green

if ($waitForExit) {
    # Los sondeos deciden el veredicto por su codigo de salida: el script lo hereda.
    & $exePath @AppArgs
    exit $LASTEXITCODE
}

if ($AppArgs -and $AppArgs.Count -gt 0) {
    Start-Process -FilePath $exePath -ArgumentList $AppArgs -WorkingDirectory $binDir
} else {
    Start-Process -FilePath $exePath -WorkingDirectory $binDir
}
