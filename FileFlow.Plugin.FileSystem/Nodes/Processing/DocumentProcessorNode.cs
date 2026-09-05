using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("DocumentProcessorNode_Name", "Documents", "DocumentProcessorNode_Desc", PipelineRole.Analyze,
    "documento", "lineas", "conteo", "tipo", "extension", "doc", "pdf", "txt", "stats")]
public class DocumentProcessorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("DocumentProcessorNode_Name", "Document & PDF Processor");
    public string Category => "Documents";
    public string Description => LocalizationManager.Instance.GetString("DocumentProcessorNode_Desc", "Inspects documents and PDF files, counting pages and extracting key metadata.");

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

        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_DocProcessor_NotFound", "[Document Processor] File not found: '{0}'", filePath), LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            int estimatedPageCount = 1;
            int lineCount = 0;

            if (ext == ".pdf")
            {
                estimatedPageCount = EstimatePdfPageCount(filePath);
            }
            else if (ext is ".txt" or ".log" or ".json" or ".xml" or ".md" or ".csv")
            {
                var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
                lineCount = lines.Length;
                estimatedPageCount = Math.Max(1, (lines.Length + 49) / 50); // 50 lines per page
            }

            string docType = ext.TrimStart('.').ToUpperInvariant();
            item.Metadata["DocumentType"] = docType;
            item.Metadata["EstimatedPageCount"] = estimatedPageCount;
            item.Metadata["DocumentLineCount"] = lineCount;
            item.AddLog($"DocumentProcessorNode inspected {filePath} ({estimatedPageCount} pages)");

            sw.Stop();
            string detailsJson = $"{{\"documentType\": \"{docType}\", \"estimatedPages\": {estimatedPageCount}, \"lineCount\": {lineCount}, \"fileSizeBytes\": {item.FileSizeBytes}}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_DocProcessor_Analyzed", "[Document Processor] Document analyzed ({0}): ~{1} pages, {2:N0} lines", docType, estimatedPageCount, lineCount), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"file\": \"{filePath.Replace("\\", "\\\\")}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_DocProcessor_Error", "[Document Processor] Error processing document: {0}", ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
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
