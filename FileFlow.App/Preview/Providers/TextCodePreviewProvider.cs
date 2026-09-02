using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileFlow.App.Preview.Core;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

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

    public async Task<FrameworkElement> CreateVisualElementAsync(FilePreviewContext context, CancellationToken cancellationToken)
    {
        var grid = new Grid { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F1117")) };

        var editor = new TextEditor
        {
            IsReadOnly = true,
            ShowLineNumbers = true,
            FontFamily = new FontFamily("Consolas, Cascadia Code, Courier New"),
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1E4EA")),
            LineNumbersForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C6370")),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(8)
        };

        // Asignar resaltador sintáctico según extensión
        string ext = context.Extension.ToLowerInvariant();
        editor.SyntaxHighlighting = ext switch
        {
            ".cs" => HighlightingManager.Instance.GetDefinition("C#"),
            ".js" => HighlightingManager.Instance.GetDefinition("JavaScript"),
            ".html" or ".htm" => HighlightingManager.Instance.GetDefinition("HTML"),
            ".xml" or ".config" => HighlightingManager.Instance.GetDefinition("XML"),
            _ => null
        };

        if (File.Exists(context.CurrentPath))
        {
            try
            {
                var fi = new FileInfo(context.CurrentPath);
                if (fi.Length > 2 * 1024 * 1024) // > 2 MB: Lectura truncada
                {
                    using var reader = new StreamReader(context.CurrentPath);
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < 500 && !reader.EndOfStream; i++)
                    {
                        sb.AppendLine(await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false));
                    }
                    sb.AppendLine($"\n... [Archivo grande ({fi.Length / 1024.0:F1} KB): mostrando las primeras 500 líneas] ...");
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
