using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FileFlow.Plugin.Documents;

[NodeDefinition("PdfMetadataNode_Name", "Documents", "PdfMetadataNode_Desc", PipelineRole.Analyze,
    "pdf", "metadatos", "autor", "titulo", "asunto", "palabras clave", "metadata")]
public sealed class PdfMetadataNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("PdfMetadataNode_Name", "Metadatos de PDF (PDF Metadata)");
    public string Category => "Documents";
    public string Description => LocalizationManager.Instance.GetString("PdfMetadataNode_Desc", "Inspecciona y actualiza los metadatos de documentos PDF (Título, Autor, Asunto, Palabras Clave).");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UpdateMetadata"] = false,
        ["Title"] = "",
        ["Author"] = "",
        ["Subject"] = "",
        ["Keywords"] = "",
        ["OutputDirectory"] = "{GlobalOutputDir}"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("UpdateMetadata", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 1),
        new("Title", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 2),
        new("Author", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 3),
        new("Subject", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 4),
        new("Keywords", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 5),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 6)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var storage = context.GetStorage();
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken))
        {
            await context.EmitAsync("Out", item);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath);
        if (!ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            await context.EmitAsync("Out", item);
            return;
        }

        bool update = Parameters.TryGetValue("UpdateMetadata", out var updObj) && ParameterHelper.GetBoolean(updObj, false);

        if (!update)
        {
            // Solo lectura de metadatos
            await using var inStream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken);
            using var pdfDoc = PdfReader.Open(inStream, PdfDocumentOpenMode.Import);
            item.Metadata["Pdf:Title"] = pdfDoc.Info.Title;
            item.Metadata["Pdf:Author"] = pdfDoc.Info.Author;
            item.Metadata["Pdf:Subject"] = pdfDoc.Info.Subject;
            item.Metadata["Pdf:Keywords"] = pdfDoc.Info.Keywords;
            item.Metadata["Pdf:PageCount"] = pdfDoc.PageCount;
            item.Metadata["Pdf:CreationDate"] = pdfDoc.Info.CreationDate;

            await context.EmitAsync("Out", item);
            return;
        }

        // Actualización y exportación
        string rawOutDir = Parameters.TryGetValue("OutputDirectory", out var outDirObj) ? ParameterHelper.GetString(outDirObj, "{GlobalOutputDir}") : "{GlobalOutputDir}";
        string outDir = ParameterHelper.ResolveOutputPath(rawOutDir, item);
        if (!await storage.DirectoryExistsAsync(outDir, cancellationToken))
        {
            await storage.CreateDirectoryAsync(outDir, cancellationToken);
        }

        string destPath = Path.Combine(outDir, Path.GetFileName(item.CurrentPath));

        await using (var inStream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken))
        using (var pdfDoc = PdfReader.Open(inStream, PdfDocumentOpenMode.Modify))
        {
            if (Parameters.TryGetValue("Title", out var title) && !string.IsNullOrWhiteSpace(title?.ToString()))
            {
                pdfDoc.Info.Title = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(title.ToString()!, item);
            }
            if (Parameters.TryGetValue("Author", out var author) && !string.IsNullOrWhiteSpace(author?.ToString()))
            {
                pdfDoc.Info.Author = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(author.ToString()!, item);
            }
            if (Parameters.TryGetValue("Subject", out var subj) && !string.IsNullOrWhiteSpace(subj?.ToString()))
            {
                pdfDoc.Info.Subject = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(subj.ToString()!, item);
            }
            if (Parameters.TryGetValue("Keywords", out var kw) && !string.IsNullOrWhiteSpace(kw?.ToString()))
            {
                pdfDoc.Info.Keywords = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(kw.ToString()!, item);
            }

            await using var outStream = await storage.OpenWriteAsync(destPath, cancellationToken);
            pdfDoc.Save(outStream);
        }

        long destSizeBytes = await storage.FileExistsAsync(destPath, cancellationToken)
            ? await storage.GetFileSizeAsync(destPath, cancellationToken)
            : 0;

        var resultContext = new FileItemContext(destPath)
        {
            OriginalPath = item.OriginalPath,
            FileSizeBytes = destSizeBytes
        };

        foreach (var (k, v) in item.Metadata)
        {
            resultContext.Metadata[k] = v;
        }

        await context.EmitAsync("Out", resultContext);
    }
}
