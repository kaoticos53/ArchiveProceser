using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("DocumentProcessorNode_Name", "MediaDocs", "DocumentProcessorNode_Desc")]
public class DocumentProcessorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("DocumentProcessorNode_Name", "Document & PDF Processor");
    public string Category => "MediaDocs";
    public string Description => LocalizationManager.Instance.GetString("DocumentProcessorNode_Desc", "Inspecciona documentos y archivos PDF, contando páginas y extrayendo metadatos clave.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Operation"] = "InspectMetadata", // InspectMetadata, ExtractTextSummary
        ["ExtractPageCount"] = true
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string filePath = item.CurrentPath;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            context.Log($"DocumentProcessorNode: File '{filePath}' not found.", LogLevel.Warning);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            context.Log($"DocumentProcessorNode processing file: {filePath}", LogLevel.Information);

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            int estimatedPageCount = 1;

            if (ext == ".pdf")
            {
                estimatedPageCount = EstimatePdfPageCount(filePath);
            }
            else if (ext is ".txt" or ".log" or ".json" or ".xml" or ".md" or ".csv")
            {
                var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
                estimatedPageCount = Math.Max(1, (lines.Length + 49) / 50); // 50 lines per page
            }

            item.Metadata["DocumentType"] = ext.TrimStart('.').ToUpperInvariant();
            item.Metadata["EstimatedPageCount"] = estimatedPageCount;
            item.Metadata["DocumentLineCount"] = ext is ".txt" or ".log" or ".json" or ".xml" or ".md" or ".csv" ? File.ReadAllLines(filePath).Length : 0;
            item.AddLog($"DocumentProcessorNode inspected {filePath} ({estimatedPageCount} pages)");

            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            context.Log($"DocumentProcessorNode Error: {ex.Message}", LogLevel.Error);
            item.AddLog($"DocumentProcessorNode error: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static int EstimatePdfPageCount(string pdfPath)
    {
        try
        {
            string content = File.ReadAllText(pdfPath);
            var matches = System.Text.RegularExpressions.Regex.Matches(content, @"/Type\s*/Page\b");
            if (matches.Count > 0)
            {
                return matches.Count;
            }
        }
        catch { }
        return 1;
    }
}
