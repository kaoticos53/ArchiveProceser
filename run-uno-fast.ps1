param (
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
#  FileFlow Studio - Host Uno Fast Launch (NoBuild)
# =========================================================
# El gemelo de .\run-fast.ps1 para el host Uno: sin compilar, directamente sobre el último
# binario. Compilar y lanzar en el MISMO comando cuando el XAML haya cambiado (los builds
# incrementales obsoletos de XAML mintieron a la bisección en el hito 233).
#
# Los modos de sondeo ESPERAN el proceso y heredan su exit code (el veredicto):
#   .\run-uno-fast.ps1 -SelfCheck          -> exit 0 = verificado
#   .\run-uno-fast.ps1 -SelfCheckDialogs    -> exit 0 = verificado (paneles de nodo: editor y variables)
#   .\run-uno-fast.ps1 -SelfCheckControlBar -> exit 0 = verificado (barra de control y su cajón)
#   .\run-uno-fast.ps1 -SelfCheckSettings  -> exit 0 = verificado (superficie de AJUSTES, modo propio)
#   .\run-uno-fast.ps1 -SelfCheckUia       -> exit 0 = verificado (hijo python observando)

$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

$exePath = Join-Path $scriptDir "FileFlow.App.Uno\bin\$Configuration\net10.0-windows10.0.19041.0\FileFlow.App.Uno.exe"

# Si no se encuentra en la configuracion solicitada, probar la otra configuracion (Debug/Release)
if (-not (Test-Path $exePath)) {
    $fallbackConfig = if ($Configuration -eq "Debug") { "Release" } else { "Debug" }
    $fallbackPath = Join-Path $scriptDir "FileFlow.App.Uno\bin\$fallbackConfig\net10.0-windows10.0.19041.0\FileFlow.App.Uno.exe"
    if (Test-Path $fallbackPath) {
        $exePath = $fallbackPath
        $Configuration = $fallbackConfig
    }
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  FileFlow Studio - Host Uno Fast Launch " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

if (-not (Test-Path $exePath)) {
    Write-Host "`n[AVISO] No se encontro el ejecutable compilado en:" -ForegroundColor Yellow
    Write-Host "  $exePath" -ForegroundColor White
    Write-Host "`nPor favor, compila el host Uno al menos una vez ejecutando: .\run-uno.ps1" -ForegroundColor Gray
    exit 1
}

$binDir = Split-Path -Parent $exePath

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

Write-Host "`n[OK] Iniciando el host Uno ($Configuration)..." -ForegroundColor Green

if ($waitForExit) {
    # CON Start-Process -Wait -PassThru y NO con `& $exePath`: el host es una aplicacion de GUI y
    # PowerShell no espera a las aplicaciones de GUI, asi que `&` volvia enseguida con un
    # $LASTEXITCODE viejo (medido en el hito 270: «exit 0» con el sondeo aun corriendo y el informe a
    # medio escribir).
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
