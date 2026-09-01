using System.IO;
using System.Windows;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.App.Services;

/// <summary>
/// Proveedor de muestras de datos (reales de disco o sintéticas de prueba) para previsualización en vivo.
/// </summary>
public static class RenamerSampleDataProvider
{
    public static List<FileItemContext> GetSampleItems(NodeViewModel nodeViewModel, out string sourceDescription)
    {
        var editorNodes = nodeViewModel.ParentEditor?.Nodes?.ToList()
            ?? (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm ? mainVm.Editor.Nodes.ToList() : new List<NodeViewModel>());
        var connections = nodeViewModel.ParentEditor?.Connections?.ToList()
            ?? (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm2 ? mainVm2.Editor.Connections.ToList() : new List<ConnectionViewModel>());

        // 1. Buscar FolderSourceNode (priorizando nodos conectados aguas arriba)
        var folderNode = FindUpstreamFolderSourceNode(nodeViewModel, connections)
            ?? editorNodes.FirstOrDefault(n => n.IsFolderSourceNode);

        if (folderNode != null)
        {
            string rawSourcePath = folderNode.Parameters.FirstOrDefault(p => p.Key.Equals("SourcePath", StringComparison.OrdinalIgnoreCase))?.Value?.ToString()
                ?? (folderNode.NodeInstance.Parameters.TryGetValue("SourcePath", out var sp) ? sp?.ToString() : null)
                ?? string.Empty;

            bool recursive = true;
            var recParam = folderNode.Parameters.FirstOrDefault(p => p.Key.Equals("Recursive", StringComparison.OrdinalIgnoreCase));
            if (recParam != null && bool.TryParse(recParam.Value?.ToString(), out bool recVal))
            {
                recursive = recVal;
            }
            else if (folderNode.NodeInstance.Parameters.TryGetValue("Recursive", out var rObj) && rObj is bool rBool)
            {
                recursive = rBool;
            }

            string emitMode = folderNode.Parameters.FirstOrDefault(p => p.Key.Equals("EmitMode", StringComparison.OrdinalIgnoreCase))?.Value?.ToString()
                ?? (folderNode.NodeInstance.Parameters.TryGetValue("EmitMode", out var emObj) ? emObj?.ToString() : "FilesOnly")
                ?? "FilesOnly";

            if (!string.IsNullOrWhiteSpace(rawSourcePath))
            {
                string resolvedPath = VariableTemplateResolver.Resolve(rawSourcePath, new FileItemContext(Directory.GetCurrentDirectory()));
                if (!Path.IsPathRooted(resolvedPath))
                {
                    resolvedPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), resolvedPath));
                }

                if (Directory.Exists(resolvedPath))
                {
                    var items = new List<FileItemContext>();
                    var enumOptions = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = recursive,
                        ReturnSpecialDirectories = false
                    };

                    try
                    {
                        if (emitMode.Equals("DirectoriesOnly", StringComparison.OrdinalIgnoreCase))
                        {
                            var dirs = Directory.EnumerateDirectories(resolvedPath, "*", enumOptions).Take(100);
                            foreach (var dir in dirs)
                            {
                                items.Add(new FileItemContext(dir, isDirectory: true));
                            }
                        }
                        else
                        {
                            var files = Directory.EnumerateFiles(resolvedPath, "*", enumOptions).Take(100);
                            foreach (var file in files)
                            {
                                items.Add(new FileItemContext(file, isDirectory: false));
                            }

                            if (emitMode.Equals("FilesAndDirectories", StringComparison.OrdinalIgnoreCase) && items.Count < 100)
                            {
                                int remaining = 100 - items.Count;
                                var dirs = Directory.EnumerateDirectories(resolvedPath, "*", enumOptions).Take(remaining);
                                foreach (var dir in dirs)
                                {
                                    items.Add(new FileItemContext(dir, isDirectory: true));
                                }
                            }
                        }
                    }
                    catch
                    {
                        // En caso de error de lectura I/O, fallback a muestras predefinidas
                    }

                    if (items.Count > 0)
                    {
                        string dirDisplayName = Path.GetFileName(resolvedPath);
                        if (string.IsNullOrWhiteSpace(dirDisplayName)) dirDisplayName = resolvedPath;
                        sourceDescription = $"(Muestra de {items.Count} archivo(s) real(es) de '{dirDisplayName}')";
                        return items;
                    }
                }
            }
        }

        sourceDescription = "(Muestras sintéticas predefinidas)";
        var syntheticSamples = new[]
        {
            ("1 - pepe.jpg", new Dictionary<string, object?> { ["DateTaken"] = new DateTime(2026, 8, 15) }),
            ("2 - jaco.jpg", new Dictionary<string, object?> { ["DateTaken"] = new DateTime(2026, 8, 15) }),
            ("10 - kilo.jpg", new Dictionary<string, object?> { ["DateTaken"] = new DateTime(2026, 8, 15) }),
            ("serie guapa 1x1.mov", new Dictionary<string, object?> { ["Show"] = "Serie Guapa" }),
            ("serie guapa papo 1x2.mov", new Dictionary<string, object?> { ["Show"] = "Serie Guapa" }),
            ("serie guapa jose 1x10.mov", new Dictionary<string, object?> { ["Show"] = "Serie Guapa" }),
            ("DSC_0042.JPG", new Dictionary<string, object?> { ["Exif:CameraModel"] = "SonyA7", ["DateTaken"] = new DateTime(2026, 8, 15), ["CameraModel"] = "SonyA7" }),
            ("01 - Daft Punk [Live 2007].mp3", new Dictionary<string, object?> { ["Audio:Artist"] = "Daft Punk", ["Audio:Title"] = "Around The World", ["Audio:TrackNumber"] = "01" }),
            ("informe_financiero_borrador_v1.docx", new Dictionary<string, object?> { ["Year"] = "2026", ["DirName"] = "Contabilidad", ["ParentDir"] = "Contabilidad", ["Hash:SHA256"] = "a1b2c3d4e5f6" })
        };

        var result = new List<FileItemContext>();
        foreach (var (fileName, metadata) in syntheticSamples)
        {
            var item = new FileItemContext(Path.Combine(@"C:\Muestras", fileName));
            foreach (var (k, v) in metadata) item.Metadata[k] = v;
            result.Add(item);
        }
        return result;
    }

    private static NodeViewModel? FindUpstreamFolderSourceNode(NodeViewModel targetNode, IEnumerable<ConnectionViewModel> connections)
    {
        var visited = new HashSet<NodeViewModel>();
        var queue = new Queue<NodeViewModel>();
        queue.Enqueue(targetNode);
        var connList = connections.ToList();

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            var incoming = connList.Where(c => c.Target.NodeOwner == curr).ToList();
            foreach (var conn in incoming)
            {
                var upstream = conn.Source.NodeOwner;
                if (upstream.IsFolderSourceNode)
                {
                    return upstream;
                }
                if (visited.Add(upstream))
                {
                    queue.Enqueue(upstream);
                }
            }
        }
        return null;
    }
}
