using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FileFlow.Plugin.Documents;

[NodeDefinition("PdfSplitNode_Name", "Documents", "PdfSplitNode_Desc", PipelineRole.Transform,
    "pdf", "separar", "dividir", "paginas", "cortar", "split", "extract")]
public class PdfSplitNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("PdfSplitNode_Name", "Dividir PDF (PDF Split)");
    public string Category => "Documents";
    public string Description => LocalizationManager.Instance.GetString("PdfSplitNode_Desc", "Divide un documento PDF de múltiples páginas en archivos individuales por página.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Original", typeof(FileItemContext), PortDirection.Output, "Original")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OutputDirectory"] = "{GlobalOutputDir}",
        ["FileNamePattern"] = "{BaseName}_page_{PageNumber:D3}.pdf"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 1),
        new("FileNamePattern", ParameterEditorType.Text, DefaultValue: "{BaseName}_page_{PageNumber:D3}.pdf", DisplayOrder: 2)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            await context.EmitAsync("Original", item);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath);
        if (!ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            await context.EmitAsync("Original", item);
            return;
        }

        string rawOutDir = Parameters.TryGetValue("OutputDirectory", out var outDirObj) ? ParameterHelper.GetString(outDirObj, "{GlobalOutputDir}") : "{GlobalOutputDir}";
        string outDir = ParameterHelper.ResolveOutputPath(rawOutDir, item);
        Directory.CreateDirectory(outDir);

        string baseName = Path.GetFileNameWithoutExtension(item.CurrentPath);
        string pattern = Parameters.TryGetValue("FileNamePattern", out var patObj) ? ParameterHelper.GetString(patObj, "{BaseName}_page_{PageNumber:D3}.pdf") : "{BaseName}_page_{PageNumber:D3}.pdf";

        using var inputDocument = PdfReader.Open(item.CurrentPath, PdfDocumentOpenMode.Import);
        int pageCount = inputDocument.PageCount;

        for (int i = 0; i < pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var singlePageDoc = new PdfDocument();
            singlePageDoc.AddPage(inputDocument.Pages[i]);

            int pageNumber = i + 1;
            string pageFileName = pattern
                .Replace("{BaseName}", baseName, StringComparison.OrdinalIgnoreCase)
                .Replace("{PageNumber:D3}", pageNumber.ToString("D3"), StringComparison.OrdinalIgnoreCase)
                .Replace("{PageNumber}", pageNumber.ToString(), StringComparison.OrdinalIgnoreCase);

            if (!pageFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                pageFileName += ".pdf";
            }

            string outFilePath = Path.Combine(outDir, pageFileName);
            singlePageDoc.Save(outFilePath);

            var pageContext = new FileItemContext(outFilePath)
            {
                OriginalPath = item.OriginalPath
            };

            foreach (var (k, v) in item.Metadata)
            {
                pageContext.Metadata[k] = v;
            }

            pageContext.Metadata["PageNumber"] = pageNumber;
            pageContext.Metadata["TotalPages"] = pageCount;
            pageContext.Metadata["SourceDocument"] = item.CurrentPath;

            await context.EmitAsync("Out", pageContext);
        }

        await context.EmitAsync("Original", item);
    }
}
