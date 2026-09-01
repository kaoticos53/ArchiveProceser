<#
.SYNOPSIS
    Convierte el manual de usuario en Markdown (docs/manual_de_usuario.md) a un documento PDF profesional.

.DESCRIPTION
    Transforma el archivo Markdown a un documento HTML con diseño tipográfico y estilos de impresión A4,
    y utiliza el motor headless de Microsoft Edge (o Chrome) para compilar un archivo PDF de alta calidad
    listo para su distribución en el instalador y la versión portable.

.PARAMETER MarkdownPath
    Ruta del archivo Markdown de entrada. Por defecto: docs/manual_de_usuario.md.

.PARAMETER OutputPdfPath
    Ruta del archivo PDF de salida. Por defecto: docs/manual_de_usuario.pdf.
#>
param(
    [string]$MarkdownPath = "",
    [string]$OutputPdfPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($MarkdownPath)) {
    $MarkdownPath = Join-Path $repoRoot "docs\manual_de_usuario.md"
}
if ([string]::IsNullOrWhiteSpace($OutputPdfPath)) {
    $OutputPdfPath = Join-Path $repoRoot "docs\manual_de_usuario.pdf"
}

if (-not (Test-Path $MarkdownPath)) {
    throw "No se encontró el archivo de manual en: $MarkdownPath"
}

Write-Host "==> Convirtiendo manual de usuario a PDF..." -ForegroundColor Cyan
Write-Host "    Entrada: $MarkdownPath" -ForegroundColor DarkGray
Write-Host "    Salida:  $OutputPdfPath" -ForegroundColor DarkGray

# 1. Localizar ejecutable de Microsoft Edge o Google Chrome
$browserCandidates = @(
    "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    "C:\Program Files\Google\Chrome\Application\chrome.exe",
    "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
)

$browserPath = $null
foreach ($cand in $browserCandidates) {
    if (Test-Path $cand) {
        $browserPath = $cand
        break
    }
}

if ($null -eq $browserPath) {
    # Intentar buscar en PATH
    $edgeInPath = Get-Command msedge.exe -ErrorAction SilentlyContinue
    if ($edgeInPath) { $browserPath = $edgeInPath.Source }
    else {
        $chromeInPath = Get-Command chrome.exe -ErrorAction SilentlyContinue
        if ($chromeInPath) { $browserPath = $chromeInPath.Source }
    }
}

if ($null -eq $browserPath) {
    Write-Warning "No se encontró Microsoft Edge ni Chrome para convertir HTML a PDF. Se mantendrá el archivo .md."
    exit 0
}

# 2. Leer contenido Markdown
$mdContent = Get-Content -Path $MarkdownPath -Raw -Encoding utf8

