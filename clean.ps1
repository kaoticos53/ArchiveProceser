# =========================================================
#   FileFlow Studio - Clean Artifacts Automation Script
# =========================================================

<#
.SYNOPSIS
    Limpia todos los archivos intermedios de compilación, binarios, carpetas de publicación,
    resultados de pruebas y cachés temporales de FileFlow Studio.

.DESCRIPTION
    Este script elimina:
    - Directorios de salida de compilación: 'bin' y 'obj' en todos los proyectos.
    - Artefactos de publicación: 'installer/publish' e 'installer/output'.
    - Resultados de pruebas y cobertura: 'TestResults' y 'coverage-report'.
    - Cachés de desarrollo y temporales: '.vs', '.dotnet_tmp', archivos '*.user', '*.suo' y 'crash.log'.
    - Opcionalmente con -IncludePdfs: manuales PDF generados en 'docs/*.pdf'.

.PARAMETER DryRun
    Si se especifica, simula la limpieza sin eliminar nada, mostrando los elementos detectados y el espacio recuperable.

.PARAMETER IncludePdfs
    Si se especifica, incluye la eliminación de los archivos PDF generados en 'docs/*.pdf'.

.PARAMETER Quiet
    Si se especifica, reduce los mensajes informativos en consola.

.PARAMETER Help
    Muestra la ayuda del script.

.EXAMPLE
    .\clean.ps1
    .\clean.ps1 -DryRun
    .\clean.ps1 -IncludePdfs
#>

param(
    [switch]$DryRun = $false,
    [switch]$IncludePdfs = $false,
    [switch]$Quiet = $false,
    [switch]$Help = $false
)

if ($Help) {
    Write-Host "Uso de clean.ps1:" -ForegroundColor Cyan
    Write-Host "  .\clean.ps1                Ejecuta la limpieza completa de binarios, publicaciones y temporales" -ForegroundColor Yellow
    Write-Host "  .\clean.ps1 -DryRun        Simula la limpieza y calcula el espacio que se liberaría" -ForegroundColor Yellow
    Write-Host "  .\clean.ps1 -IncludePdfs   Limpia también los manuales PDF generados en docs/" -ForegroundColor Yellow
    Write-Host "  .\clean.ps1 -Quiet         Ejecuta la limpieza en modo silencioso" -ForegroundColor Yellow
    exit 0
}

$repoRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
    $repoRoot = (Get-Location).Path
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "   FileFlow Studio - Limpieza Integral   " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "[MODO SIMULACION (DryRun)] No se eliminara ningun archivo.`n" -ForegroundColor Yellow
}

# 1. Cerrar instancias en ejecucion de FileFlow.App para liberar bloqueos de DLL/EXE
if (-not $DryRun) {
    $runningProcesses = Get-Process -Name "FileFlow.App" -ErrorAction SilentlyContinue
    if ($runningProcesses) {
        Write-Host "==> Cerrando instancias activas de FileFlow.App..." -ForegroundColor DarkYellow
        $runningProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }
}

# 2. Ejecutar dotnet clean sobre la solucion si no es DryRun
$slnPath = Join-Path $repoRoot "FileFlow.slnx"
if ((-not $DryRun) -and (Test-Path $slnPath)) {
    Write-Host "==> Ejecutando dotnet clean..." -ForegroundColor Cyan
    dotnet clean $slnPath -c Debug --verbosity quiet | Out-Null
    dotnet clean $slnPath -c Release --verbosity quiet | Out-Null
}

$global:cleanTotalBytes = 0
$global:cleanDeletedCount = 0

function Measure-DirectorySize([string]$dir) {
    if (-not (Test-Path $dir)) { return 0 }
    $sum = 0
    $items = Get-ChildItem -Path $dir -Recurse -File -Force -ErrorAction SilentlyContinue
    if ($items) {
        foreach ($it in $items) {
            $sum += $it.Length
        }
    }
    return $sum
}

function Remove-TargetDirectory([string]$dirPath, [string]$label) {
    if (Test-Path $dirPath) {
        $bytes = Measure-DirectorySize $dirPath
        $global:cleanTotalBytes += $bytes
        $global:cleanDeletedCount++
        $mb = [math]::Round($bytes / 1048576, 2)
        
        if ($DryRun) {
            Write-Host "  [SIMULADO] Carpeta: $label ($mb MB) -> $dirPath" -ForegroundColor Yellow
        } else {
            try {
                Remove-Item -Path $dirPath -Recurse -Force -ErrorAction Stop
                Write-Host "  [ELIMINADO] $label ($mb MB)" -ForegroundColor Green
            } catch {
                Write-Warning "  [AVISO] No se pudo eliminar '$dirPath': $($_.Exception.Message)"
            }
        }
    }
}

