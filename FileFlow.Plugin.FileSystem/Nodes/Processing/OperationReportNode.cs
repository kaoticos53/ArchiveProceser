using System.Diagnostics;
using FileFlow.Plugin.FileSystem.Reporting;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("OperationReportNode_Name", "Integrations", "OperationReportNode_Desc", PipelineRole.Control,
    "reporte", "informe", "html", "markdown", "auditoria", "resumen", "trazabilidad", "report")]
public class OperationReportNode : IFlowNode
{
    private readonly Lock _lock = new();
    private readonly List<ReportItemData> _accumulatedItems = [];
    private string? _lastExecutionId;
    private bool _reportEmitted;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("OperationReportNode_Name", "Operation Report");
    public string Category => "Integrations";
    public string Description => LocalizationManager.Instance.GetString("OperationReportNode_Desc", "Generates an attractive visual execution and operations report for all processed files.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Report", typeof(FileItemContext), PortDirection.Output, "Report"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ReportFormat"] = "HTML",
        ["ReportScope"] = "Consolidated",
        ["GroupBy"] = "Directory",
        ["ReportFileName"] = "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}",
        ["Theme"] = "ModernDark",
        ["AutoOpenReport"] = false,
        ["IncludeMetadata"] = true
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("ReportFormat", ParameterEditorType.Dropdown, DefaultValue: "HTML", DisplayOrder: 1, Options: ["HTML", "Markdown", "Text", "JSON", "CSV"]),
        new("ReportScope", ParameterEditorType.Dropdown, DefaultValue: "Consolidated", DisplayOrder: 2, Options: ["Consolidated", "PerFile", "Both"]),
        new("GroupBy", ParameterEditorType.Dropdown, DefaultValue: "Directory", DisplayOrder: 3, Options: ["Directory", "Extension", "Status", "Flat"]),
        new("ReportFileName", ParameterEditorType.Text, DefaultValue: "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}", DisplayOrder: 4),
        new("Theme", ParameterEditorType.Dropdown, DefaultValue: "ModernDark", DisplayOrder: 5, Options: ["ModernDark", "CleanLight", "Executive"]),
        new("AutoOpenReport", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 6),
        new("IncludeMetadata", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 7)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        string format = Parameters.TryGetValue("ReportFormat", out var fVal) ? ParameterHelper.GetString(fVal, "HTML") : "HTML";
        string scope = Parameters.TryGetValue("ReportScope", out var scVal) ? ParameterHelper.GetString(scVal, "Consolidated") : "Consolidated";
        string groupBy = Parameters.TryGetValue("GroupBy", out var gbVal) ? ParameterHelper.GetString(gbVal, "Directory") : "Directory";
        string theme = Parameters.TryGetValue("Theme", out var thVal) ? ParameterHelper.GetString(thVal, "ModernDark") : "ModernDark";
        bool autoOpen = Parameters.TryGetValue("AutoOpenReport", out var aoVal) && ParameterHelper.GetBoolean(aoVal, false);
        bool includeMeta = Parameters.TryGetValue("IncludeMetadata", out var imVal) && ParameterHelper.GetBoolean(imVal, true);
        string nameTemplate = Parameters.TryGetValue("ReportFileName", out var fnVal) ? ParameterHelper.GetString(fnVal, "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}") : "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}";
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false);

        string executionId = item.Metadata.TryGetValue("WorkflowExecutionId", out var execId) ? execId?.ToString() ?? "Unknown" : "Unknown";

        var reportItem = new ReportItemData
        {
            Id = item.IdString,
            FileName = item.FileName,
            OriginalPath = item.OriginalPath,
            FinalPath = item.CurrentPath,
            FileSizeBytes = item.FileSizeBytes,
            FormattedSize = FormatBytes(item.FileSizeBytes),
            Steps = [.. item.ExecutionLog],
            Metadata = includeMeta ? new Dictionary<string, object?>(item.Metadata) : new Dictionary<string, object?>(),
            Tags = item.Tags.ToHashSet(StringComparer.OrdinalIgnoreCase),
            IsSuccess = true,
            ErrorMessage = null,
            ProcessedAt = DateTime.UtcNow
        };

