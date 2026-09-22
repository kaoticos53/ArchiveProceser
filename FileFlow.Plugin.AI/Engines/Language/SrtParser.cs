using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Utilidades de reconocimiento y traducción estructural de subtítulos en formato SRT.
/// Preserva números de secuencia y marcas de tiempo, traduciendo únicamente el texto visible.
/// </summary>
internal static class SrtParser
{
    private static readonly Regex TimestampRegex = new(
        @"^\d{2}:\d{2}:\d{2}[,\.]\d{3}\s*-->\s*\d{2}:\d{2}:\d{2}[,\.]\d{3}",
        RegexOptions.Compiled);

    private static readonly Regex NumberOnlyRegex = new(@"^\d+$", RegexOptions.Compiled);

    /// <summary>
    /// Determina heurísticamente si un bloque de texto tiene forma de contenido SRT.
    /// </summary>
    public static bool LooksLikeSrt(string text)
    {
        return text.Contains("-->") && Regex.IsMatch(text, @"\d{2}:\d{2}:\d{2}[,\.]\d{3}");
    }

    /// <summary>
    /// Recorre el contenido SRT línea por línea, preservando números de secuencia y marcas de
    /// tiempo intactos, y delegando la traducción de cada línea de texto visible al callback dado.
    /// </summary>
    public static async Task<string> TranslateContentAsync(
        string srtContent,
        Func<string, CancellationToken, Task<string>> translateLine,
        CancellationToken cancellationToken)
    {
        var lines = srtContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var outputLines = new List<string>(lines.Length);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed) || NumberOnlyRegex.IsMatch(trimmed) || TimestampRegex.IsMatch(trimmed))
            {
                // Es un número de secuencia, marca de tiempo o línea en blanco: preservar intacta
                outputLines.Add(line);
            }
            else
            {
                // Es texto de subtítulo: traducir
                string translated = await translateLine(trimmed, cancellationToken).ConfigureAwait(false);
                outputLines.Add(translated);
            }
        }

        return string.Join(Environment.NewLine, outputLines);
    }
}