function Remove-TargetFiles([string]$searchPath, [string]$pattern, [string]$label) {
    if (Test-Path $searchPath) {
        $files = Get-ChildItem -Path $searchPath -Filter $pattern -Recurse -File -Force -ErrorAction SilentlyContinue
        if ($files) {
            foreach ($file in $files) {
                $global:cleanTotalBytes += $file.Length
                $global:cleanDeletedCount++
                $kb = [math]::Round($file.Length / 1024, 2)
                
                if ($DryRun) {
                    Write-Host "  [SIMULADO] Archivo: $($file.Name) ($kb KB)" -ForegroundColor Yellow
                } else {
                    try {
                        Remove-Item -Path $file.FullName -Force -ErrorAction Stop
                        Write-Host "  [ELIMINADO] Archivo: $($file.Name) ($kb KB)" -ForegroundColor Green
                    } catch {
                        Write-Warning "  [AVISO] No se pudo eliminar '$($file.FullName)': $($_.Exception.Message)"
                    }
                }
            }
        }
    }
}

Write-Host "`n1. Limpiando directorios de compilacion (bin / obj)..." -ForegroundColor Cyan

# Buscar todas las carpetas bin y obj de los proyectos
$projectDirs = Get-ChildItem -Path $repoRoot -Directory -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -like "FileFlow*"
}

foreach ($pDir in $projectDirs) {
    $binPath = Join-Path $pDir.FullName "bin"
    $objPath = Join-Path $pDir.FullName "obj"
    
    if (Test-Path $binPath) {
        Remove-TargetDirectory $binPath "$($pDir.Name)\bin"
    }
    if (Test-Path $objPath) {
        Remove-TargetDirectory $objPath "$($pDir.Name)\obj"
    }
}

Write-Host "`n2. Limpiando artefactos de publicacion e instalador..." -ForegroundColor Cyan
Remove-TargetDirectory (Join-Path $repoRoot "installer\publish") "installer\publish"
Remove-TargetDirectory (Join-Path $repoRoot "installer\output") "installer\output"

Write-Host "`n3. Limpiando resultados de pruebas y cobertura..." -ForegroundColor Cyan
Remove-TargetDirectory (Join-Path $repoRoot "TestResults") "TestResults"
Remove-TargetDirectory (Join-Path $repoRoot "coverage-report") "coverage-report"

Write-Host "`n4. Limpiando caches de IDE y archivos temporales..." -ForegroundColor Cyan
Remove-TargetDirectory (Join-Path $repoRoot ".vs") ".vs (Cache Visual Studio)"
Remove-TargetDirectory (Join-Path $repoRoot ".dotnet_tmp") ".dotnet_tmp"

# Archivos de usuario / suo / crash.log
Remove-TargetFiles $repoRoot "*.user" "Archivos *.user"
Remove-TargetFiles $repoRoot "*.suo" "Archivos *.suo"

$crashLog = Join-Path $repoRoot "crash.log"
if (Test-Path $crashLog) {
    $item = Get-Item $crashLog
    $global:cleanTotalBytes += $item.Length
    $global:cleanDeletedCount++
    if ($DryRun) {
        Write-Host "  [SIMULADO] Archivo: crash.log" -ForegroundColor Yellow
    } else {
        Remove-Item $crashLog -Force -ErrorAction SilentlyContinue
        Write-Host "  [ELIMINADO] Archivo: crash.log" -ForegroundColor Green
    }
}

# 5. Opcional: PDFs generados
if ($IncludePdfs) {
    Write-Host "`n5. Limpiando manuales PDF generados en docs/..." -ForegroundColor Cyan
    $docsDir = Join-Path $repoRoot "docs"
    Remove-TargetFiles $docsDir "*.pdf" "Manuales PDF generados"
}

# Resumen final
$totalMb = [math]::Round($global:cleanTotalBytes / 1048576, 2)
Write-Host "`n=========================================" -ForegroundColor Green
if ($DryRun) {
    Write-Host " Simulacion completada." -ForegroundColor Yellow
    Write-Host " Elementos identificados : $global:cleanDeletedCount" -ForegroundColor Cyan
    Write-Host " Espacio recuperable     : $totalMb MB" -ForegroundColor Cyan
} else {
    Write-Host " Limpieza completada con exito!" -ForegroundColor Green
    Write-Host " Elementos eliminados    : $global:cleanDeletedCount" -ForegroundColor Cyan
    Write-Host " Espacio total liberado  : $totalMb MB" -ForegroundColor Cyan
}
Write-Host "=========================================`n" -ForegroundColor Green
