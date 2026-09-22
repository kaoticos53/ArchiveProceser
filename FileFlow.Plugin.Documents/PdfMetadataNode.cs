using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FileFlow.Plugin.Documents;

[NodeDefinition("PdfMetadataNode_Name", "Documents", "PdfMetadataNode_Desc", PipelineRole.Analyze,
    "pdf", "metadatos", "autor", "titulo", "asunto", "palabras clave", "metadata")]
public sealed class PdfMetadataNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("PdfMetadataNode_Name", "Metadatos de PDF (PDF Metadata)");
    public override string Category => "Documents";
    public override string Description => LocalizationManager.Instance.GetString("PdfMetadataNode_Desc", "Inspecciona y actualiza los metadatos de documentos PDF (Título, Autor, Asunto, Palabras Clave).");

    public PdfMetadataNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
        ];

        Parameters["UpdateMetadata"] = false;
        Parameters["Title"] = "";
        Parameters["Author"] = "";
        Parameters["Subject"] = "";
        Parameters["Keywords"] = "";
        Parameters["OutputDirectory"] = "{GlobalOutputDir}";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("UpdateMetadata", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 1),
        new("Title", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 2),
        new("Author", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 3),
        new("Subject", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 4),
        new("Keywords", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 5),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 6)
    ];

    public override async Task ExecuteAsync(
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

        bool update = GetParameter("UpdateMetadata", false);

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
        string rawOutDir = GetParameter("OutputDirectory", "{GlobalOutputDir}");
        string outDir = ParameterHelper.ResolveOutputPath(rawOutDir, item);
        if (!await storage.DirectoryExistsAsync(outDir, cancellationToken))
        {
            await storage.CreateDirectoryAsync(outDir, cancellationToken);
        }

        string destPath = Path.Combine(outDir, Path.GetFileName(item.CurrentPath));

        await using (var inStream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken))
        using (var pdfDoc = PdfReader.Open(inStream, PdfDocumentOpenMode.Modify))
        {
            string title = GetParameter("Title", string.Empty);
            if (!string.IsNullOrWhiteSpace(title))
            {
                pdfDoc.Info.Title = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(title, item);
            }

            string author = GetParameter("Author", string.Empty);
            if (!string.IsNullOrWhiteSpace(author))
            {
                pdfDoc.Info.Author = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(author, item);
            }

            string subject = GetParameter("Subject", string.Empty);
            if (!string.IsNullOrWhiteSpace(subject))
            {
                pdfDoc.Info.Subject = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(subject, item);
            }

            string keywords = GetParameter("Keywords", string.Empty);
            if (!string.IsNullOrWhiteSpace(keywords))
            {
                pdfDoc.Info.Keywords = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(keywords, item);
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
