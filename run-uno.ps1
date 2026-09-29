param (
    [switch]$NoBuild,
    [switch]$SelfCheck,
    [switch]$SelfCheckSettings,
    [switch]$SelfCheckControlBar,
    [switch]$SelfCheckDialogs,
    [switch]$SelfCheckUia,
    [string]$Configuration = "Debug",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

# =========================================================
#   FileFlow Studio - Host Uno Platform (WinUI 3) Launcher
# =========================================================
# El host Uno compila con `dotnet build` sobre SU solución (FileFlow.Uno.slnx), la misma que se abre
# en Visual Studio. El proyecto tiene WindowsPackageType=None + WindowsAppSDKSelfContained, así que los
# targets de WinAppSDK corren sin MSBuild de Visual Studio (era cierto lo contrario hasta el hito 267,
# cuando el host se compilaba con MSBuild de VS y arrastraba el toolkit del escritorio).
#
# Esa solución elige además el SABOR de UI por su nombre: los plugins se compilan SIN Avalonia, así que
# el binario del host Uno no lleva una sola DLL del toolkit del escritorio. El ejecutable vive en
# net10.0-windows10.0.19041.0 y los gemelos del Avalonia son .\run.ps1 y .\run-fast.ps1.
#
# Uso:
#   .\run-uno.ps1                       compila (dotnet build) y lanza la app
#   .\run-uno.ps1 -NoBuild              lanza sin compilar
#   .\run-uno.ps1 -SelfCheck            sondeo interno en runtime (exit 0 = verificado)
#   .\run-uno.ps1 -SelfCheckDialogs    sondeo de los PANELES DE NODO (el editor de texto y el catálogo
#                                      de variables que abren las filas del inspector)
#   .\run-uno.ps1 -SelfCheckControlBar sondeo de la BARRA DE CONTROL y su cajón (modo propio: su ciclo
#                                       mueve el documento, y las sondas del lienzo no lo toleran)
#   .\run-uno.ps1 -SelfCheckSettings    sondeo de la superficie de AJUSTES (modo propio: su medición
#                                       cambia tema e idioma, y las sondas del lienzo no lo toleran)
#   .\run-uno.ps1 -SelfCheckUia         sondeo UIA EXTERNO (hijo python; exit 0 = verificado)
#   .\run-uno.ps1 -- --selfcheck        (equivalente por argumento directo de la app)

$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

# El modo de sondeo pasa el argumento a la app y ESPERA el proceso (el veredicto es el exit code).
$argsList = [System.Collections.Generic.List[string]]::new()
if ($SelfCheck)           { $argsList.Add("--selfcheck") }
if ($SelfCheckSettings)   { $argsList.Add("--selfcheck-settings") }
if ($SelfCheckControlBar) { $argsList.Add("--selfcheck-controlbar") }
if ($SelfCheckDialogs)    { $argsList.Add("--selfcheck-dialogs") }
if ($SelfCheckUia)        { $argsList.Add("--selfcheck-uia") }
if ($AppArgs) {
    foreach ($arg in $AppArgs) {
        if (-not [string]::IsNullOrWhiteSpace($arg)) {
            $argsList.Add($arg)
        }
    }
}
$waitForExit = $SelfCheck -or $SelfCheckSettings -or $SelfCheckControlBar -or $SelfCheckDialogs -or $SelfCheckUia

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Host Uno (WinUI 3)   " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$projectDir = Join-Path $scriptDir "FileFlow.App.Uno"
$projectPath = Join-Path $projectDir "FileFlow.App.Uno.csproj"
$solutionPath = Join-Path $scriptDir "FileFlow.Uno.slnx"

if (-not $NoBuild) {
    if (-not (Test-Path $solutionPath)) {
        Write-Host "`n[ERROR] No se encontro la solucion del host Uno en:" -ForegroundColor Red
        Write-Host "  $solutionPath" -ForegroundColor White
        Write-Host "Es la solucion que elige el sabor de UI (sin el toolkit del escritorio)." -ForegroundColor Gray
        exit 1
    }

    Write-Host "`nCompilando el host Uno ($Configuration) con dotnet build sobre FileFlow.Uno.slnx..." -ForegroundColor Yellow
    Write-Host "(la solucion compila el grafo SIN Avalonia: el sabor lo elige su nombre)" -ForegroundColor Gray
    & dotnet build $solutionPath -c $Configuration -p:FileFlowUnoHost=true --nologo -v:m
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
    #
    # CON Start-Process -Wait -PassThru y NO con `& $exePath`: el host es una aplicacion de GUI
    # (subsistema Windows) y PowerShell NO espera a las de GUI —`&` devuelve el control de inmediato y
    # $LASTEXITCODE se queda con el valor anterior—. El 270 lo midio: el lanzador devolvia «exit 0» con
    # el sondeo todavia corriendo y el informe a medio escribir (o el de la corrida anterior), que es la
    # manera mas facil de dar por bueno un veredicto que nadie ha leido.
    if ($argsList.Count -gt 0) {
        $sondeo = Start-Process -FilePath $exePath -ArgumentList $argsList.ToArray() -WorkingDirectory $binDir -Wait -PassThru
    } else {
        $sondeo = Start-Process -FilePath $exePath -WorkingDirectory $binDir -Wait -PassThru
    }
    exit $sondeo.ExitCode
}

if ($argsList.Count -gt 0) {
    Start-Process -FilePath $exePath -ArgumentList $argsList.ToArray() -WorkingDirectory $binDir
} else {
    Start-Process -FilePath $exePath -WorkingDirectory $binDir
}
