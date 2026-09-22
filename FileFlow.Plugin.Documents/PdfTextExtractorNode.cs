using System.IO;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using UglyToad.PdfPig;

namespace FileFlow.Plugin.Documents;

[NodeDefinition("PdfTextExtractorNode_Name", "Documents", "PdfTextExtractorNode_Desc", PipelineRole.Analyze,
    "pdf", "texto", "extraer", "ocr", "txt", "leer", "text", "extract")]
public sealed class PdfTextExtractorNode : FlowNodeBase
{
    static PdfTextExtractorNode()
    {
        FileFlowFontResolver.EnsureInitialized();
    }

    public override string Name => LocalizationManager.Instance.GetString("PdfTextExtractorNode_Name", "Extraer Texto de PDF (PDF Text Extractor)");
    public override string Category => "Documents";
    public override string Description => LocalizationManager.Instance.GetString("PdfTextExtractorNode_Desc", "Extrae el contenido textual de documentos PDF para indexación, búsqueda o exportación.");

    public PdfTextExtractorNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("TextFile", typeof(FileItemContext), PortDirection.Output, "TextFile")
        ];

        Parameters["ExportTextFile"] = false;
        Parameters["OutputDirectory"] = "{GlobalOutputDir}";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("ExportTextFile", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 1),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 2)
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

        var sb = new StringBuilder();
        int pageCount = 0;

        await using (var stream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken))
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(stream))
        {
            pageCount = pdf.NumberOfPages;
            foreach (var page in pdf.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                sb.AppendLine(page.Text);
            }
        }

        string extractedText = sb.ToString();
        item.Metadata["PdfText"] = extractedText;
        item.Metadata["PdfPageCount"] = pageCount;
        item.Metadata["PdfWordCount"] = extractedText.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

        await context.EmitAsync("Out", item);

        bool exportTxt = GetParameter("ExportTextFile", false);
        if (exportTxt)
        {
            string rawOutDir = GetParameter("OutputDirectory", "{GlobalOutputDir}");
            string outDir = ParameterHelper.ResolveOutputPath(rawOutDir, item);
            if (!await storage.DirectoryExistsAsync(outDir, cancellationToken))
            {
                await storage.CreateDirectoryAsync(outDir, cancellationToken);
            }

            string txtFileName = Path.GetFileNameWithoutExtension(item.CurrentPath) + ".txt";
            string txtFilePath = Path.Combine(outDir, txtFileName);

            await storage.WriteAllTextAsync(txtFilePath, extractedText, ct: cancellationToken);

            var txtContext = new FileItemContext(txtFilePath)
            {
                OriginalPath = item.OriginalPath
            };

            foreach (var (k, v) in item.Metadata)
            {
                txtContext.Metadata[k] = v;
            }

            await context.EmitAsync("TextFile", txtContext);
        }
    }
}