# 3. Convertidor robusto de Markdown a HTML estilizado
function Convert-MarkdownToHtmlBody([string]$md) {
    $lines = $md -split "`r?`n"
    $html = [System.Text.StringBuilder]::new()
    
    $inCodeBlock = $false
    $inTable = $false
    $inList = $false
    $codeLang = ""

    foreach ($line in $lines) {
        # Bloques de Código ```
        if ($line -match '^```([a-zA-Z0-9_-]*)') {
            if (-not $inCodeBlock) {
                if ($inList) { [void]$html.AppendLine("</ul>"); $inList = $false }
                if ($inTable) { [void]$html.AppendLine("</tbody></table></div>"); $inTable = $false }
                $inCodeBlock = $true
                $codeLang = $matches[1]
                [void]$html.AppendLine("<pre class='code-block'><code class='$codeLang'>")
            } else {
                $inCodeBlock = $false
                [void]$html.AppendLine("</code></pre>")
            }
            continue
        }

        if ($inCodeBlock) {
            $encoded = [System.Net.WebUtility]::HtmlEncode($line)
            [void]$html.AppendLine($encoded)
            continue
        }

        # Tablas Markdown | col | col |
        if ($line -match '^\|(.+)\|$') {
            if ($inList) { [void]$html.AppendLine("</ul>"); $inList = $false }
            $cells = $line.Trim('|').Split('|') | ForEach-Object { $_.Trim() }
            
            # Separador de tabla |---|---|
            if ($line -match '^\|[\s\-:]+\|\s*$') {
                continue
            }

            if (-not $inTable) {
                $inTable = $true
                [void]$html.AppendLine("<div class='table-container'><table><thead><tr>")
                foreach ($c in $cells) {
                    [void]$html.Append("<th>$(Format-InlineMd $c)</th>")
                }
                [void]$html.AppendLine("</tr></thead><tbody>")
            } else {
                [void]$html.Append("<tr>")
                foreach ($c in $cells) {
                    [void]$html.Append("<td>$(Format-InlineMd $c)</td>")
                }
                [void]$html.AppendLine("</tr>")
            }
            continue
        } else {
            if ($inTable) {
                [void]$html.AppendLine("</tbody></table></div>")
                $inTable = $false
            }
        }

        # Listas desordenadas (- o *)
        if ($line -match '^\s*[-*]\s+(.+)$') {
            if (-not $inList) {
                $inList = $true
                [void]$html.AppendLine("<ul>")
            }
            $itemContent = Format-InlineMd $matches[1]
            [void]$html.AppendLine("<li>$itemContent</li>")
            continue
        } else {
            if ($inList) {
                [void]$html.AppendLine("</ul>")
                $inList = $false
            }
        }

        # Encabezados (#, ##, ###, ####)
        if ($line -match '^(#{1,6})\s+(.+)$') {
            $level = $matches[1].Length
            $headerText = Format-InlineMd $matches[2]
            $slug = ($matches[2].ToLower() -replace '[^a-z0-9\s-]', '' -replace '\s+', '-')
            [void]$html.AppendLine("<h$level id='$slug'>$headerText</h$level>")
            continue
        }

        # Líneas horizontales ---
        if ($line -match '^---+$') {
            [void]$html.AppendLine("<hr />")
            continue
        }

        # Blockquotes >
        if ($line -match '^>\s*(.+)$') {
            $quoteText = Format-InlineMd $matches[1]
            [void]$html.AppendLine("<blockquote class='callout'><p>$quoteText</p></blockquote>")
            continue
        }

        # Párrafos normales
        if (-not [string]::IsNullOrWhiteSpace($line)) {
            $formatted = Format-InlineMd $line
            [void]$html.AppendLine("<p>$formatted</p>")
        }
    }

    if ($inCodeBlock) { [void]$html.AppendLine("</code></pre>") }
    if ($inTable) { [void]$html.AppendLine("</tbody></table></div>") }
    if ($inList) { [void]$html.AppendLine("</ul>") }

    return $html.ToString()
}

function Format-InlineMd([string]$text) {
    if ([string]::IsNullOrEmpty($text)) { return "" }
    
    # 1. Enlaces Markdown [texto](url)
    $text = [regex]::Replace($text, '\[(.*?)\]\((.*?)\)', '<a href="$2">$1</a>')
    
    # 2. Código inline `code`
    $text = [regex]::Replace($text, '`([^`]+)`', '<code class="inline-code">$1</code>')
    
    # 3. Negrita **texto**
    $text = [regex]::Replace($text, '\*\*([^*]+)\*\*', '<strong>$1</strong>')
    
    # 4. Cursiva *texto*
    $text = [regex]::Replace($text, '\*([^*]+)\*', '<em>$1</em>')
    
    return $text
}

$bodyHtml = Convert-MarkdownToHtmlBody $mdContent

