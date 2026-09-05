using System.Collections.Concurrent;
using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using MiniExcelLibs;

namespace FileFlow.Plugin.Data;

[NodeDefinition("ExcelReportGeneratorNode_Name", "Data", "ExcelReportGeneratorNode_Desc", PipelineRole.Sink,
    "excel", "informe", "reporte", "exportar", "tabla", "consolidar", "xlsx")]
public class ExcelReportGeneratorNode : IFlowNode
{
    private readonly ConcurrentBag<Dictionary<string, object?>> _collectedRows = [];
    private readonly Lock _lock = new();
    private string? _lastExecutionId;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ExcelReportGeneratorNode_Name", "Generador de Reportes Excel (.xlsx)");
    public string Category => "Data";
    public string Description => LocalizationManager.Instance.GetString("ExcelReportGeneratorNode_Desc", "Acumula los metadatos de los archivos procesados y genera un archivo Excel (.xlsx) estructurado al concluir el flujo.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Report", typeof(FileItemContext), PortDirection.Output, "Report")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OutputDirectory"] = "{GlobalOutputDir}",
        ["ReportFileName"] = "Reporte_Ejecucion_{Date}.xlsx",
        ["ColumnsToExport"] = "FileName, FileSizeBytes, DurationMs, Status, HashSHA256"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 1),
        new("ReportFileName", ParameterEditorType.Text, DefaultValue: "Reporte_Ejecucion_{Date}.xlsx", DisplayOrder: 2),
        new("ColumnsToExport", ParameterEditorType.Text, DefaultValue: "FileName, FileSizeBytes, DurationMs, Status, HashSHA256", DisplayOrder: 3)
    ];

    private string? _discoveredGlobalOutputDir;

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (item.Metadata.TryGetValue("GlobalOutputDir", out var gOutObj) && gOutObj is string gOut && !string.IsNullOrWhiteSpace(gOut))
        {
            _discoveredGlobalOutputDir = gOut;
        }

        string colsConfig = Parameters.TryGetValue("ColumnsToExport", out var cols) ? cols?.ToString() ?? string.Empty : string.Empty;
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

    public async Task OnWorkflowCompletedAsync(IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (_collectedRows.IsEmpty)
        {
            context.Log("[ExcelReport] No se procesaron archivos; reporte omitido.", LogLevel.Information);
            return;
        }

        string outDir = Parameters.TryGetValue("OutputDirectory", out var od) ? od?.ToString() ?? "{GlobalOutputDir}" : "{GlobalOutputDir}";
        outDir = Environment.ExpandEnvironmentVariables(outDir);

        if (!string.IsNullOrWhiteSpace(_discoveredGlobalOutputDir))
        {
            outDir = outDir.Replace("{GlobalOutputDir}", _discoveredGlobalOutputDir, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(outDir) || outDir.Contains("{GlobalOutputDir}", StringComparison.OrdinalIgnoreCase))
        {
            outDir = Path.GetTempPath();
        }

        Directory.CreateDirectory(outDir);

        string reportNameTemplate = Parameters.TryGetValue("ReportFileName", out var rfn) ? rfn?.ToString() ?? "Reporte_{Date}.xlsx" : "Reporte_{Date}.xlsx";
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
        await MiniExcel.SaveAsAsync(reportPath, rowsList, overwriteFile: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        var reportItem = new FileItemContext(reportPath)
        {
            OriginalPath = reportPath,
            FileSizeBytes = new FileInfo(reportPath).Length
        };
        reportItem.Metadata["IsReport"] = true;
        reportItem.Metadata["ReportType"] = "ExcelReport";
        reportItem.Metadata["TotalRows"] = rowsList.Count;

        await context.EmitAsync("Report", reportItem).ConfigureAwait(false);
        context.Log($"[ExcelReport] Reporte Excel generado exitosamente: '{reportFileName}'", LogLevel.Information);
    }
}
