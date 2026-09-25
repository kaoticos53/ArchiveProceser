using System.IO;
using FileFlow.Sdk;

namespace FileFlow.App.Preview.Core;

/// <summary>
/// Contexto con información y metadatos del archivo a previsualizar.
/// Soporta comparación entre archivo original y procesado.
/// </summary>
public class FilePreviewContext
{
    public string CurrentPath { get; set; } = string.Empty;
    public string? OriginalPath { get; set; }
    public string FileName => Path.GetFileName(CurrentPath);
    public long FileSizeBytes { get; set; }
    public string Extension => Path.GetExtension(CurrentPath).ToLowerInvariant();
    public bool HasOriginalComparison => !string.IsNullOrWhiteSpace(OriginalPath) &&
                                         !OriginalPath.Equals(CurrentPath, StringComparison.OrdinalIgnoreCase) &&
                                         File.Exists(OriginalPath);

    public Dictionary<string, object> Metadata { get; } = new(StringComparer.OrdinalIgnoreCase);

    public FilePreviewContext(string currentPath, string? originalPath = null)
    {
        CurrentPath = currentPath;
        OriginalPath = originalPath;

        if (File.Exists(currentPath))
        {
            try
            {
                FileSizeBytes = new FileInfo(currentPath).Length;
            }
            catch { }
        }
    }

    public static FilePreviewContext FromFileItemContext(FileItemContext item)
    {
        var ctx = new FilePreviewContext(item.CurrentPath, item.OriginalPath)
        {
            FileSizeBytes = item.FileSizeBytes
        };

        foreach (var (k, v) in item.Metadata)
        {
            if (v != null) ctx.Metadata[k] = v;
        }

        return ctx;
    }
}
