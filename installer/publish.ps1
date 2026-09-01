<#
.SYNOPSIS
	Publica FileFlow.App (WPF) lista para empaquetar con Inno Setup.

.DESCRIPTION
	Ejecuta `dotnet publish` sobre FileFlow.App con las opciones indicadas y copia
	los plugins (ya gestionados por el target CopyPlugins del csproj) en la carpeta
	de publicación. El resultado queda en installer/publish/<Runtime>.

.PARAMETER Configuration
	Configuración de compilación. Por defecto: Release.

.PARAMETER Runtime
	RID de destino. Por defecto: win-x64.

.PARAMETER SelfContained
	Si es $true, incluye el runtime de .NET 9 (no requiere tenerlo instalado).
	Si es $false, genera un publish framework-dependent (requiere .NET 9 Desktop Runtime en la máquina destino).
	Nota: PublishSingleFile requiere SelfContained=$true para incluir también el runtime en el .exe único.

.PARAMETER SingleFile
	Si es $true (por defecto), empaqueta todo el código gestionado y el runtime en un único
	FileFlow.App.exe autoextraíble, dejando fuera solo la carpeta Plugins/ (necesaria para
	la carga dinámica de plugins vía AssemblyLoadContext) y los .pdb.

.EXAMPLE
	./installer/publish.ps1
	./installer/publish.ps1 -SelfContained:$false -SingleFile:$false
	./installer/publish.ps1 -Runtime win-arm64
#>
param(
	[string]$Configuration = "Release",
	[string]$Runtime = "win-x64",
	$SelfContained = $true,
	$SingleFile = $true
)

$ErrorActionPreference = "Stop"

$isSelfContained = if ($SelfContained -is [bool]) { $SelfContained } else { [System.Convert]::ToBoolean($SelfContained) }
$isSingleFile = if ($SingleFile -is [bool]) { $SingleFile } else { [System.Convert]::ToBoolean($SingleFile) }

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "FileFlow.App\FileFlow.App.csproj"
$publishRoot = Join-Path $PSScriptRoot "publish\$Runtime"

Write-Host "==> Limpiando carpeta de publicación anterior: $publishRoot" -ForegroundColor Cyan
if (Test-Path $publishRoot) {
	Remove-Item $publishRoot -Recurse -Force
}

Write-Host "==> Publicando FileFlow.App ($Configuration, $Runtime, SelfContained=$isSelfContained, SingleFile=$isSingleFile)..." -ForegroundColor Cyan

if ($isSingleFile -and -not $isSelfContained) {
	Write-Warning "SingleFile requiere SelfContained=true. Forzando SelfContained=true."
	$isSelfContained = $true
}

$publishArgs = @(
	"publish", $projectPath,
	"-c", $Configuration,
	"-r", $Runtime,
	"--self-contained", $isSelfContained.ToString().ToLower(),
	"-o", $publishRoot,
	"-p:PublishSingleFile=$($SingleFile.ToString().ToLower())",
	"-p:IncludeNativeLibrariesForSelfExtract=true",
	"-p:EnableCompressionInSingleFile=true",
	"-p:DebugType=none",
	"-p:DebugSymbols=false"
)

dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
	throw "dotnet publish falló con código de salida $LASTEXITCODE"
}

if (-not (Test-Path (Join-Path $publishRoot "Plugins"))) {
	Write-Warning "No se encontró la carpeta 'Plugins' en el publish. Verifica el target CopyPlugins en FileFlow.App.csproj."
}

# Copiar ejemplos de flujos
$examplesSource = Join-Path $repoRoot "docs\examples"
$examplesDest = Join-Path $publishRoot "Examples"
if (Test-Path $examplesSource) {
	Write-Host "==> Copiando ejemplos de flujos a: $examplesDest" -ForegroundColor Cyan
	if (-not (Test-Path $examplesDest)) {
		New-Item -ItemType Directory -Path $examplesDest -Force | Out-Null
	}
	Copy-Item -Path "$examplesSource\*" -Destination $examplesDest -Recurse -Force
} else {
	Write-Warning "No se encontró la carpeta de ejemplos en '$examplesSource'."
}

# Generar y copiar manual de usuario en PDF
$pdfBuilderScript = Join-Path $PSScriptRoot "build-pdf-manual.ps1"
if (Test-Path $pdfBuilderScript) {
	try {
		& $pdfBuilderScript
	} catch {
		Write-Warning "No se pudo compilar el manual PDF: $($_.Exception.Message)"
	}
}

$docsDest = Join-Path $publishRoot "Docs"
Write-Host "==> Copiando manual de usuario en PDF a: $docsDest" -ForegroundColor Cyan
if (-not (Test-Path $docsDest)) {
	New-Item -ItemType Directory -Path $docsDest -Force | Out-Null
}

$pdfManual = Join-Path $repoRoot "docs\manual_de_usuario.pdf"
if (Test-Path $pdfManual) {
	Copy-Item -Path $pdfManual -Destination (Join-Path $docsDest "manual_de_usuario.pdf") -Force
} else {
	Write-Warning "No se encontró el manual PDF en '$pdfManual'."
}

Write-Host "==> Publicación completada en: $publishRoot" -ForegroundColor Green

