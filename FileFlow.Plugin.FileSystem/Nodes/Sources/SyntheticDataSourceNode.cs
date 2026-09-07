using System.IO;
using FileFlow.Plugin.FileSystem.UI.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("SyntheticDataSourceNode_Name", "Files", "SyntheticDataSourceNode_Desc", PipelineRole.Source,
    "testing", "pruebas", "sintetico", "mock", "dataset", "peliculas", "series", "musica", "comics", "dummy")]
public class SyntheticDataSourceNode : IFlowNode
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
        ["CustomItems"] = "",
        ["OutputFolder"] = ""
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Category", ParameterEditorType.Dropdown, DefaultValue: "Películas", DisplayOrder: 1, Options: ["Todas", "Películas", "Series", "Cómics y Manga", "Música", "Fotos", "Documentos", "Personalizada"]),
        new("EmissionMode", ParameterEditorType.Dropdown, DefaultValue: "Virtual", DisplayOrder: 2, Options: ["Virtual", "PhysicalMock"]),
        new("MaxItems", ParameterEditorType.Number, DefaultValue: 0, DisplayOrder: 3, Min: 0, Max: 1000),
        new("EmissionDelayMs", ParameterEditorType.Number, DefaultValue: 0, DisplayOrder: 4, Min: 0, Max: 10000),
        new("CustomItems", ParameterEditorType.MultiLineText, DefaultValue: "", DisplayOrder: 5),
        new("OutputFolder", ParameterEditorType.FolderPath, DefaultValue: "", DisplayOrder: 6)
    ];

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
        string customText = Parameters.TryGetValue("CustomItems", out var custVal) && custVal != null ? custVal.ToString()! : string.Empty;
        string outputFolder = Parameters.TryGetValue("OutputFolder", out var outFoldVal) && outFoldVal != null ? outFoldVal.ToString()! : string.Empty;

        List<FileItemContext> itemsToEmit;

        if (string.Equals(category, "Personalizada", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(customText))
        {
            var lines = customText.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            itemsToEmit = new List<FileItemContext>(lines.Length);
            foreach (var line in lines)
            {
                var customItem = new FileItemContext(Path.Combine(@"C:\Muestras\Personalizadas", line), false)
                {
                    FileSizeBytes = 1024
                };
                customItem.Metadata["Category"] = "Personalizada";
                customItem.Metadata["VirtualSample"] = true;
                itemsToEmit.Add(customItem);
            }
        }
        else
        {
            itemsToEmit = RenamerSampleDataProvider.GetSampleItemsByCategory(category, out _);
        }

        if (maxItems > 0 && itemsToEmit.Count > maxItems)
        {
            itemsToEmit = itemsToEmit.Take(maxItems).ToList();
        }

        // Si es modo físico mock, crear archivos temporales reales de prueba
        if (string.Equals(emissionMode, "PhysicalMock", StringComparison.OrdinalIgnoreCase))
        {
            string baseFolder = string.IsNullOrWhiteSpace(outputFolder)
                ? Path.Combine(Path.GetTempPath(), "FileFlow_MockData", Guid.NewGuid().ToString("N"))
                : outputFolder;

            Directory.CreateDirectory(baseFolder);

            foreach (var it in itemsToEmit)
            {
                string safeFileName = Path.GetFileName(it.CurrentPath);
                string physicalPath = Path.Combine(baseFolder, safeFileName);
                if (!File.Exists(physicalPath))
                {
                    await File.WriteAllTextAsync(physicalPath, $"FileFlow Mock Content for {safeFileName}", cancellationToken);
                }

                it.CurrentPath = physicalPath;
                it.OriginalPath = physicalPath;
            }
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
