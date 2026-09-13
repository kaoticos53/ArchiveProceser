using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FileFlow.App.Preview.Core;

namespace FileFlow.App.Preview.Providers;

public class TextCodePreviewProvider : IFilePreviewProvider
{
    public string ProviderName => "Text & Code Previewer";
    public int Priority => 80;

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json", ".xml", ".log", ".md", ".cs", ".js", ".py", ".sql", ".html", ".css",
        ".yml", ".yaml", ".ini", ".config", ".csv", ".tsv", ".bat", ".ps1", ".sh", ".srt", ".vtt"
    };

    public bool CanHandle(FilePreviewContext context)
    {
        return _supportedExtensions.Contains(context.Extension);
    }

    public async Task<Control> CreateVisualElementAsync(FilePreviewContext context, CancellationToken cancellationToken)
    {
        var grid = new Grid { Background = new SolidColorBrush(Color.Parse("#0F1117")) };

        var editor = new TextBox
        {
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas, Cascadia Code, Courier New, monospace"),
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#E1E4EA")),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            Padding = new Thickness(12)
        };

        string ext = context.Extension.ToLowerInvariant();

        if (File.Exists(context.CurrentPath))
        {
            try
            {
                var fi = new FileInfo(context.CurrentPath);
                if (fi.Length > 2 * 1024 * 1024) // > 2 MB: Lectura truncada
                {
                    using var reader = new StreamReader(context.CurrentPath);
                    var sb = new System.Text.StringBuilder();
                    string? line;
                    int lineCount = 0;
                    while (lineCount < 500 && (line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
                    {
                        sb.AppendLine(line);
                        lineCount++;
                    }
                    sb.AppendLine($"\n... [Archivo grande ({fi.Length / 1024.0:F1} KB): mostrando las primeras {lineCount} líneas] ...");
                    editor.Text = sb.ToString();
                }
                else
                {
                    string content = await File.ReadAllTextAsync(context.CurrentPath, cancellationToken).ConfigureAwait(false);

                    // Embellecer JSON si aplica
                    if (ext == ".json" && content.Length < 1024 * 1024)
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(content);
                            content = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
                        }
                        catch { }
                    }

                    editor.Text = content;
                }
            }
            catch (Exception ex)
            {
                editor.Text = $"Error al leer el archivo: {ex.Message}";
            }
        }

        grid.Children.Add(editor);
        return grid;
    }
}