        lock (_lock)
        {
            if (_lastExecutionId != executionId)
            {
                _lastExecutionId = executionId;
                _accumulatedItems.Clear();
                _reportEmitted = false;
            }

            _accumulatedItems.Add(reportItem);
        }

        try
        {
            if (scope.Equals("PerFile", StringComparison.OrdinalIgnoreCase) || scope.Equals("Both", StringComparison.OrdinalIgnoreCase))
            {
                var perFileSummary = new ReportSummaryData
                {
                    Title = "Reporte de Ejecución - " + item.FileName,
                    GeneratedAt = DateTime.UtcNow,
                    TotalFiles = 1,
                    SuccessCount = 1,
                    ErrorCount = 0,
                    TotalBytes = item.FileSizeBytes,
                    FormattedTotalBytes = FormatBytes(item.FileSizeBytes),
                    Items = [reportItem],
                    GroupBy = "Flat",
                    Groups = ReportSummaryData.CreateGroups([reportItem], "Flat", FormatBytes)
                };

                var renderer = GetRenderer(format);
                string perFileContent = renderer.Render(perFileSummary, theme, includeMeta);
                string extension = renderer.FileExtension.TrimStart('.');

                string perFileName = $"{Path.GetFileNameWithoutExtension(item.CurrentPath)}_Report.{extension}";

                var reportContext = new FileItemContext(perFileName, isDirectory: false)
                {
                    CurrentPath = perFileName,
                    PhysicalPath = string.Empty,
                    OriginalPath = perFileName
                };
                reportContext.Metadata["VirtualContent"] = perFileContent;
                reportContext.Metadata["ReportContent"] = perFileContent;
                reportContext.Metadata["DocumentType"] = format.ToUpperInvariant();
                reportContext.Metadata["IsReport"] = true;
                reportContext.Metadata["WorkflowExecutionId"] = executionId;
                reportContext.AddLog($"Individual in-memory report generated: {perFileName}");

                if (isDryRun)
                {
                    context.RegisterPlannedAction(new PlannedAction(
                        Guid.NewGuid(),
                        Id,
                        Name,
                        PlannedOperationType.Custom,
                        item.CurrentPath,
                        perFileName,
                        $"Simulación: Se generaría reporte individual en memoria '{perFileName}'"
                    ));
                }
                else if (autoOpen)
                {
                    TryOpenTemporaryReport(perFileName, perFileContent);
                }

                await context.EmitAsync("Report", reportContext);
            }

            sw.Stop();
            item.AddLog($"OperationReportNode processed file in memory. Format: {format}, Scope: {scope}");

            string detailsJson = $"{{\"format\": \"{format}\", \"scope\": \"{scope}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Report_Updated", "[Operation Report] Report updated in memory (Format: {0}, Scope: {1})", format, scope), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            // Forward original item to Out
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            sw.Stop();
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Report_Error", "[Operation Report] Error generating report: {0}", ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds);
            item.AddLog($"OperationReportNode failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    public async Task OnWorkflowCompletedAsync(
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string format = Parameters.TryGetValue("ReportFormat", out var fVal) ? ParameterHelper.GetString(fVal, "HTML") : "HTML";
        string scope = Parameters.TryGetValue("ReportScope", out var scVal) ? ParameterHelper.GetString(scVal, "Consolidated") : "Consolidated";
        string groupBy = Parameters.TryGetValue("GroupBy", out var gbVal) ? ParameterHelper.GetString(gbVal, "Directory") : "Directory";
        string theme = Parameters.TryGetValue("Theme", out var thVal) ? ParameterHelper.GetString(thVal, "ModernDark") : "ModernDark";
        bool autoOpen = Parameters.TryGetValue("AutoOpenReport", out var aoVal) && ParameterHelper.GetBoolean(aoVal, false);
        bool includeMeta = Parameters.TryGetValue("IncludeMetadata", out var imVal) && ParameterHelper.GetBoolean(imVal, true);
        string nameTemplate = Parameters.TryGetValue("ReportFileName", out var fnVal) ? ParameterHelper.GetString(fnVal, "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}") : "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}";

        if (scope.Equals("PerFile", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        List<ReportItemData> itemsSnapshot;
        string executionId;
        lock (_lock)
        {
            if (_reportEmitted || _accumulatedItems.Count == 0) return;
            _reportEmitted = true;
            itemsSnapshot = [.. _accumulatedItems];
            executionId = _lastExecutionId ?? "Batch";
        }

        long totalBytes = itemsSnapshot.Sum(i => i.FileSizeBytes);
        int successCount = itemsSnapshot.Count(i => i.IsSuccess);
        int errorCount = itemsSnapshot.Count - successCount;
        bool isDryRun = context.IsDryRun;

        var consolidatedSummary = new ReportSummaryData
        {
            Title = "Reporte Consolidado de Operaciones",
            GeneratedAt = DateTime.UtcNow,
            TotalFiles = itemsSnapshot.Count,
            SuccessCount = successCount,
            ErrorCount = errorCount,
            TotalBytes = totalBytes,
            FormattedTotalBytes = FormatBytes(totalBytes),
            Items = itemsSnapshot,
            GroupBy = groupBy,
            Groups = ReportSummaryData.CreateGroups(itemsSnapshot, groupBy, FormatBytes)
        };

        var renderer = GetRenderer(format);
        string consolidatedContent = renderer.Render(consolidatedSummary, theme, includeMeta);
        string extension = renderer.FileExtension.TrimStart('.');

        var dummyItem = new FileItemContext("BatchReport", false);
        string resolvedName = VariableTemplateResolver.Resolve(nameTemplate, dummyItem);
        string virtualFileName = resolvedName.EndsWith($".{extension}", StringComparison.OrdinalIgnoreCase) ? resolvedName : $"{resolvedName}.{extension}";

        var reportContext = new FileItemContext(virtualFileName, isDirectory: false)
        {
            CurrentPath = virtualFileName,
            PhysicalPath = string.Empty,
            OriginalPath = virtualFileName
        };
        reportContext.Metadata["VirtualContent"] = consolidatedContent;
        reportContext.Metadata["ReportContent"] = consolidatedContent;
        reportContext.Metadata["DocumentType"] = format.ToUpperInvariant();
        reportContext.Metadata["IsReport"] = true;
        reportContext.Metadata["WorkflowExecutionId"] = executionId;
        reportContext.AddLog($"Consolidated in-memory report generated ({consolidatedSummary.TotalFiles} files): {virtualFileName}");

        if (isDryRun)
        {
            context.RegisterPlannedAction(new PlannedAction(
                Guid.NewGuid(),
                Id,
                Name,
                PlannedOperationType.Custom,
                string.Empty,
                virtualFileName,
                $"Simulación: Se generaría reporte consolidado en memoria '{virtualFileName}' ({consolidatedSummary.TotalFiles} archivos)"
            ));
        }
        else if (autoOpen)
        {
            TryOpenTemporaryReport(virtualFileName, consolidatedContent);
        }

        context.Log(LocalizationManager.Instance.GetFormattedString("Log_Report_Consolidated", "[Operation Report] Consolidated report generated in memory ({0} files)", consolidatedSummary.TotalFiles), LogLevel.Information, reportContext);
        await context.EmitAsync("Report", reportContext);
    }

    private static IReportRenderer GetRenderer(string format)
    {
        return format.ToUpperInvariant() switch
        {
            "MARKDOWN" or "MD" => new MarkdownReportRenderer(),
            "TEXT" or "TXT" => new TextReportRenderer(),
            "JSON" => new JsonReportRenderer(),
            "CSV" => new CsvReportRenderer(),
            "HTML" or _ => new HtmlReportRenderer()
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "0 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    private static void TryOpenTemporaryReport(string fileName, string content)
    {
        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_Reports");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            string tempFile = Path.Combine(tempDir, fileName);
            File.WriteAllText(tempFile, content, System.Text.Encoding.UTF8);
            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
        }
        catch
        {
            // Silently ignore if preview launcher fails
        }
    }
}
