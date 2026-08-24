<#
.SYNOPSIS
	Genera assets/FileFlow.ico a partir de un diseño vectorial simple dibujado con System.Drawing.
	No requiere herramientas externas: solo .NET (System.Drawing.Common) disponible en Windows.

.DESCRIPTION
	Dibuja un icono que representa el motor de flujo DAG de FileFlow Studio (nodos conectados)
	sobre un fondo con gradiente, y lo empaqueta como .ico multi-resolución (16/32/48/256 px)
	usando PNG embebido (formato ICO moderno soportado desde Windows Vista).

.EXAMPLE
	./assets/generate-icon.ps1
#>
param(
	[string]$OutputPath = (Join-Path $PSScriptRoot "FileFlow.ico")
)

Add-Type -AssemblyName System.Drawing

function New-FileFlowBitmap {
	param([int]$Size)

	$bmp = New-Object System.Drawing.Bitmap $Size, $Size
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
	$g.Clear([System.Drawing.Color]::Transparent)

	# Fondo: cuadrado redondeado con gradiente (azul oscuro -> cian, estilo "tech/DAG")
	$rect = New-Object System.Drawing.Rectangle 0, 0, $Size, $Size
	$colorStart = [System.Drawing.Color]::FromArgb(255, 15, 23, 42)   # slate-900
	$colorEnd   = [System.Drawing.Color]::FromArgb(255, 8, 145, 178)  # cyan-600
	$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $colorStart, $colorEnd, 45)

	$radius = [Math]::Max(2, [int]($Size * 0.22))
	$path = New-Object System.Drawing.Drawing2D.GraphicsPath
	$d = $radius * 2
	$path.AddArc(0, 0, $d, $d, 180, 90)
	$path.AddArc($Size - $d, 0, $d, $d, 270, 90)
	$path.AddArc($Size - $d, $Size - $d, $d, $d, 0, 90)
	$path.AddArc(0, $Size - $d, $d, $d, 90, 90)
	$path.CloseFigure()
	$g.FillPath($brush, $path)

	# Nodos y conexiones (representación del grafo DAG)
	$penColor = [System.Drawing.Color]::FromArgb(230, 224, 242, 254)
	$pen = New-Object System.Drawing.Pen($penColor, [Math]::Max(1.0, $Size * 0.035))
	$nodeBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 224, 242, 254))
	$nodeBrushAccent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 250, 204, 21)) # amber-400

	# Coordenadas relativas (0..1) de 3 nodos formando un pequeño DAG
	$p1 = New-Object System.Drawing.PointF ($Size * 0.28), ($Size * 0.30)
	$p2 = New-Object System.Drawing.PointF ($Size * 0.74), ($Size * 0.24)
	$p3 = New-Object System.Drawing.PointF ($Size * 0.50), ($Size * 0.72)

	$g.DrawLine($pen, $p1, $p2)
	$g.DrawLine($pen, $p1, $p3)
	$g.DrawLine($pen, $p2, $p3)

	$nodeRadius = [Math]::Max(2.0, $Size * 0.085)
	foreach ($p in @($p1, $p2)) {
		$g.FillEllipse($nodeBrush, $p.X - $nodeRadius, $p.Y - $nodeRadius, $nodeRadius * 2, $nodeRadius * 2)
	}
	# Nodo final destacado en color acento
	$g.FillEllipse($nodeBrushAccent, $p3.X - $nodeRadius, $p3.Y - $nodeRadius, $nodeRadius * 2, $nodeRadius * 2)

	$g.Dispose()
	$brush.Dispose()
	$pen.Dispose()
	$nodeBrush.Dispose()
	$nodeBrushAccent.Dispose()

	return $bmp
}

function ConvertTo-PngBytes {
	param([System.Drawing.Bitmap]$Bitmap)
	$ms = New-Object System.IO.MemoryStream
	$Bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
	return $ms.ToArray()
}

$sizes = @(16, 32, 48, 256)
$images = New-Object System.Collections.Generic.List[object]

foreach ($size in $sizes) {
	$bmp = New-FileFlowBitmap -Size $size
	$pngBytes = ConvertTo-PngBytes -Bitmap $bmp
	Write-Host "Generado tamaño $size -> $($pngBytes.Length) bytes PNG"
	$images.Add([PSCustomObject]@{ Size = $size; Bytes = $pngBytes })
	$bmp.Dispose()
}

# Construcción manual del archivo .ico (ICONDIR + ICONDIRENTRY[] + datos PNG embebidos)
$fs = New-Object System.IO.FileStream $OutputPath, ([System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter $fs

# ICONDIR: reserved(2)=0, type(2)=1 (icon), count(2)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$images.Count)

$headerSize = 6 + (16 * $images.Count)
$offset = $headerSize

foreach ($img in $images) {
	$widthByte = if ($img.Size -ge 256) { 0 } else { [byte]$img.Size }
	$heightByte = if ($img.Size -ge 256) { 0 } else { [byte]$img.Size }
	$bw.Write([byte]$widthByte)   # width (0 = 256)
	$bw.Write([byte]$heightByte)  # height (0 = 256)
	$bw.Write([byte]0)           # color palette
	$bw.Write([byte]0)           # reserved
	$bw.Write([UInt16]1)         # color planes
	$bw.Write([UInt16]32)        # bits per pixel
	$bw.Write([UInt32]$img.Bytes.Length) # tamaño de los datos de imagen
	$bw.Write([UInt32]$offset)           # offset de los datos de imagen
	$offset += $img.Bytes.Length
}

foreach ($img in $images) {
	[byte[]]$imgBytes = $img.Bytes
	$bw.Write($imgBytes, 0, $imgBytes.Length)
}

$bw.Flush()
$bw.Close()
$fs.Close()

Write-Host "Icono generado en: $OutputPath" -ForegroundColor Green
