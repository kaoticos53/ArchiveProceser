# =========================================================
#   FileFlow Studio - Mutation Harness (mutate.ps1)
# =========================================================

<#
.SYNOPSIS
    Aplica defectos deliberados y pequenos sobre el codigo de produccion y comprueba que la suite los muerde.

.DESCRIPTION
    Una mutacion es un defecto declarado (mutations/*.json): una sustitucion literal sobre uno o varios ficheros
    de producto. Si la suite sigue en verde con el defecto dentro, la prueba que decia cubrir ese comportamiento
    no lo cubre, y el andamiaje lo cuenta como fallo.

    El andamiaje es responsable de tres cosas, y las tres son obligatorias:

      1. RESTAURAR siempre, pase lo que pase (excepcion, mutacion obsoleta, fallo de compilacion o de tests).
      2. RECOMPILAR siempre despues de restaurar: las fuentes restauradas no arreglan los binarios, y una corrida
         posterior con --no-build mediria el mutante. Esta medido: dos pruebas "rotas" que eran el mutante que
         quedo compilado de una corrida anterior.
      3. NEGARSE a dejar el mutante en el arbol: antes de tocar nada se escribe un diario en disco
         (.mutation-journal/) con una copia y el hash de cada fichero; al terminar se restaura por bytes, se
         recompila y se verifica por hash. Si algo no cuadra, sale con codigo 2 y lo dice. Un diario sin cerrar
         (proceso matado) se recupera al arrancar la siguiente corrida.

    La comprobacion final de cada mutacion usa --no-build a proposito: solo puede pasar si la recompilacion tras
    restaurar ocurrio de verdad, asi que deja el arbol de fuentes y los binarios en el estado original.

    Los textos que imprime este script son ASCII a proposito: Windows PowerShell 5.1 lee los .ps1 sin BOM como
    ANSI. El texto con acentos vive en las definiciones (mutations/*.json), que se leen declarando UTF-8.

.PARAMETER Name
    Identificador de una mutacion (-Name limpiador-vuelve-a-mirar-el-disco), o varios separados por comas.

.PARAMETER All
    Ejecuta todas las mutaciones declaradas.

.PARAMETER Directory
    Directorio de definiciones (por omision mutations/). Sirve para probar el andamiaje con definiciones de
    usar y tirar sin tocar las del repositorio.

.PARAMETER List
    Lista las mutaciones declaradas con su tesis y su testigo, sin ejecutar ninguna.

.PARAMETER Help
    Muestra esta ayuda.

.EXAMPLE
    .\mutate.ps1 -List
    .\mutate.ps1 -Name limpiador-vuelve-a-mirar-el-disco
    .\mutate.ps1 -All

.NOTES
    Codigos de salida: 0 = todas las mutaciones mordieron; 1 = alguna sobrevivio, fue imprecisa o no compila;
    2 = rechazo (mutacion obsoleta, restauracion incompleta o binarios que no corresponden al arbol restaurado).
#>

param(
    [string]$Name = "",
    [switch]$All = $false,
    [string]$Directory = "",
    [switch]$List = $false,
    [switch]$Coverage = $false,
    [switch]$Help = $false
)

$ErrorActionPreference = "Stop"

# Los mensajes y las definiciones son UTF-8 (el repositorio esta en espanol): que la consola no los machaque.
try { [System.Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

# -- Rutas --------------------------------------------------------------------------------------------
$repoRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) { (Get-Location).Path } else { $PSScriptRoot }
$testProject = Join-Path $repoRoot "FileFlow.Tests/FileFlow.Tests.csproj"
$mutationsDir = if ([string]::IsNullOrWhiteSpace($Directory)) { Join-Path $repoRoot "mutations" } else { $Directory }
$journalDir = Join-Path $repoRoot ".mutation-journal"
$journalSessionPath = Join-Path $journalDir "session.json"
$journalFilesDir = Join-Path $journalDir "files"

# -- Mensajes -----------------------------------------------------------------------------------------
function Write-Banner([string]$Text, [string]$Color = "Cyan") {
    Write-Host "`n=========================================" -ForegroundColor $Color
    Write-Host " $Text" -ForegroundColor $Color
    Write-Host "=========================================" -ForegroundColor $Color
}

function Write-Step([string]$Text) { Write-Host "`n==> $Text" -ForegroundColor Cyan }
function Write-Info([string]$Text) { Write-Host "    $Text" -ForegroundColor DarkGray }
function Write-Ok([string]$Text) { Write-Host "    [OK] $Text" -ForegroundColor Green }
function Write-Warn([string]$Text) { Write-Host "    [AVISO] $Text" -ForegroundColor Yellow }
function Write-Fail([string]$Text) { Write-Host "    [RECHAZO] $Text" -ForegroundColor Red }

if ($Help) {
    Write-Host "Uso de mutate.ps1:" -ForegroundColor Cyan
    Write-Host "  .\mutate.ps1 -List                          Lista las mutaciones declaradas" -ForegroundColor Yellow
    Write-Host "  .\mutate.ps1 -Name <id>                     Ejecuta una mutacion (varios ids separados por comas)" -ForegroundColor Yellow
    Write-Host "  .\mutate.ps1 -All                           Ejecuta todas las mutaciones declaradas" -ForegroundColor Yellow
    Write-Host "  .\mutate.ps1 -Coverage                      Publica la cobertura: lo declarado y los huecos" -ForegroundColor Yellow
    Write-Host "  .\mutate.ps1 -All -Directory <ruta>         Usa otro directorio de definiciones" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Formato de una mutacion: mutations/README.md" -ForegroundColor DarkGray
    exit 0
}

# -- Utilidades de fichero ----------------------------------------------------------------------------
function Get-Sha256Hex([byte[]]$Bytes) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-RelativePath([string]$Base, [string]$Full) {
    $prefix = $Base.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if ($Full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $Full.Substring($prefix.Length).Replace('\', '/')
    }
    return [System.IO.Path]::GetFileName($Full)
}

function Write-TextPreservingEncoding([string]$Path, [string]$Text, [bool]$HasBom) {
    $encoding = New-Object System.Text.UTF8Encoding($HasBom)
    [System.IO.File]::WriteAllText($Path, $Text, $encoding)
}

# -- Instantanea, diario y restauracion ---------------------------------------------------------------
function New-FileSnapshot([string[]]$Paths) {
    $snapshot = [ordered]@{}
    foreach ($path in $Paths) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "El fichero declarado por la mutacion no existe: $path"
        }

        $bytes = [System.IO.File]::ReadAllBytes($path)
        $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
        $text = if ($hasBom) {
            [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
        } else {
            [System.Text.Encoding]::UTF8.GetString($bytes)
        }

        $snapshot[$path] = @{
            Sha256 = Get-Sha256Hex $bytes
            Text   = $text
            HasBom = $hasBom
            Crlf   = $text.Contains("`r`n")
        }
    }
    return $snapshot
}

function Write-MutationJournal($Mutation, $Snapshot) {
    if (Test-Path $journalDir) { Remove-Item $journalDir -Recurse -Force }
    New-Item -ItemType Directory -Path $journalFilesDir -Force | Out-Null

    $entries = @()
    foreach ($path in $Snapshot.Keys) {
        $relative = Get-RelativePath $repoRoot $path
        $copy = Join-Path $journalFilesDir ($relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        New-Item -ItemType Directory -Path (Split-Path $copy -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $path -Destination $copy -Force
        $entries += [pscustomobject]@{ path = $relative; sha256 = $Snapshot[$path].Sha256 }
    }

    $session = [pscustomobject]@{
        mutation     = $Mutation.Id
        startedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        files        = $entries
    }
    $session | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $journalSessionPath -Encoding utf8
}

function Clear-MutationJournal {
    if (Test-Path $journalDir) { Remove-Item $journalDir -Recurse -Force -ErrorAction SilentlyContinue }
}

function Restore-FileSnapshot($Snapshot) {
    foreach ($path in $Snapshot.Keys) {
        $saved = $Snapshot[$path]
        Write-TextPreservingEncoding $path $saved.Text $saved.HasBom
    }
}

function Get-MutatedFiles($Snapshot) {
    $dirty = @()
    foreach ($path in $Snapshot.Keys) {
        if (-not (Test-Path -LiteralPath $path)) {
            $dirty += (Get-RelativePath $repoRoot $path) + " (desaparecido)"
            continue
        }
        $now = Get-Sha256Hex ([System.IO.File]::ReadAllBytes($path))
        if ($now -ne $Snapshot[$path].Sha256) {
            $dirty += (Get-RelativePath $repoRoot $path)
        }
    }
    return $dirty
}

# -- Comandos externos --------------------------------------------------------------------------------
# Los comandos nativos escriben en stderr y aqui un defecto deliberado escribe mucho: en Windows PowerShell 5.1
# cada linea de stderr se convierte en error terminante con ErrorActionPreference = Stop, asi que se baja a
# Continue solo durante la llamada (el ambito de la funcion no toca el del script) y el veredicto sale del
# codigo de salida, no del ruido.
function Invoke-Native([scriptblock]$Command) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $Command 2>&1
        return [pscustomobject]@{ Output = @($output); Code = $LASTEXITCODE }
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

function Invoke-BuildOnce {
    $result = Invoke-Native { dotnet build $testProject -v q --nologo }
    $errors = @($result.Output | Select-String -Pattern "error " | ForEach-Object { $_.Line.Trim() })

    # Un fallo de compilacion tiene dos causas que no se parecen en nada: el mutante no compila (defecto
    # declarado invalido) o el entorno no deja escribir los binarios (un FileFlow.App abierto los bloquea y la
    # compilacion da MSB3021/MSB3027 tras diez reintentos). Confundirlas seria dictar "la mutacion no vale"
    # sobre un defecto que si vale y no se llego a medir.
    $isCompilerError = @($errors | Where-Object { $_ -match "error CS\d" }).Count -gt 0
    $isFileLock = @($errors | Where-Object { $_ -match "MSB3021|MSB3027|being used by another process|bloqueado por" }).Count -gt 0

    return [pscustomobject]@{
        Ok              = ($result.Code -eq 0)
        Code            = $result.Code
        Errors          = $errors
        IsCompilerError = $isCompilerError
        IsFileLock      = $isFileLock
    }
}

# Las instancias de FileFlow.App bloquean los DLL del directorio de salida y hacen fallar la compilacion. Es lo
# mismo que hace test.ps1 antes de compilar.
function Stop-RunningApp {
    $running = @(Get-Process -Name "FileFlow.App" -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) {
        Write-Warn "Cerrando $($running.Count) instancia(s) de FileFlow.App: bloquean los DLL del directorio de salida (igual que test.ps1)."
        $running | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }
}

function Invoke-TestFilter([string]$Filter) {
    $result = Invoke-Native { dotnet test $testProject --no-build --nologo --filter $Filter }
    $summary = $result.Output | Select-String -Pattern "Total:" | Select-Object -Last 1
    $line = if ($summary) { $summary.Line.Trim() } else { "(sin resumen)" }

    # Un filtro que no casa con ninguna prueba NO es una medida: `dotnet test` sale con 0 y sin resumen, asi que
    # leerlo por codigo de salida convertiria un testigo inexistente en "superviviente" y un control inexistente
    # en "control verde". El total declarado en el resumen es lo unico que distingue medir de no medir.
    $matched = 0
    if ($line -match "Total:\s*(\d+)") { $matched = [int]$Matches[1] }

    return [pscustomobject]@{ Ok = ($result.Code -eq 0); Code = $result.Code; Summary = $line; Matched = $matched }
}

# -- Cobertura publicada -------------------------------------------------------------------------------
# El documento lo genera la suite (`MutationDeclarationCoverageTests`) desde las definiciones y el arbol, y la
# guardia falla si se queda atras: aqui solo se lee, para que la lista de huecos tenga un unico autor.
function Show-MutationCoverage {
    $document = Join-Path $mutationsDir "COVERAGE.md"
    if (-not (Test-Path $document)) {
        Write-Fail "No hay documento de cobertura en $document."
        Write-Info "Generalo con: dotnet test --filter MutationDeclarationCoverageTests con FILEFLOW_UPDATE_MUTATION_COVERAGE=1"
        exit 2
    }

    $text = Get-Content -LiteralPath $document -Raw -Encoding UTF8
    $declared = @(Get-ChildItem -Path $mutationsDir -Filter *.json -File).Count
    if ($text -match "Mutaciones declaradas:\s*(\d+)") {
        $published = [int]$Matches[1]
        if ($published -ne $declared) {
            Write-Warn "El documento esta desactualizado: declara $published mutacion(es) y hay $declared definicion(es). Regeneralo con FILEFLOW_UPDATE_MUTATION_COVERAGE=1 dotnet test --filter MutationDeclarationCoverageTests."
        }
    }

    Write-Host $text
}

# -- Recuperacion de una corrida interrumpida ---------------------------------------------------------
function Repair-InterruptedRun {
    Write-Warn "Hay un diario de mutacion sin cerrar: una corrida anterior no termino (proceso matado o fallo)."
    $session = Get-Content -LiteralPath $journalSessionPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Info "Mutacion interrumpida: $($session.mutation) ($($session.startedAtUtc))"

    foreach ($file in $session.files) {
        $separator = [System.IO.Path]::DirectorySeparatorChar
        $target = Join-Path $repoRoot ($file.path.Replace('/', $separator))
        $copy = Join-Path $journalFilesDir ($file.path.Replace('/', $separator))
        if (Test-Path $copy) {
            Copy-Item -LiteralPath $copy -Destination $target -Force
        }
        $now = Get-Sha256Hex ([System.IO.File]::ReadAllBytes($target))
        if ($now -ne $file.sha256) {
            throw "Recuperacion fallida: '$($file.path)' no coincide con el original del diario. Restauralo a mano desde $journalFilesDir."
        }
    }

    $build = Invoke-BuildOnce
    if (-not $build.Ok) {
        throw "El arbol se recupero pero no compila: $($build.Errors -join ' | ')"
    }

    Clear-MutationJournal
    Write-Ok "Arbol recuperado del diario y recompilado antes de seguir."
}

# -- Definiciones -------------------------------------------------------------------------------------
function Assert-MutationShape($Definition, [string]$Origin) {
    foreach ($field in @("id", "claim", "edits", "witness")) {
        if (-not $Definition.PSObject.Properties.Name.Contains($field)) {
            throw "La mutacion '$Origin' no declara '$field'."
        }
    }
    if ([string]::IsNullOrWhiteSpace($Definition.witness.filter)) {
        throw "La mutacion '$Origin' no declara testigo (witness.filter): sin testigo no hay nada que morder."
    }
    foreach ($edit in $Definition.edits) {
        foreach ($field in @("file", "replacements")) {
            if (-not $edit.PSObject.Properties.Name.Contains($field)) {
                throw "Una edicion de la mutacion '$Origin' no declara '$field'."
            }
        }
        foreach ($replacement in $edit.replacements) {
            foreach ($field in @("old", "new")) {
                if (-not $replacement.PSObject.Properties.Name.Contains($field)) {
                    throw "Una sustitucion de la mutacion '$Origin' no declara '$field'."
                }
            }
        }
    }
}

function Get-DeclaredMutations {
    if (-not (Test-Path $mutationsDir)) {
        throw "No existe el directorio de mutaciones: $mutationsDir"
    }

    $files = @(Get-ChildItem -Path $mutationsDir -Filter *.json -File | Sort-Object Name)
    if ($files.Count -eq 0) {
        throw "No hay ninguna mutacion declarada en $mutationsDir"
    }

    $declared = @()
    foreach ($file in $files) {
        $definition = (Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8) | ConvertFrom-Json
        Assert-MutationShape $definition $file.Name
        $declared += [pscustomobject]@{
            Id      = $definition.id
            Hito    = $definition.hito
            Claim   = $definition.claim
            Why     = $definition.why
            Edits   = $definition.edits
            Witness = $definition.witness
            Control = $definition.control
            Origin  = $file.Name
        }
    }
    return $declared
}

function Show-MutationList($Mutations) {
    Write-Banner "Mutaciones declaradas ($($Mutations.Count))"
    foreach ($mutation in $Mutations) {
        Write-Host "`n  $($mutation.Id)" -ForegroundColor Yellow
        Write-Host "    tesis   : $($mutation.Claim)" -ForegroundColor Gray
        Write-Host "    testigo : $($mutation.Witness.filter)" -ForegroundColor DarkGray
        if ($mutation.Control) {
            Write-Host "    control : $($mutation.Control.filter)" -ForegroundColor DarkGray
        }
        if ($null -ne $mutation.Hito) {
            Write-Host "    origen  : hito $($mutation.Hito) ($($mutation.Origin))" -ForegroundColor DarkGray
        }
    }
    Write-Host ""
}

# -- La mutacion --------------------------------------------------------------------------------------
function Apply-MutationEdits($Mutation, $Snapshot) {
    # Todo se valida y se sustituye EN MEMORIA antes de escribir un solo byte: una mutacion obsoleta (el codigo
    # cambio desde que se declaro) no puede dejar media mutacion aplicada en el arbol.
    $plan = [ordered]@{}
    foreach ($edit in $Mutation.Edits) {
        $path = Join-Path $repoRoot $edit.file
        if (-not $Snapshot.Contains($path)) {
            throw "La edicion apunta a '$($edit.file)' y el fichero no esta en la instantanea."
        }

        $text = $Snapshot[$path].Text -replace "`r`n", "`n"
        foreach ($replacement in $edit.replacements) {
            $expected = 1
            if ($null -ne $replacement.count) { $expected = [int]$replacement.count }

            $found = ([regex]::Matches($text, [regex]::Escape($replacement.old))).Count
            if ($found -ne $expected) {
                throw "Mutacion obsoleta en '$($edit.file)': el fragmento aparece $found vez/veces y se esperaban $expected. El codigo cambio desde que se declaro la mutacion; actualizala (no se ha tocado nada)."
            }

            $text = $text.Replace($replacement.old, $replacement.new)
        }
        $plan[$path] = $text
    }

    foreach ($path in $plan.Keys) {
        $saved = $Snapshot[$path]
        $text = if ($saved.Crlf) { $plan[$path] -replace "`n", "`r`n" } else { $plan[$path] }
        Write-TextPreservingEncoding $path $text $saved.HasBom
    }
}

function Invoke-Mutation($Mutation) {
    Write-Step "$($Mutation.Id)"
    Write-Info $Mutation.Claim

    $touched = @($Mutation.Edits | ForEach-Object { Join-Path $repoRoot $_.file })
    $snapshot = New-FileSnapshot $touched

    $state = [ordered]@{
        Id      = $Mutation.Id
        Verdict = "sin-veredicto"
        Note    = ""
        Witness = $null
        Control = $null
        Proof   = ""
        Refusal = ""
        Seconds = 0
    }
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        Write-MutationJournal $Mutation $snapshot
        Apply-MutationEdits $Mutation $snapshot
        Write-Info "Mutante aplicado en $($touched.Count) fichero(s)."

        $build = Invoke-BuildOnce
        if (-not $build.Ok -and -not $build.IsCompilerError) {
            $hint = if ($build.IsFileLock) { " (ficheros bloqueados: ¿hay un FileFlow.App abierto?)" } else { "" }
            throw "la compilacion fallo por el entorno y no por el mutante${hint}: $(($build.Errors | Select-Object -First 1))"
        }
        if (-not $build.Ok) {
            $state.Verdict = "no-compila"
            $state.Note = ($build.Errors | Select-Object -First 3) -join " | "
            Write-Warn "El mutante no compila: la mutacion no es valida (no es un defecto silencioso)."
        }
        else {
            $witness = Invoke-TestFilter $Mutation.Witness.Filter
            $state.Witness = $witness
            Write-Info "testigo : $($witness.Summary)"

            if ($witness.Matched -eq 0) {
                $state.Verdict = "imprecisa"
                $state.Note = "el testigo no coincide con ninguna prueba: la prueba se renombro o el filtro esta mal escrito, y sin prueba no se esta midiendo nada"
            }
            elseif ($witness.Ok) {
                $state.Verdict = "sobrevive"
                $state.Note = "el testigo siguio en verde: la prueba que declara cubrir esto no lo detecta"
            }
            elseif ($null -ne $Mutation.Control) {
                $control = Invoke-TestFilter $Mutation.Control.Filter
                $state.Control = $control
                Write-Info "control : $($control.Summary)"
                if ($control.Matched -eq 0) {
                    $state.Verdict = "imprecisa"
                    $state.Note = "el control no coincide con ninguna prueba: un control vacio no distingue un mutante preciso de uno que lo rompe todo"
                }
                elseif ($control.Ok) {
                    $state.Verdict = "muerde"
                }
                else {
                    $state.Verdict = "imprecisa"
                    $state.Note = "rompio tambien el control: el mutante no dice lo que la tesis afirma"
                }
            }
            else {
                $state.Verdict = "muerde"
            }
        }
    }
    catch {
        $state.Verdict = "rechazo"
        $state.Note = $_.Exception.Message
        Write-Fail $state.Note
    }
    finally {
        # 1. Restaurar por bytes, pase lo que pase.
        Restore-FileSnapshot $snapshot

        # 2. Recompilar SIEMPRE tras restaurar: fuentes restauradas con binarios mutados es exactamente el estado
        #    que hacia que una corrida posterior midiera el mutante.
        $rebuild = Invoke-BuildOnce
        if (-not $rebuild.Ok) {
            $hint = if ($rebuild.IsFileLock) { " (ficheros bloqueados: ¿hay un FileFlow.App abierto?)" } else { "" }
            $state.Refusal = "la recompilacion tras restaurar fallo${hint}: $(($rebuild.Errors | Select-Object -First 3) -join ' | ')"
        }

        # 3. Negarse a dejar el mutante: verificacion por hash contra la instantanea.
        $dirty = @(Get-MutatedFiles $snapshot)
        if ($dirty.Count -gt 0) {
            $state.Refusal = "quedo el mutante en el arbol: $($dirty -join ', ')"
        }
        else {
            Write-Ok "arbol restaurado por bytes ($($touched.Count) fichero(s) identicos al original) y recompilado."
        }

        # 4. La prueba de que los binarios en disco son los del arbol restaurado: el testigo tiene que estar en
        #    verde SIN recompilar. Si no lo esta, lo que hay en bin/ no corresponde a estas fuentes.
        if ([string]::IsNullOrEmpty($state.Refusal)) {
            $proof = Invoke-TestFilter $Mutation.Witness.Filter
            if ($proof.Matched -eq 0) {
                $state.Refusal = "el testigo no coincide con ninguna prueba al comprobar el arbol restaurado: la definicion no mide nada"
            }
            elseif ($proof.Ok) {
                $state.Proof = "testigo en verde con --no-build (los binarios son los del arbol restaurado)"
                Write-Ok $state.Proof
            }
            else {
                $state.Refusal = "el testigo sigue rojo con el arbol restaurado y --no-build: los binarios no corresponden a las fuentes ($($proof.Summary))"
            }
        }

        if ([string]::IsNullOrEmpty($state.Refusal)) {
            Clear-MutationJournal
        }
    }

    $stopwatch.Stop()
    $state.Seconds = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1)

    if (-not [string]::IsNullOrEmpty($state.Refusal)) {
        Write-Fail $state.Refusal
    }

    return $state
}

# -- Programa principal -------------------------------------------------------------------------------

Write-Banner "FileFlow Studio - Andamiaje de mutaciones"

if (Test-Path $journalSessionPath) {
    Repair-InterruptedRun
}

if ($Coverage) {
    Show-MutationCoverage
    exit 0
}

$mutations = @(Get-DeclaredMutations)

if ($List -or ($Name -eq "" -and -not $All)) {
    Show-MutationList $mutations
    if (-not $List) {
        Write-Host "Elige una mutacion con -Name <id> o ejecutalas todas con -All." -ForegroundColor DarkGray
    }
    exit 0
}

$selected = @()
if ($All) {
    $selected = $mutations
}
else {
    foreach ($requested in ($Name -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })) {
        $match = $mutations | Where-Object { $_.Id -eq $requested }
        if (-not $match) {
            Write-Host "`n[ERROR] No existe la mutacion '$requested'. Declaradas: $($mutations.Id -join ', ')" -ForegroundColor Red
            exit 1
        }
        $selected += $match
    }
}

Stop-RunningApp

$results = @()
foreach ($mutation in $selected) {
    $results += Invoke-Mutation $mutation
}

# -- Informe ------------------------------------------------------------------------------------------
Write-Banner "Resultado"

$bit = 0
$survived = 0
$weak = 0
$refused = 0

foreach ($result in $results) {
    $verdict = $result.Verdict
    switch ($verdict) {
        "muerde"     { $color = "Green"; $bit++ }
        "sobrevive"  { $color = "Red"; $survived++ }
        "rechazo"    { $color = "Red"; $refused++ }
        "no-compila" { $color = "Red"; $weak++ }
        default      { $color = "Red"; $weak++ }
    }

    Write-Host ("`n  {0,-12} {1}  ({2} s)" -f $verdict.ToUpperInvariant(), $result.Id, $result.Seconds) -ForegroundColor $color
    if ($result.Witness) {
        Write-Host "      testigo : $($result.Witness.Summary)" -ForegroundColor DarkGray
    }
    if ($result.Control) {
        Write-Host "      control : $($result.Control.Summary)" -ForegroundColor DarkGray
    }
    if ($result.Note -ne "") {
        Write-Host "      nota    : $($result.Note)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "  Mutaciones ejecutadas : $($results.Count)" -ForegroundColor Cyan
Write-Host "  Mordidas              : $bit" -ForegroundColor Green
Write-Host "  Supervivientes        : $survived" -ForegroundColor $(if ($survived -gt 0) { "Red" } else { "DarkGray" })
Write-Host "  Imprecisas o invalidas: $weak" -ForegroundColor $(if ($weak -gt 0) { "Red" } else { "DarkGray" })
Write-Host "  Rechazos              : $refused" -ForegroundColor $(if ($refused -gt 0) { "Red" } else { "DarkGray" })
Write-Host ""

if ($refused -gt 0) {
    Write-Host " [RECHAZO] El andamiaje no termino limpio o no pudo medir: revisa las notas de arriba." -ForegroundColor Red
    exit 2
}
if ($survived -gt 0) {
    Write-Host " [FALLO] Alguna mutacion sobrevivio: la suite dice cubrir un comportamiento que no detecta." -ForegroundColor Red
    exit 1
}
if ($weak -gt 0) {
    Write-Host " [FALLO] Alguna mutacion no mide lo que declara (no compila o rompio el control)." -ForegroundColor Red
    exit 1
}

Write-Host " [OK] Todas las mutaciones mordieron y el arbol quedo como estaba." -ForegroundColor Green
exit 0
