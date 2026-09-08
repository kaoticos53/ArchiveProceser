using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FileFlow.Sdk.SyntheticData;

namespace FileFlow.Plugin.FileSystem.Services;

/// <summary>
/// Parser y serializador bidireccional para el lenguaje DSL rápido de definición de árboles sintéticos,
/// soportando indentación, rutas anidadas, tamaños en unidades legibles (KB, MB, GB), metadatos clave/valor
/// y simulación de entradas de archivos comprimidos.
/// </summary>
public static class SyntheticTreeDslParser
{
    private static readonly Regex ArchivePattern = new(@"\[archive:\s*(?<entries>[^\]]+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parsea texto DSL multilínea a una lista de definiciones de archivos y carpetas sintéticas.
    /// </summary>
    public static List<SyntheticFileDefinition> Parse(string dslText)
    {
        var result = new List<SyntheticFileDefinition>();
        if (string.IsNullOrWhiteSpace(dslText)) return result;

        var lines = dslText.Split(['\r', '\n'], StringSplitOptions.None);
        var dirStack = new Stack<(int indent, string path)>();

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            string trimmed = rawLine.Trim();
            if (trimmed.StartsWith('#') || trimmed.StartsWith("//")) continue;

            int indent = GetIndentLevel(rawLine);

            while (dirStack.Count > 0 && dirStack.Peek().indent >= indent)
            {
                dirStack.Pop();
            }

            string currentParent = dirStack.Count > 0 ? dirStack.Peek().path : string.Empty;

            // Revisar si es una línea de archivo comprimido con [archive: ...]
            List<SyntheticArchiveEntryDefinition> archiveEntries = [];
            string lineToParse = trimmed;
            var match = ArchivePattern.Match(trimmed);
            if (match.Success)
            {
                string rawEntries = match.Groups["entries"].Value;
                archiveEntries = ParseArchiveEntries(rawEntries);
                lineToParse = trimmed.Remove(match.Index, match.Length).Trim();
            }

            // Separar partes por '|'
            var tokens = lineToParse.Split('|', StringSplitOptions.TrimEntries);
            string pathToken = tokens[0].Trim();
            if (string.IsNullOrWhiteSpace(pathToken)) continue;

            bool isFolderDeclaration = pathToken.EndsWith('/') || pathToken.EndsWith('\\');
            string cleanPathToken = pathToken.TrimEnd('/', '\\');

            string combinedRelativePath = string.IsNullOrEmpty(currentParent)
                ? cleanPathToken
                : $"{currentParent}/{cleanPathToken}";

            long sizeBytes = 1024;
            var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (string.IsNullOrWhiteSpace(token)) continue;

                int eqIdx = token.IndexOf('=');
                if (eqIdx > 0)
                {
                    string key = token[..eqIdx].Trim();
                    string val = token[(eqIdx + 1)..].Trim();

                    if (key.Equals("size", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("bytes", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("tam", StringComparison.OrdinalIgnoreCase))
                    {
                        sizeBytes = ParseSize(val);
                    }
                    else
                    {
                        metadata[key] = val;
                    }
                }
                else
                {
                    // Si es un token sin igual, comprobar si es tamaño directo (ej: '15MB')
                    if (TryParseSize(token, out long parsedSize))
                    {
                        sizeBytes = parsedSize;
                    }
                    else
                    {
                        metadata[token] = true;
                    }
                }
            }

            var item = new SyntheticFileDefinition
            {
                RelativePath = combinedRelativePath,
                IsDirectory = isFolderDeclaration,
                FileSizeBytes = isFolderDeclaration ? 0 : sizeBytes,
                Metadata = metadata,
                SimulatedArchiveEntries = archiveEntries
            };

            result.Add(item);

            if (isFolderDeclaration)
            {
                dirStack.Push((indent, combinedRelativePath));
            }
        }

        return result;
    }

    /// <summary>
    /// Serializa una colección de definiciones de archivo a texto DSL legible y editable.
    /// </summary>
    public static string Serialize(IReadOnlyList<SyntheticFileDefinition> items)
    {
        if (items == null || items.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var knownFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Agrupar o recorrer en orden jerárquico
        foreach (var item in items)
        {
            string dir = item.Directory;
            if (!string.IsNullOrWhiteSpace(dir) && !knownFolders.Contains(dir))
            {
                // Emitir carpetas intermedias
                var parts = dir.Split('/');
                string currentAccum = string.Empty;
                for (int p = 0; p < parts.Length; p++)
                {
                    currentAccum = string.IsNullOrEmpty(currentAccum) ? parts[p] : $"{currentAccum}/{parts[p]}";
                    if (knownFolders.Add(currentAccum))
                    {
                        string indent = new(' ', p * 4);
                        sb.AppendLine($"{indent}{parts[p]}/");
                    }
                }
            }

            int depth = string.IsNullOrWhiteSpace(dir) ? 0 : dir.Split('/').Length;
            string itemIndent = new(' ', depth * 4);

            if (item.IsDirectory)
            {
                sb.AppendLine($"{itemIndent}{item.FileName}/");
                continue;
            }

            sb.Append($"{itemIndent}{item.FileName}");

            // Agregar tamaño si es distinto de cero
            if (item.FileSizeBytes > 0)
            {
                sb.Append($" | size={FormatSize(item.FileSizeBytes)}");
            }

            // Metadatos
            foreach (var kvp in item.Metadata)
            {
                if (kvp.Key.Equals("Category", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals("VirtualSample", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals("IsVirtual", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sb.Append($" | {kvp.Key}={kvp.Value}");
            }

            // Entradas simuladas de comprimido
            if (item.SimulatedArchiveEntries.Count > 0)
            {
                sb.Append(" [archive: ");
                var entriesStr = item.SimulatedArchiveEntries.Select(e =>
                    e.FileSizeBytes > 0
                        ? $"{e.InnerPath} | size={FormatSize(e.FileSizeBytes)}"
                        : e.InnerPath);
                sb.Append(string.Join("; ", entriesStr));
                sb.Append(']');
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static List<SyntheticArchiveEntryDefinition> ParseArchiveEntries(string rawEntries)
    {
        var result = new List<SyntheticArchiveEntryDefinition>();
        var entries = rawEntries.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var entryStr in entries)
        {
            var tokens = entryStr.Split('|', StringSplitOptions.TrimEntries);
            string innerPath = tokens[0].Trim().Replace('\\', '/');
            long size = 1024;
            var meta = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < tokens.Length; i++)
            {
                string t = tokens[i].Trim();
                int eq = t.IndexOf('=');
                if (eq > 0)
                {
                    string k = t[..eq].Trim();
                    string v = t[(eq + 1)..].Trim();
                    if (k.Equals("size", StringComparison.OrdinalIgnoreCase))
                    {
                        size = ParseSize(v);
                    }
                    else
                    {
                        meta[k] = v;
                    }
                }
                else if (TryParseSize(t, out long s))
                {
                    size = s;
                }
            }

            result.Add(new SyntheticArchiveEntryDefinition
            {
                InnerPath = innerPath,
                FileSizeBytes = size,
                IsDirectory = innerPath.EndsWith('/'),
                Metadata = meta
            });
        }

        return result;
    }

    private static int GetIndentLevel(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ') count++;
            else if (c == '\t') count += 4;
            else break;
        }
        return count;
    }

    public static long ParseSize(string sizeStr)
    {
        if (TryParseSize(sizeStr, out long size))
        {
            return size;
        }
        return 1024;
    }

    public static bool TryParseSize(string sizeStr, out long bytes)
    {
        bytes = 0;
        if (string.IsNullOrWhiteSpace(sizeStr)) return false;

        string trimmed = sizeStr.Trim().ToUpperInvariant();
        double multiplier = 1;

        if (trimmed.EndsWith("TB"))
        {
            multiplier = 1024L * 1024 * 1024 * 1024;
            trimmed = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith("GB"))
        {
            multiplier = 1024L * 1024 * 1024;
            trimmed = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith("MB"))
        {
            multiplier = 1024L * 1024;
            trimmed = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith("KB"))
        {
            multiplier = 1024;
            trimmed = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith('B'))
        {
            multiplier = 1;
            trimmed = trimmed[..^1].Trim();
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double num) ||
            double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out num))
        {
            bytes = (long)(num * multiplier);
            return true;
        }

        return false;
    }

    public static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024 * 1024 * 1024 => $"{(double)bytes / (1024L * 1024 * 1024 * 1024):0.##}TB",
            >= 1024L * 1024 * 1024 => $"{(double)bytes / (1024L * 1024 * 1024):0.##}GB",
            >= 1024L * 1024 => $"{(double)bytes / (1024L * 1024):0.##}MB",
            >= 1024 => $"{(double)bytes / 1024:0.##}KB",
            _ => $"{bytes}B"
        };
    }
}
