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

$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

$unoLauncher = Join-Path $scriptDir "run-uno-fast.ps1"

$params = @{}
if ($SelfCheck) { $params["SelfCheck"] = $true }
if ($SelfCheckSettings) { $params["SelfCheckSettings"] = $true }
if ($SelfCheckControlBar) { $params["SelfCheckControlBar"] = $true }
if ($SelfCheckDialogs) { $params["SelfCheckDialogs"] = $true }
if ($SelfCheckUia) { $params["SelfCheckUia"] = $true }
if ($Configuration) { $params["Configuration"] = $Configuration }
if ($AppArgs) { $params["AppArgs"] = $AppArgs }

& $unoLauncher @params
exit $LASTEXITCODE
