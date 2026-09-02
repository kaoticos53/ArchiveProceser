using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FileFlow.Plugin.Documents;

[NodeDefinition("PdfMergeNode_Name", "Documents", "PdfMergeNode_Desc")]
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
            lock (_lock)
            {
                _collectedPdfPaths.Add(item.CurrentPath);
            }
        }

        await context.EmitAsync("PassThrough", item);
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
