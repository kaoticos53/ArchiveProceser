using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FileFlow.Plugin.Documents;

[NodeDefinition("PdfMergeNode_Name", "Documents", "PdfMergeNode_Desc", PipelineRole.Transform,
    "pdf", "unir", "fusionar", "juntar", "combinar", "merge", "join")]
public sealed class PdfMergeNode : FlowNodeBase
{
    static PdfMergeNode()
    {
        FileFlowFontResolver.EnsureInitialized();
    }

    public override string Name => LocalizationManager.Instance.GetString("PdfMergeNode_Name", "Unir PDFs (PDF Merge)");
    public override string Category => "Documents";
    public override string Description => LocalizationManager.Instance.GetString("PdfMergeNode_Desc", "Combina múltiples documentos PDF en un único archivo PDF consolidado.");

    public PdfMergeNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("PassThrough", typeof(FileItemContext), PortDirection.Output, "PassThrough")
        ];

        Parameters["OutputDirectory"] = "{GlobalOutputDir}";
        Parameters["OutputFileName"] = "Merged_Document.pdf";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 1),
        new("OutputFileName", ParameterEditorType.Text, DefaultValue: "Merged_Document.pdf", DisplayOrder: 2)
    ];

    private readonly List<string> _collectedPdfPaths = [];
    private readonly Lock _lock = new();
    private string? _lastExecutionId;

    /// <summary>
    /// Dónde se escribe el PDF consolidado, resuelto <b>con un elemento de verdad</b> mientras el flujo corre.
    ///
    /// <para>El PDF se escribe al terminar la ejecución, y ahí ya no hay elemento: el nodo resolvía las plantillas
    /// contra un elemento vacío, así que la carpeta de salida del flujo no se veía —caía en la de los ajustes— y
    /// cualquier variable del nombre (`{FileName}`) se quedaba sin valor. Se resuelve una vez, con el primer PDF que
    /// entra, y se guarda para el cierre.</para>
    /// </summary>
    private string? _resolvedOutputDirectory;
    private string? _resolvedOutputFileName;

    public override async Task ExecuteAsync(
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
                    _resolvedOutputDirectory = null;
                    _resolvedOutputFileName = null;
                }
                _collectedPdfPaths.Add(item.CurrentPath);
                _resolvedOutputDirectory = ParameterHelper.ResolveOutputPath(
                    GetParameter("OutputDirectory", "{GlobalOutputDir}"), item);
                _resolvedOutputFileName = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(
                    GetParameter("OutputFileName", "Merged_Document.pdf"), item);
            }
        }

        await context.EmitAsync("PassThrough", item);
    }

    public override async Task OnWorkflowCompletedAsync(
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

        // Con lo que se resolvió al recoger los PDFs (elemento de verdad); si el cierre llega sin nada recogido
        // —una llamada directa al cierre, sin ejecución— se resuelve como antes, contra un elemento vacío.
        string? resolvedDir = _resolvedOutputDirectory;
        string? resolvedName = _resolvedOutputFileName;
        if (string.IsNullOrWhiteSpace(resolvedDir) || string.IsNullOrWhiteSpace(resolvedName))
        {
            var dummyItem = new FileItemContext(string.Empty);
            resolvedDir = ParameterHelper.ResolveOutputPath(GetParameter("OutputDirectory", "{GlobalOutputDir}"), dummyItem);
            resolvedName = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(
                GetParameter("OutputFileName", "Merged_Document.pdf"), dummyItem);
        }

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
    /// Sobrecarga síncrona eliminada. Use MergePdfFilesAsync en su lugar.
    /// </summary>
    [Obsolete("Utilice MergePdfFilesAsync. Este método ha sido eliminado por riesgo de deadlock (sync-over-async).", true)]
    public static string MergePdfFiles(IEnumerable<string> pdfPaths, string destinationPath) =>
        throw new NotSupportedException("Use MergePdfFilesAsync en su lugar.");
}
