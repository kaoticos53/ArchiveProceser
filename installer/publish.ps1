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
	[bool]$SelfContained = $true,
	[bool]$SingleFile = $true
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "FileFlow.App\FileFlow.App.csproj"
$publishRoot = Join-Path $PSScriptRoot "publish\$Runtime"

Write-Host "==> Limpiando carpeta de publicación anterior: $publishRoot" -ForegroundColor Cyan
if (Test-Path $publishRoot) {
	Remove-Item $publishRoot -Recurse -Force
}

Write-Host "==> Publicando FileFlow.App ($Configuration, $Runtime, SelfContained=$SelfContained, SingleFile=$SingleFile)..." -ForegroundColor Cyan

if ($SingleFile -and -not $SelfContained) {
	Write-Warning "SingleFile requiere SelfContained=true. Forzando SelfContained=true."
	$SelfContained = $true
}

$publishArgs = @(
	"publish", $projectPath,
	"-c", $Configuration,
	"-r", $Runtime,
	"--self-contained", $SelfContained.ToString().ToLower(),
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

Write-Host "==> Publicación completada en: $publishRoot" -ForegroundColor Green
