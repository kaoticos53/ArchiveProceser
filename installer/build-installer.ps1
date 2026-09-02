<#
.SYNOPSIS
	Genera el instalador de FileFlow Studio de principio a fin (publish + Inno Setup).

.DESCRIPTION
	1. Verifica que Inno Setup (ISCC.exe) esté disponible; si no, ofrece instalarlo con winget.
	2. Ejecuta installer/publish.ps1 para generar el publish de FileFlow.App.
	3. Compila installer/FileFlow.iss con ISCC.exe, generando el .exe instalador en installer/output.

.PARAMETER Version
	Versión a mostrar en el instalador y en el nombre del archivo generado. Por defecto: 1.0.0.

.PARAMETER Runtime
	RID de destino. Por defecto: win-x64.

.PARAMETER SelfContained
	Si es $true (por defecto), el instalador incluye el runtime de .NET 9.

.EXAMPLE
	./installer/build-installer.ps1
	./installer/build-installer.ps1 -Version "1.2.0" -SelfContained:$false
#>
param(
	[string]$Version = "1.0.0",
	[string]$Runtime = "win-x64",
	$SelfContained = $true
)

$ErrorActionPreference = "Stop"
$isSelfContained = if ($SelfContained -is [bool]) { $SelfContained } else { [System.Convert]::ToBoolean($SelfContained) }

function Find-InnoSetupCompiler {
	# 1. Buscar en el PATH
	$cmd = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
	if ($cmd) { return $cmd.Source }

	# 2. Buscar en el registro (clave usada por el instalador de Inno Setup)
	$registryPaths = @(
		"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
		"HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
	)
	foreach ($regPath in $registryPaths) {
		$entry = Get-ItemProperty $regPath -ErrorAction SilentlyContinue |
			Where-Object { $_.DisplayName -like "Inno Setup*" } |
			Select-Object -First 1
		if ($entry -and $entry.InstallLocation) {
			$candidate = Join-Path $entry.InstallLocation "ISCC.exe"
			if (Test-Path $candidate) { return $candidate }
		}
	}

	# 3. Buscar carpetas típicas "Inno Setup <version>" en Program Files (32 y 64 bits, cualquier versión)
	$programFolders = @("$env:ProgramFiles", "${env:ProgramFiles(x86)}") | Where-Object { $_ }
	foreach ($folder in $programFolders) {
		$matches = Get-ChildItem -Path $folder -Directory -Filter "Inno Setup*" -ErrorAction SilentlyContinue
		foreach ($match in $matches) {
			$candidate = Join-Path $match.FullName "ISCC.exe"
			if (Test-Path $candidate) { return $candidate }
		}
	}

	return $null
}

Write-Host "==> Buscando Inno Setup (ISCC.exe)..." -ForegroundColor Cyan
$iscc = Find-InnoSetupCompiler

if (-not $iscc) {
	Write-Warning "Inno Setup no está instalado."
	$answer = Read-Host "¿Quieres instalarlo ahora con winget? (S/N)"
	if ($answer -match "^[sS]") {
		winget install --id JRSoftware.InnoSetup -e --accept-source-agreements --accept-package-agreements
		$iscc = Find-InnoSetupCompiler
		if (-not $iscc) {
			throw "No se pudo localizar ISCC.exe tras la instalación. Reinicia la terminal e inténtalo de nuevo."
		}
	} else {
		throw "Inno Setup es necesario para generar el instalador. Instálalo manualmente desde https://jrsoftware.org/isdl.php"
	}
}

Write-Host "==> Inno Setup encontrado en: $iscc" -ForegroundColor Green

# 1. Publicar la aplicación
& (Join-Path $PSScriptRoot "publish.ps1") -Runtime $Runtime -SelfContained $isSelfContained

# 2. Compilar el instalador
$sourceDir = Join-Path $PSScriptRoot "publish\$Runtime"
$issPath = Join-Path $PSScriptRoot "FileFlow.iss"
$outputDir = Join-Path $PSScriptRoot "output"

if (-not (Test-Path $outputDir)) {
	New-Item -ItemType Directory -Path $outputDir | Out-Null
}

Write-Host "==> Compilando instalador con Inno Setup..." -ForegroundColor Cyan

& $iscc $issPath "/DSourceDir=$sourceDir" "/DAppVersion=$Version" "/O$outputDir"

if ($LASTEXITCODE -ne 0) {
	throw "ISCC.exe falló con código de salida $LASTEXITCODE"
}

# Copiar manuales PDF (Español e Inglés) a la carpeta de salida
$repoRoot = Split-Path -Parent $PSScriptRoot
$pdfManuals = @(
	@{ Src = "docs\manual_de_usuario.pdf"; Dest = "FileFlowStudio-Manual-de-Usuario.pdf" },
	@{ Src = "docs\manual_usuario_principiantes.pdf"; Dest = "FileFlowStudio-Guia-Principiantes.pdf" },
	@{ Src = "docs\manual_nodo_scripting.pdf"; Dest = "FileFlowStudio-Manual-Scripting.pdf" },
	@{ Src = "docs\user_manual.pdf"; Dest = "FileFlowStudio-User-Manual.pdf" },
	@{ Src = "docs\beginner_user_guide.pdf"; Dest = "FileFlowStudio-Beginners-Guide.pdf" },
	@{ Src = "docs\scripting_node_manual.pdf"; Dest = "FileFlowStudio-Scripting-Manual.pdf" }
)
foreach ($item in $pdfManuals) {
	$srcPath = Join-Path $repoRoot $item.Src
	if (Test-Path $srcPath) {
		Copy-Item -Path $srcPath -Destination (Join-Path $outputDir $item.Dest) -Force
		Write-Host "    - Copiado $($item.Dest)" -ForegroundColor DarkGray
	}
}

Write-Host "==> Instalador generado correctamente en: $outputDir" -ForegroundColor Green
Get-ChildItem $outputDir | ForEach-Object { Write-Host "    - $($_.FullName)" -ForegroundColor Green }