# 4. Plantilla HTML completa con CSS para impresión profesional
$fullHtml = @"
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8">
    <title>FileFlow Studio - Manual de Usuario</title>
    <style>
        @page {
            size: A4;
            margin: 20mm 15mm 20mm 15mm;
            @bottom-right {
                content: counter(page);
            }
        }
        
        * {
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
            font-size: 10.5pt;
            line-height: 1.6;
            color: #1F2937;
            background-color: #FFFFFF;
            margin: 0;
            padding: 0;
        }

        h1, h2, h3, h4 {
            color: #111827;
            font-weight: 700;
            line-height: 1.25;
            page-break-after: avoid;
        }

        h1 {
            font-size: 22pt;
            border-bottom: 2.5px solid #6366F1;
            padding-bottom: 6px;
            margin-top: 15pt;
            margin-bottom: 12pt;
            color: #4F46E5;
        }

        h2 {
            font-size: 15pt;
            border-bottom: 1px solid #E5E7EB;
            padding-bottom: 4px;
            margin-top: 18pt;
            margin-bottom: 8pt;
            color: #374151;
            page-break-before: auto;
        }

        h3 {
            font-size: 12pt;
            margin-top: 14pt;
            margin-bottom: 6pt;
            color: #4B5563;
        }

        p {
            margin-top: 0;
            margin-bottom: 8pt;
            text-align: justify;
        }

        a {
            color: #4F46E5;
            text-decoration: none;
        }

        hr {
            border: 0;
            height: 1px;
            background: #E5E7EB;
            margin: 14pt 0;
        }

        ul, ol {
            margin-top: 0;
            margin-bottom: 8pt;
            padding-left: 20pt;
        }

        li {
            margin-bottom: 3pt;
        }

        blockquote.callout {
            margin: 10pt 0;
            padding: 8pt 14pt;
            background-color: #F3F4F6;
            border-left: 4px solid #6366F1;
            border-radius: 4px;
            color: #374151;
            page-break-inside: avoid;
        }

        .code-block {
            background-color: #1E293B;
            color: #F8FAFC;
            padding: 10pt 14pt;
            border-radius: 6px;
            font-family: "Cascadia Code", Consolas, "Courier New", monospace;
            font-size: 9pt;
            line-height: 1.45;
            overflow-x: auto;
            margin: 8pt 0;
            page-break-inside: avoid;
        }

        .inline-code {
            background-color: #F1F5F9;
            color: #0F172A;
            padding: 1.5pt 4pt;
            border-radius: 4px;
            font-family: "Cascadia Code", Consolas, "Courier New", monospace;
            font-size: 9pt;
            border: 1px solid #E2E8F0;
        }

        .table-container {
            width: 100%;
            margin: 10pt 0;
            page-break-inside: avoid;
        }

        table {
            width: 100%;
            border-collapse: collapse;
            font-size: 9.5pt;
        }

        th {
            background-color: #4F46E5;
            color: #FFFFFF;
            font-weight: 600;
            text-align: left;
            padding: 6pt 8pt;
            border: 1px solid #4338CA;
        }

        td {
            padding: 5pt 8pt;
            border: 1px solid #E5E7EB;
            vertical-align: top;
        }

        tr:nth-child(even) td {
            background-color: #F9FAFB;
        }

        .header-cover {
            text-align: center;
            padding: 25pt 10pt;
            margin-bottom: 20pt;
            background: linear-gradient(135deg, #EEF2FF 0%, #E0E7FF 100%);
            border-radius: 8px;
            border: 1px solid #C7D2FE;
        }

        .header-cover h1 {
            border-bottom: none;
            font-size: 26pt;
            margin: 0;
            color: #3730A3;
        }

        .header-cover p {
            text-align: center;
            font-size: 12pt;
            color: #4338CA;
            margin-top: 6pt;
        }
    </style>
</head>
<body>
    <div class="header-cover">
        <h1>⚡ FileFlow Studio</h1>
        <p><strong>Manual de Usuario y Guía de Referencia Completa</strong></p>
        <p style="font-size: 9.5pt; color: #6366F1; margin-top: 4pt;">Motor Visual de Automatización DAG en .NET 9 y C# 13</p>
    </div>

    $bodyHtml
</body>
</html>
"@

# 5. Guardar archivo HTML temporal
$tempHtmlPath = [System.IO.Path]::ChangeExtension($OutputPdfPath, ".temp.html")
[System.IO.File]::WriteAllText($tempHtmlPath, $fullHtml, [System.Text.Encoding]::UTF8)

# 6. Ejecutar Microsoft Edge en modo headless para generar PDF
try {
    $outDir = Split-Path -Parent $OutputPdfPath
    if (-not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    }

    $edgeArgs = @(
        "--headless=new",
        "--disable-gpu",
        "--no-sandbox",
        "--disable-dev-shm-usage",
        "--no-pdf-header-footer",
        "--print-to-pdf=`"$OutputPdfPath`"",
        "`"$tempHtmlPath`""
    )

    Write-Host "==> Renderizando PDF con motor Chromium ($browserPath)..." -ForegroundColor Cyan
    $process = Start-Process -FilePath $browserPath -ArgumentList $edgeArgs -NoNewWindow -Wait -PassThru

    Start-Sleep -Milliseconds 500

    if (Test-Path $OutputPdfPath) {
        $fileSize = (Get-Item $OutputPdfPath).Length / 1KB
        Write-Host "==> PDF generado con éxito: $OutputPdfPath ($([math]::Round($fileSize, 1)) KB)" -ForegroundColor Green
    } else {
        throw "No se generó el archivo PDF en $OutputPdfPath. (Código de salida: $($process.ExitCode))"
    }
}
finally {
    Start-Sleep -Milliseconds 300
    try {
        if (Test-Path $tempHtmlPath) {
            [System.IO.File]::Delete($tempHtmlPath)
        }
    } catch {}
}
