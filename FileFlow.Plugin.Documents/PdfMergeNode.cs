using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FileFlow.Plugin.Documents;

[NodeDefinition("PdfMergeNode_Name", "Documents", "PdfMergeNode_Desc", PipelineRole.Transform,
    "pdf", "unir", "fusionar", "juntar", "combinar", "merge", "join")]
public sealed class PdfMergeNode : IFlowNode
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
        var storage = context.GetStorage();
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken))
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

    public async Task OnWorkflowCompletedAsync(
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        List<string> pdfsToMerge;
        lock (_lock)
        {
            if (_collectedPdfPaths.Count == 0) return;
            pdfsToMerge = new List<string>(_collectedPdfPaths);
            _collectedPdfPaths.Clear();
        }

        string outDir = Parameters.TryGetValue("OutputDirectory", out var dVal) ? ParameterHelper.GetString(dVal, "{GlobalOutputDir}") : "{GlobalOutputDir}";
        string outFileName = Parameters.TryGetValue("OutputFileName", out var fVal) ? ParameterHelper.GetString(fVal, "Merged_Document.pdf") : "Merged_Document.pdf";

        var dummyItem = new FileItemContext(string.Empty);
        string resolvedDir = ParameterHelper.ResolveOutputPath(outDir, dummyItem);
        string resolvedName = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(outFileName, dummyItem);
        string destinationPath = Path.Combine(resolvedDir, resolvedName);
        var storage = context.GetStorage();

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
            await context.EmitAsync("Out", dryItem);
            return;
        }

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await MergePdfFilesAsync(pdfsToMerge, destinationPath, storage, cancellationToken);
            sw.Stop();

            long outSize = await storage.FileExistsAsync(destinationPath, cancellationToken)
                ? await storage.GetFileSizeAsync(destinationPath, cancellationToken)
                : 0;
            var mergedItem = new FileItemContext(destinationPath)
            {
                FileSizeBytes = outSize
            };
            mergedItem.Metadata["MergedPdfCount"] = pdfsToMerge.Count;
            mergedItem.AddLog($"PDFs combinados exitosamente ({pdfsToMerge.Count} archivos) en '{destinationPath}'");

            context.Log($"[PDF Merge] {pdfsToMerge.Count} PDFs unidos exitosamente en '{destinationPath}' ({outSize} bytes)", LogLevel.Information, mergedItem, durationMs: sw.Elapsed.TotalMilliseconds);
            await context.EmitAsync("Out", mergedItem);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"[PDF Merge] Error al unir PDFs: {ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// Combina una lista explícita de rutas PDF en un archivo destino utilizando la abstracción de almacenamiento.
    /// </summary>
    public static async Task<string> MergePdfFilesAsync(
        IEnumerable<string> pdfPaths,
        string destinationPath,
        IStorageService storage,
        CancellationToken ct = default)
    {
        string? destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destDir) && !await storage.DirectoryExistsAsync(destDir, ct))
        {
            await storage.CreateDirectoryAsync(destDir, ct);
        }

        using var outputDocument = new PdfDocument();

        foreach (string pdfPath in pdfPaths)
        {
            if (!await storage.FileExistsAsync(pdfPath, ct)) continue;

            await using var inStream = await storage.OpenReadAsync(pdfPath, ct);
            using var inputDocument = PdfReader.Open(inStream, PdfDocumentOpenMode.Import);
            int count = inputDocument.PageCount;
            for (int idx = 0; idx < count; idx++)
            {
                PdfPage page = inputDocument.Pages[idx];
                outputDocument.AddPage(page);
            }
        }

        await using var outStream = await storage.OpenWriteAsync(destinationPath, ct);
        outputDocument.Save(outStream);
        return destinationPath;
    }

    /// <summary>
    /// Combina una lista explícita de rutas PDF en un archivo destino (sobrecarga síncrona / fallback).
    /// </summary>
    [Obsolete("Utilice MergePdfFilesAsync en su lugar para evitar llamadas síncronas bloqueantes.", false)]
    public static string MergePdfFiles(IEnumerable<string> pdfPaths, string destinationPath) =>
        MergePdfFilesAsync(pdfPaths, destinationPath, NullStorageService.Instance).GetAwaiter().GetResult();
}
