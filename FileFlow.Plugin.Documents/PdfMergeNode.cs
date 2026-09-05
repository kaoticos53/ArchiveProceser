using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FileFlow.Plugin.Documents;

[NodeDefinition("PdfMergeNode_Name", "Documents", "PdfMergeNode_Desc", PipelineRole.Transform,
    "pdf", "unir", "fusionar", "juntar", "combinar", "merge", "join")]
public class PdfMergeNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("PdfMergeNode_Name", "Unir PDFs (PDF Merge)");
    public string Category => "Documents";
    public string Description => LocalizationManager.Instance.GetString("PdfMergeNode_Desc", "Combina múltiples documentos PDF en un único archivo PDF consolidado.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("PassThrough", typeof(FileItemContext), PortDirection.Output, "PassThrough")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OutputDirectory"] = "{GlobalOutputDir}",
        ["OutputFileName"] = "Merged_Document.pdf"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 1),
        new("OutputFileName", ParameterEditorType.Text, DefaultValue: "Merged_Document.pdf", DisplayOrder: 2)
    ];

    private readonly List<string> _collectedPdfPaths = [];
    private readonly Lock _lock = new();
    private string? _lastExecutionId;

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            await context.EmitAsync("PassThrough", item);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath);
        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            string executionId = item.Metadata.TryGetValue("WorkflowExecutionId", out var idObj) ? idObj?.ToString() ?? string.Empty : string.Empty;
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(executionId) && _lastExecutionId != executionId)
                {
                    _lastExecutionId = executionId;
                    _collectedPdfPaths.Clear();
                }
                _collectedPdfPaths.Add(item.CurrentPath);
            }
        }

        await context.EmitAsync("PassThrough", item);
    }

    public Task OnWorkflowCompletedAsync(
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        List<string> pdfsToMerge;
        lock (_lock)
        {
            if (_collectedPdfPaths.Count == 0) return Task.CompletedTask;
            pdfsToMerge = new List<string>(_collectedPdfPaths);
            _collectedPdfPaths.Clear();
        }

        string outDir = Parameters.TryGetValue("OutputDirectory", out var dVal) ? ParameterHelper.GetString(dVal, "{GlobalOutputDir}") : "{GlobalOutputDir}";
        string outFileName = Parameters.TryGetValue("OutputFileName", out var fVal) ? ParameterHelper.GetString(fVal, "Merged_Document.pdf") : "Merged_Document.pdf";

        var dummyItem = new FileItemContext(string.Empty);
        string resolvedDir = ParameterHelper.ResolveOutputPath(outDir, dummyItem);
        string resolvedName = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(outFileName, dummyItem);
        string destinationPath = Path.Combine(resolvedDir, resolvedName);

        if (context.IsDryRun)
        {
            context.RegisterPlannedAction(new PlannedAction(
                Guid.NewGuid(),
                Id,
                Name,
                PlannedOperationType.Custom,
                string.Join(", ", pdfsToMerge),
                destinationPath,
                $"[DryRun] Unir {pdfsToMerge.Count} archivos PDF en '{destinationPath}'"
            ));
            var dryItem = new FileItemContext(destinationPath) { FileSizeBytes = 0 };
            dryItem.Metadata["MergedPdfCount"] = pdfsToMerge.Count;
            dryItem.AddLog($"[DryRun] Planned PDF Merge: {destinationPath} ({pdfsToMerge.Count} files)");
            return context.EmitAsync("Out", dryItem);
        }

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            MergePdfFiles(pdfsToMerge, destinationPath);
            sw.Stop();

            long outSize = File.Exists(destinationPath) ? new FileInfo(destinationPath).Length : 0;
            var mergedItem = new FileItemContext(destinationPath)
            {
                FileSizeBytes = outSize
            };
            mergedItem.Metadata["MergedPdfCount"] = pdfsToMerge.Count;
            mergedItem.AddLog($"PDFs combinados exitosamente ({pdfsToMerge.Count} archivos) en '{destinationPath}'");

            context.Log($"[PDF Merge] {pdfsToMerge.Count} PDFs unidos exitosamente en '{destinationPath}' ({outSize} bytes)", LogLevel.Information, mergedItem, durationMs: sw.Elapsed.TotalMilliseconds);
            return context.EmitAsync("Out", mergedItem);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"[PDF Merge] Error al unir PDFs: {ex.Message}", LogLevel.Error);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Combina una lista explícita de rutas PDF en un archivo destino.
    /// </summary>
    public static string MergePdfFiles(IEnumerable<string> pdfPaths, string destinationPath)
    {
        string? destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        using var outputDocument = new PdfDocument();

        foreach (string pdfPath in pdfPaths)
        {
            if (!File.Exists(pdfPath)) continue;

            using var inputDocument = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
            int count = inputDocument.PageCount;
            for (int idx = 0; idx < count; idx++)
            {
                PdfPage page = inputDocument.Pages[idx];
                outputDocument.AddPage(page);
            }
        }

        outputDocument.Save(destinationPath);
        return destinationPath;
    }
}
