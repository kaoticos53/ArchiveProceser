using System.IO;
using FileFlow.Sdk;

namespace FileFlow.Plugin.Network;

/// <summary>
/// Helper interno para resolver variables de contexto en rutas remotas y nombres de archivo de red.
/// </summary>
public static class NetworkTemplateHelper
{
    public static string ResolveRemotePath(string template, FileItemContext item)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return item.FileName;
        }

        var now = DateTime.Now;
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(item.FileName);
        string extension = Path.GetExtension(item.FileName);

        string result = template
            .Replace("{FileName}", item.FileName, StringComparison.OrdinalIgnoreCase)
            .Replace("{FileNameWithoutExtension}", fileNameWithoutExt, StringComparison.OrdinalIgnoreCase)
            .Replace("{FileBaseName}", fileNameWithoutExt, StringComparison.OrdinalIgnoreCase)
            .Replace("{Extension}", extension, StringComparison.OrdinalIgnoreCase)
            .Replace("{Year}", now.Year.ToString("D4"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Month}", now.Month.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Day}", now.Day.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Date}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Hour}", now.Hour.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Minute}", now.Minute.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Second}", now.Second.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{OriginalDirectoryName}", !string.IsNullOrWhiteSpace(item.OriginalPath) ? Path.GetFileName(Path.GetDirectoryName(item.OriginalPath)) ?? string.Empty : string.Empty, StringComparison.OrdinalIgnoreCase);

        // Resolver metadatos personalizados si existen
        if (item.Metadata != null && item.Metadata.Count > 0)
        {
            foreach (var kvp in item.Metadata)
            {
                if (kvp.Value != null)
                {
                    result = result.Replace($"{{{kvp.Key}}}", kvp.Value.ToString(), StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        return result.Replace('\\', '/');
    }
}
