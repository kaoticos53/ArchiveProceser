using System.Diagnostics;
using FileFlow.Plugin.FileSystem.Reporting;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("OperationReportNode_Name", "FileSystem", "OperationReportNode_Desc")]
public class OperationReportNode : IFlowNode
{
    private readonly Lock _lock = new();
    private readonly List<ReportItemData> _accumulatedItems = [];
    private string? _lastExecutionId;
    private bool _reportEmitted;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("OperationReportNode_Name", "Operation Report");
    public string Category => "FileSystem";
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
        new("GroupBy", ParameterEditorType.Dropdown, DefaultValue: "Directory", DisplayOrder: 3, Options: ["Directory", "Flat", "Extension", "Status"]),
        new("ReportFileName", ParameterEditorType.Text, DefaultValue: "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}", DisplayOrder: 4),
        new("Theme", ParameterEditorType.Dropdown, DefaultValue: "ModernDark", DisplayOrder: 5, Options: ["ModernDark", "CleanLight", "Cyberpunk", "Nordic", "Executive", "HighContrast"]),
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
        string scope = Parameters.TryGetValue("ReportScope", out var sVal) ? ParameterHelper.GetString(sVal, "Consolidated") : "Consolidated";
        string theme = Parameters.TryGetValue("Theme", out var tVal) ? ParameterHelper.GetString(tVal, "ModernDark") : "ModernDark";
        bool autoOpen = Parameters.TryGetValue("AutoOpenReport", out var aoVal) && ParameterHelper.GetBoolean(aoVal, false);
        bool includeMetadata = !Parameters.TryGetValue("IncludeMetadata", out var imVal) || ParameterHelper.GetBoolean(imVal, true);
        bool isDryRun = context.IsDryRun || (item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false));

        IReportRenderer renderer = GetRenderer(format);

        // Convert current item to ReportItemData
        var reportItem = new ReportItemData
        {
            Id = item.IdString,
            FileName = item.FileName,
            OriginalPath = string.IsNullOrWhiteSpace(item.OriginalPath) ? item.CurrentPath : item.OriginalPath,
            FinalPath = item.CurrentPath,
            FileSizeBytes = item.FileSizeBytes,
            FormattedSize = FormatBytes(item.FileSizeBytes),
            Steps = item.ExecutionLog.ToList(),
            Metadata = new Dictionary<string, object?>(item.Metadata, StringComparer.OrdinalIgnoreCase),
            Tags = new HashSet<string>(item.Tags, StringComparer.OrdinalIgnoreCase),
            IsSuccess = !item.Metadata.ContainsKey("Error") && !item.Metadata.ContainsKey("Faulted"),
            ErrorMessage = item.Metadata.TryGetValue("Error", out var err) ? err?.ToString() : null,
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            lock (_lock)
            {
                // Detect reset across different workflow executions
                if (item.Metadata.TryGetValue("WorkflowExecutionId", out var execIdObj) && execIdObj?.ToString() is string execId && _lastExecutionId != execId)
                {
                    _lastExecutionId = execId;
                    _accumulatedItems.Clear();
                    _reportEmitted = false;
                }

                _accumulatedItems.Add(reportItem);
            }

            // 1. Per-file report handling (only if scope is PerFile or Both)
            if (scope.Equals("PerFile", StringComparison.OrdinalIgnoreCase) || scope.Equals("Both", StringComparison.OrdinalIgnoreCase))
            {
                string perFileName = $"{Path.GetFileNameWithoutExtension(item.FileName)}_Report.{renderer.FileExtension}";

                var singleSummary = new ReportSummaryData
                {
                    Title = $"Reporte de Operaciones - {item.FileName}",
                    GeneratedAt = DateTime.UtcNow,
                    TotalFiles = 1,
                    SuccessCount = reportItem.IsSuccess ? 1 : 0,
                    ErrorCount = reportItem.IsSuccess ? 0 : 1,
                    TotalBytes = item.FileSizeBytes,
                    FormattedTotalBytes = FormatBytes(item.FileSizeBytes),
                    Items = [reportItem],
                    GroupBy = "Flat",
                    Groups = ReportSummaryData.CreateGroups([reportItem], "Flat", b => FormatBytes(b))
                };

                string perFileContent = renderer.Render(singleSummary, theme, includeMetadata);

                var reportContext = new FileItemContext(perFileName)
                {
                    OriginalPath = perFileName,
                    PhysicalPath = string.Empty,
                    FileSizeBytes = System.Text.Encoding.UTF8.GetByteCount(perFileContent)
                };
                reportContext.Metadata["VirtualContent"] = perFileContent;
                reportContext.Metadata["ReportContent"] = perFileContent;
                reportContext.Metadata["ReportFormat"] = format;
                reportContext.Metadata["ReportScope"] = "PerFile";
                reportContext.Metadata["SourceItemFileName"] = item.FileName;
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
            context.Log($"[Reporte] Reporte actualizado en memoria (Formato: {format}, Ámbito: {scope})", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            // Forward original item to Out
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            sw.Stop();
            context.Log($"[Reporte] Error al generar reporte: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds);
            item.AddLog($"OperationReportNode failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    public async Task OnWorkflowCompletedAsync(
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string format = Parameters.TryGetValue("ReportFormat", out var fVal) ? ParameterHelper.GetString(fVal, "HTML") : "HTML";
        string scope = Parameters.TryGetValue("ReportScope", out var sVal) ? ParameterHelper.GetString(sVal, "Consolidated") : "Consolidated";
        string groupBy = Parameters.TryGetValue("GroupBy", out var gbVal) ? ParameterHelper.GetString(gbVal, "Directory") : "Directory";
        string fileNamePattern = Parameters.TryGetValue("ReportFileName", out var fnVal) ? ParameterHelper.GetString(fnVal, "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}") : "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}";
        string theme = Parameters.TryGetValue("Theme", out var tVal) ? ParameterHelper.GetString(tVal, "ModernDark") : "ModernDark";
        bool autoOpen = Parameters.TryGetValue("AutoOpenReport", out var aoVal) && ParameterHelper.GetBoolean(aoVal, false);
        bool includeMetadata = !Parameters.TryGetValue("IncludeMetadata", out var imVal) || ParameterHelper.GetBoolean(imVal, true);
        bool isDryRun = context.IsDryRun;

        if (!scope.Equals("Consolidated", StringComparison.OrdinalIgnoreCase) && !scope.Equals("Both", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ReportSummaryData consolidatedSummary;
        lock (_lock)
        {
            if (_reportEmitted || _accumulatedItems.Count == 0)
            {
                return;
            }
            _reportEmitted = true;

            long totalBytes = _accumulatedItems.Sum(i => i.FileSizeBytes);
            int success = _accumulatedItems.Count(i => i.IsSuccess);
            int errors = _accumulatedItems.Count - success;
            var itemsSnapshot = _accumulatedItems.ToList();
            var groups = ReportSummaryData.CreateGroups(itemsSnapshot, groupBy, b => FormatBytes(b));

            consolidatedSummary = new ReportSummaryData
            {
                Title = "Reporte Consolidado de Operaciones",
                GeneratedAt = DateTime.UtcNow,
                TotalFiles = _accumulatedItems.Count,
                SuccessCount = success,
                ErrorCount = errors,
                TotalBytes = totalBytes,
                FormattedTotalBytes = FormatBytes(totalBytes),
                Items = itemsSnapshot,
                GroupBy = groupBy,
                Groups = groups
            };
        }

        IReportRenderer renderer = GetRenderer(format);
        string consolidatedContent = renderer.Render(consolidatedSummary, theme, includeMetadata);

        var dummy = new FileItemContext(string.Empty);
        string resolvedFileName = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(fileNamePattern, dummy);
        if (string.IsNullOrWhiteSpace(resolvedFileName))
        {
            resolvedFileName = $"Reporte_Ejecucion_{DateTime.Now:yyyyMMdd_HHmmss}";
        }
        if (!resolvedFileName.EndsWith($".{renderer.FileExtension}", StringComparison.OrdinalIgnoreCase))
        {
            resolvedFileName += $".{renderer.FileExtension}";
        }
        string virtualFileName = Path.GetFileName(resolvedFileName);

        var reportContext = new FileItemContext(virtualFileName)
        {
            OriginalPath = virtualFileName,
            PhysicalPath = string.Empty,
            FileSizeBytes = System.Text.Encoding.UTF8.GetByteCount(consolidatedContent)
        };
        reportContext.Metadata["VirtualContent"] = consolidatedContent;
        reportContext.Metadata["ReportContent"] = consolidatedContent;
        reportContext.Metadata["ReportFormat"] = format;
        reportContext.Metadata["ReportScope"] = "Consolidated";
        reportContext.Metadata["TotalFiles"] = consolidatedSummary.TotalFiles;
        reportContext.Metadata["SuccessCount"] = consolidatedSummary.SuccessCount;
        reportContext.Metadata["ErrorCount"] = consolidatedSummary.ErrorCount;
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

        context.Log($"[Reporte] Reporte consolidado generado en memoria ({consolidatedSummary.TotalFiles} archivos)", LogLevel.Information, reportContext);
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
