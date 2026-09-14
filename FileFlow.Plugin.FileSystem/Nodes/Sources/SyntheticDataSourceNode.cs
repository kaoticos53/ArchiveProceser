using System.IO;
using System.Text.Json;
using System.Windows;
using FileFlow.Plugin.FileSystem.Services;
using FileFlow.Plugin.FileSystem.UI.Services;
using FileFlow.Plugin.FileSystem.UI.Views;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.SyntheticData;
using FileFlow.Sdk.VirtualFileSystem;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("SyntheticDataSourceNode_Name", "Files", "SyntheticDataSourceNode_Desc", PipelineRole.Source,
    "testing", "pruebas", "sintetico", "mock", "dataset", "peliculas", "series", "musica", "comics", "dummy")]
public sealed class SyntheticDataSourceNode : IFlowNode, INodeCustomActionProvider
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SyntheticDataSourceNode_Name", "Generador de Datos de Prueba");
    public string Category => "Files";
    public string Description => LocalizationManager.Instance.GetString("SyntheticDataSourceNode_Desc", "Emite archivos de prueba categorizados (Películas, Series, Cómics, Música o Personalizados) para pruebas y depuración de pipelines sin requerir archivos reales.");

    public IReadOnlyList<NodePort> Inputs { get; } = Array.Empty<NodePort>();

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Category"] = "Películas",
        ["EmissionMode"] = "Virtual",
        ["MaxItems"] = 0,
        ["EmissionDelayMs"] = 0,
        ["EmitDirectories"] = false,
        ["CustomItems"] = "",
        ["OutputFolder"] = ""
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Category", ParameterEditorType.Dropdown, DefaultValue: "Películas", DisplayOrder: 1, Options: ["Todas", "Películas", "Series", "Cómics y Manga", "Música", "Fotos", "Documentos", "Personalizada"]),
        new("EmissionMode", ParameterEditorType.Dropdown, DefaultValue: "Virtual", DisplayOrder: 2, Options: ["Virtual", "PhysicalMock"]),
        new("MaxItems", ParameterEditorType.Number, DefaultValue: 0, DisplayOrder: 3, Min: 0, Max: 1000),
        new("EmissionDelayMs", ParameterEditorType.Number, DefaultValue: 0, DisplayOrder: 4, Min: 0, Max: 10000),
        new("EmitDirectories", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 5),
        new("CustomItems", ParameterEditorType.MultiLineText, DefaultValue: "", DisplayOrder: 6),
        new("OutputFolder", ParameterEditorType.FolderPath, DefaultValue: "", DisplayOrder: 7)
    ];

    public IReadOnlyList<NodeActionDescriptor> CustomActions =>
    [
        new("OpenDataSetDesigner", "📊 Diseñador de Datasets...", "📊", "Abrir el Diseñador Visual de Datasets Sintéticos para crear, editar o importar conjuntos de datos ficticios")
    ];

    public void ExecuteCustomAction(string actionId, object? context = null)
    {
        if (string.Equals(actionId, "OpenDataSetDesigner", StringComparison.OrdinalIgnoreCase))
        {
            var window = new SyntheticDataSetDesignerWindow();
            if (context is Window ownerWindow)
            {
                window.Owner = ownerWindow;
            }
            else if (Application.Current?.MainWindow != null)
            {
                window.Owner = Application.Current.MainWindow;
            }
            window.ShowDialog();
        }
    }

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string category = Parameters.TryGetValue("Category", out var catVal) && catVal != null ? catVal.ToString()! : "Películas";
        string emissionMode = Parameters.TryGetValue("EmissionMode", out var modeVal) && modeVal != null ? modeVal.ToString()! : "Virtual";
        int maxItems = Parameters.TryGetValue("MaxItems", out var maxVal) ? ParameterHelper.GetInt32(maxVal, 0) : 0;
        int delayMs = Parameters.TryGetValue("EmissionDelayMs", out var delayVal) ? ParameterHelper.GetInt32(delayVal, 0) : 0;
        bool emitDirectories = Parameters.TryGetValue("EmitDirectories", out var edVal) && ParameterHelper.GetBoolean(edVal, false);
        string customText = Parameters.TryGetValue("CustomItems", out var custVal) && custVal != null ? custVal.ToString()! : string.Empty;
        string outputFolder = Parameters.TryGetValue("OutputFolder", out var outFoldVal) && outFoldVal != null ? outFoldVal.ToString()! : string.Empty;

        List<SyntheticFileDefinition> definitions = [];

        // 1. Intentar cargar dataset personalizado por nombre o ID desde el servicio de almacenamiento
        var customDs = SyntheticDataSetStorageService.Instance.GetDataSetByName(category)
                       ?? SyntheticDataSetStorageService.Instance.GetDataSetById(category);

        if (customDs != null)
        {
            definitions = customDs.Items.Select(i => i.Clone()).ToList();
        }
        else if (string.Equals(category, "Personalizada", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(customText))
        {
            // Parsear con el parser DSL (soporta tanto árbol indentado como líneas simples de texto)
            definitions = SyntheticTreeDslParser.Parse(customText);
            if (definitions.Count == 0)
            {
                var lines = customText.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var line in lines)
                {
                    definitions.Add(new SyntheticFileDefinition(line, 1024, false, new Dictionary<string, object?>
                    {
                        ["Category"] = "Personalizada",
                        ["VirtualSample"] = true
                    }));
                }
            }
        }
        else
        {
            // Catálogo oficial de muestras
            var sampleItems = RenamerSampleDataProvider.GetSampleItemsByCategory(category, out _);
            foreach (var s in sampleItems)
            {
                string fn = Path.GetFileName(s.CurrentPath);
                string relPath = $"{category}/{fn}";
                definitions.Add(new SyntheticFileDefinition(relPath, s.FileSizeBytes > 0 ? s.FileSizeBytes : 1024 * 1024, s.IsDirectory, s.Metadata));
            }
        }

        // Filtrar directorios si no se solicitan explícitamente
        if (!emitDirectories)
        {
            definitions = definitions.Where(d => !d.IsDirectory).ToList();
        }

        if (maxItems > 0 && definitions.Count > maxItems)
        {
            definitions = definitions.Take(maxItems).ToList();
        }

        string baseFolder = string.IsNullOrWhiteSpace(outputFolder)
            ? (string.Equals(emissionMode, "PhysicalMock", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(Path.GetTempPath(), "FileFlow_MockData", Guid.NewGuid().ToString("N"))
                : Path.Combine(@"C:\Muestras", category))
            : outputFolder;

        if (string.Equals(emissionMode, "PhysicalMock", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(baseFolder);
        }

        List<FileItemContext> itemsToEmit = new(definitions.Count);

        foreach (var def in definitions)
        {
            string relPath = def.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.Combine(baseFolder, relPath);

            var sampleItem = new FileItemContext(fullPath, def.IsDirectory)
            {
                FileSizeBytes = def.FileSizeBytes
            };

            sampleItem.Metadata["RelativePath"] = def.RelativePath;
            sampleItem.Metadata["RelativeDir"] = def.Directory;
            sampleItem.Metadata["VirtualSample"] = true;
            sampleItem.Metadata["IsVirtual"] = string.Equals(emissionMode, "Virtual", StringComparison.OrdinalIgnoreCase);

            foreach (var kvp in def.Metadata)
            {
                sampleItem.Metadata[kvp.Key] = kvp.Value;
            }

            if (!sampleItem.Metadata.ContainsKey("Category"))
            {
                sampleItem.Metadata["Category"] = category;
            }

            // Simulación de archivo comprimido
            if (def.IsArchive || def.SimulatedArchiveEntries.Count > 0)
            {
                sampleItem.Metadata["IsArchive"] = true;
                sampleItem.Metadata["Archive:Type"] = Path.GetExtension(fullPath).TrimStart('.').ToUpperInvariant();
                if (def.SimulatedArchiveEntries.Count > 0)
                {
                    sampleItem.Metadata["Archive:Entries"] = JsonSerializer.Serialize(def.SimulatedArchiveEntries);
                }
            }

            if (string.Equals(emissionMode, "PhysicalMock", StringComparison.OrdinalIgnoreCase))
            {
                if (def.IsDirectory)
                {
                    Directory.CreateDirectory(fullPath);
                }
                else
                {
                    string? parentDir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    // Si es ZIP físico, generar un zip real válido con System.IO.Compression
                    if (def.IsArchive && string.Equals(Path.GetExtension(fullPath), ".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!File.Exists(fullPath))
                        {
                            using var fs = new FileStream(fullPath, FileMode.Create);
                            using var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create);
                            if (def.SimulatedArchiveEntries.Count > 0)
                            {
                                foreach (var archEntry in def.SimulatedArchiveEntries)
                                {
                                    if (archEntry.IsDirectory)
                                    {
                                        zip.CreateEntry(archEntry.InnerPath.TrimEnd('/') + "/");
                                    }
                                    else
                                    {
                                        var ze = zip.CreateEntry(archEntry.InnerPath);
                                        using var entryStream = ze.Open();
                                        var bytes = System.Text.Encoding.UTF8.GetBytes($"Mock content for {archEntry.InnerPath}");
                                        entryStream.Write(bytes, 0, bytes.Length);
                                    }
                                }
                            }
                            else
                            {
                                var ze = zip.CreateEntry("mock_file.txt");
                                using var entryStream = ze.Open();
                                var bytes = System.Text.Encoding.UTF8.GetBytes($"Mock content in {Path.GetFileName(fullPath)}");
                                entryStream.Write(bytes, 0, bytes.Length);
                            }
                        }
                    }
                    else
                    {
                        if (!File.Exists(fullPath))
                        {
                            await File.WriteAllTextAsync(fullPath, $"FileFlow Mock Content for {def.FileName}", cancellationToken);
                        }
                    }
                }

                sampleItem.CurrentPath = fullPath;
                sampleItem.OriginalPath = fullPath;
            }
            else
            {
                // Modo Virtual (VFS)
                if (context.VirtualFileSystem != null)
                {
                    string? parentDir = Path.GetDirectoryName(fullPath) ?? string.Empty;
                    var vfe = new VirtualFileEntry(
                        VirtualPath: fullPath,
                        OriginalPath: fullPath,
                        FileName: Path.GetFileName(fullPath),
                        Extension: def.IsDirectory ? string.Empty : Path.GetExtension(fullPath),
                        DirectoryPath: parentDir,
                        FileSizeBytes: def.FileSizeBytes,
                        OperationType: VirtualOperationType.Original,
                        Role: VirtualFileRole.Source,
                        SourceNodeName: Name,
                        SourceNodeId: Id,
                        Metadata: sampleItem.Metadata,
                        ExecutionLog: sampleItem.ExecutionLog,
                        TimestampUtc: DateTime.UtcNow
                    );
                    context.VirtualFileSystem.AddOrUpdateFile(vfe);
                }
            }

            itemsToEmit.Add(sampleItem);
        }

        context.Log(
            LocalizationManager.Instance.GetFormattedString(
                "Log_SyntheticSource_Emitting",
                "[Generador de Datos] Emitiendo {0} archivos sintéticos (Categoría: '{1}', Modo: '{2}').",
                itemsToEmit.Count,
                category,
                emissionMode),
            LogLevel.Information);

        foreach (var sample in itemsToEmit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            await context.EmitAsync("Out", sample);
        }
    }
}
