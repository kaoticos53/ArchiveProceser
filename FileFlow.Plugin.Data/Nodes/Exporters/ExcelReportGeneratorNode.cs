using System.Collections.Concurrent;
using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using MiniExcelLibs;

namespace FileFlow.Plugin.Data;

[NodeDefinition("ExcelReportGeneratorNode_Name", "Data", "ExcelReportGeneratorNode_Desc", PipelineRole.Sink,
    "excel", "informe", "reporte", "exportar", "tabla", "consolidar", "xlsx")]
public sealed class ExcelReportGeneratorNode : FlowNodeBase
{
    private readonly ConcurrentBag<Dictionary<string, object?>> _collectedRows = [];
    private readonly Lock _lock = new();
    private string? _lastExecutionId;

    public override string Name => LocalizationManager.Instance.GetString("ExcelReportGeneratorNode_Name", "Generador de Reportes Excel (.xlsx)");
    public override string Category => "Data";
    public override string Description => LocalizationManager.Instance.GetString("ExcelReportGeneratorNode_Desc", "Acumula los metadatos de los archivos procesados y genera un archivo Excel (.xlsx) estructurado al concluir el flujo.");

    public ExcelReportGeneratorNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Report", typeof(FileItemContext), PortDirection.Output, "Report")
        ];

        Parameters["OutputDirectory"] = "{GlobalOutputDir}";
        Parameters["ReportFileName"] = "Reporte_Ejecucion_{Date}.xlsx";
        Parameters["ColumnsToExport"] = "FileName, FileSizeBytes, DurationMs, Status, HashSHA256";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 1),
        new("ReportFileName", ParameterEditorType.Text, DefaultValue: "Reporte_Ejecucion_{Date}.xlsx", DisplayOrder: 2),
        new("ColumnsToExport", ParameterEditorType.Text, DefaultValue: "FileName, FileSizeBytes, DurationMs, Status, HashSHA256", DisplayOrder: 3)
    ];

    /// <summary>
    /// La carpeta donde se escribe el reporte, resuelta <b>mientras corre el flujo</b>: el reporte se escribe al
    /// terminar y ahí ya no hay elemento del que leer la metadata. La resuelve la regla única del SDK, que expande
    /// la carpeta del flujo <b>y todos sus alias</b> y ancla toda ruta relativa, de modo que no queda el texto de
    /// una plantilla declarada ni una carpeta que dependa de dónde corre el proceso (hitos 209 y 210).
    /// </summary>
    private string? _reportOutputDir;

    private string? ResolveReportOutputDir(FileItemContext item)
    {
        string pattern = Environment.ExpandEnvironmentVariables(GetParameter("OutputDirectory", "{GlobalOutputDir}"));
        return string.IsNullOrWhiteSpace(pattern) ? null : ParameterHelper.ResolveOutputPath(pattern, item);
    }

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        _reportOutputDir = ResolveReportOutputDir(item);

        string colsConfig = GetParameter("ColumnsToExport", string.Empty);
        var selectedCols = string.IsNullOrWhiteSpace(colsConfig)
            ? []
            : colsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (selectedCols.Length == 0)
        {
            row["FileName"] = item.FileName;
            row["CurrentPath"] = item.CurrentPath;
            row["FileSizeBytes"] = item.FileSizeBytes;
            foreach (var (k, v) in item.Metadata)
            {
                row[k] = v;
            }
        }
        else
        {
            foreach (var col in selectedCols)
            {
                if (col.Equals("FileName", StringComparison.OrdinalIgnoreCase)) row["FileName"] = item.FileName;
                else if (col.Equals("CurrentPath", StringComparison.OrdinalIgnoreCase)) row["CurrentPath"] = item.CurrentPath;
                else if (col.Equals("OriginalPath", StringComparison.OrdinalIgnoreCase)) row["OriginalPath"] = item.OriginalPath;
                else if (col.Equals("FileSizeBytes", StringComparison.OrdinalIgnoreCase)) row["FileSizeBytes"] = item.FileSizeBytes;
                else if (item.Metadata.TryGetValue(col, out var mVal)) row[col] = mVal;
                else row[col] = string.Empty;
            }
        }

        string executionId = item.Metadata.TryGetValue("WorkflowExecutionId", out var idObj) ? idObj?.ToString() ?? string.Empty : string.Empty;
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(executionId) && _lastExecutionId != executionId)
            {
                _lastExecutionId = executionId;
                _collectedRows.Clear();
            }
            _collectedRows.Add(row);
        }

        // Emitir el ítem downstream sin bloquear
        await context.EmitAsync("Out", item).ConfigureAwait(false);
    }

    public override async Task OnWorkflowCompletedAsync(IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (_collectedRows.IsEmpty)
        {
            context.Log("[ExcelReport] No se procesaron archivos; reporte omitido.", LogLevel.Information);
            return;
        }

        // Sin carpeta declarada —o sin haber pasado ni un elemento, que es lo que la resuelve— el reporte va al
        // directorio temporal del producto, nunca a donde corra el proceso.
        string outDir = _reportOutputDir ?? Path.GetTempPath();

        var storage = context.GetStorage();
        if (!await storage.DirectoryExistsAsync(outDir, cancellationToken).ConfigureAwait(false))
        {
            await storage.CreateDirectoryAsync(outDir, cancellationToken).ConfigureAwait(false);
        }

        string reportNameTemplate = GetParameter("ReportFileName", "Reporte_{Date}.xlsx");
        string dateStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string reportFileName = reportNameTemplate.Replace("{Date}", dateStr, StringComparison.OrdinalIgnoreCase)
                                                 .Replace("{DateTime}", dateStr, StringComparison.OrdinalIgnoreCase);

        if (!reportFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            reportFileName += ".xlsx";
        }

        string reportPath = Path.Combine(outDir, reportFileName);

        context.Log($"[ExcelReport] Generando reporte Excel con {_collectedRows.Count} registros: {reportPath}", LogLevel.Information);

        var rowsList = _collectedRows.ToList();
        _collectedRows.Clear();
        await using (var outStream = await storage.OpenWriteAsync(reportPath, cancellationToken).ConfigureAwait(false))
        {
            await MiniExcel.SaveAsAsync(outStream, rowsList, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        long reportSize = await storage.FileExistsAsync(reportPath, cancellationToken).ConfigureAwait(false)
            ? await storage.GetFileSizeAsync(reportPath, cancellationToken).ConfigureAwait(false)
            : 0;

        var reportItem = new FileItemContext(reportPath)
        {
            OriginalPath = reportPath,
            FileSizeBytes = reportSize
        };
        reportItem.Metadata["IsReport"] = true;
        reportItem.Metadata["ReportType"] = "ExcelReport";
        reportItem.Metadata["TotalRows"] = rowsList.Count;

        await context.EmitAsync("Report", reportItem).ConfigureAwait(false);
        context.Log($"[ExcelReport] Reporte Excel generado exitosamente: '{reportFileName}'", LogLevel.Information);
    }
}
