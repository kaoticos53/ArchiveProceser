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
    private string? _consolidatedFilePath;
    private bool _reportAutoOpened;
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
        ["DestinationFolder"] = @"{RelativeDir}\Output",
        ["ReportFileName"] = "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}",
        ["Theme"] = "ModernDark",
        ["AutoOpenReport"] = false,
        ["IncludeMetadata"] = true
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        string format = Parameters.TryGetValue("ReportFormat", out var fVal) ? ParameterHelper.GetString(fVal, "HTML") : "HTML";
        string scope = Parameters.TryGetValue("ReportScope", out var sVal) ? ParameterHelper.GetString(sVal, "Consolidated") : "Consolidated";
        string groupBy = Parameters.TryGetValue("GroupBy", out var gbVal) ? ParameterHelper.GetString(gbVal, "Directory") : "Directory";
        string destFolderPattern = Parameters.TryGetValue("DestinationFolder", out var dfVal) ? ParameterHelper.GetString(dfVal, @"{RelativeDir}\Output") : @"{RelativeDir}\Output";
        string fileNamePattern = Parameters.TryGetValue("ReportFileName", out var fnVal) ? ParameterHelper.GetString(fnVal, "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}") : "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}";
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
            // 1. Per-file report handling (only if scope is PerFile or Both)
            if (scope.Equals("PerFile", StringComparison.OrdinalIgnoreCase) || scope.Equals("Both", StringComparison.OrdinalIgnoreCase))
            {
                string perFileFolder = ParameterHelper.ResolveOutputPath(destFolderPattern, item);
                string perFileName = $"{Path.GetFileNameWithoutExtension(item.FileName)}_Report.{renderer.FileExtension}";
                string perFilePath = Path.Combine(perFileFolder, perFileName);

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

                if (isDryRun)
                {
                    context.RegisterPlannedAction(new PlannedAction(
                        Guid.NewGuid(),
                        Id,
                        Name,
                        PlannedOperationType.Custom,
                        item.CurrentPath,
                        perFilePath,
                        $"Simulación: Se generaría reporte individual '{perFileName}' en '{perFileFolder}'"
                    ));
                }
                else
                {
                    if (!Directory.Exists(perFileFolder))
                    {
                        Directory.CreateDirectory(perFileFolder);
                    }
                    await File.WriteAllTextAsync(perFilePath, perFileContent, cancellationToken);

                    var reportContext = new FileItemContext(perFilePath);
                    reportContext.AddLog($"Individual report generated: {perFilePath}");
                    await context.EmitAsync("Report", reportContext);

                    if (autoOpen)
                    {
                        TryOpenReport(perFilePath);
                    }
                }
            }

            // 2. Consolidated report handling (Consolidated or Both)
            if (scope.Equals("Consolidated", StringComparison.OrdinalIgnoreCase) || scope.Equals("Both", StringComparison.OrdinalIgnoreCase))
            {
                ReportSummaryData consolidatedSummary;
                string consolidatedPath;

                lock (_lock)
                {
                    // Detect reset across different workflow executions
                    if (item.Metadata.TryGetValue("WorkflowExecutionId", out var execIdObj) && execIdObj?.ToString() is string execId && _lastExecutionId != execId)
                    {
                        _lastExecutionId = execId;
                        _accumulatedItems.Clear();
                        _consolidatedFilePath = null;
                        _reportAutoOpened = false;
                        _reportEmitted = false;
                    }

                    _accumulatedItems.Add(reportItem);

                    // Resolve the single consolidated path on first item and reuse it for all items in this execution
                    if (_consolidatedFilePath == null)
                    {
                        string targetFolder = ParameterHelper.ResolveOutputPath(destFolderPattern, item);
                        string resolvedFileName = ParameterHelper.ResolveOutputPath(fileNamePattern, item);
                        if (!resolvedFileName.EndsWith($".{renderer.FileExtension}", StringComparison.OrdinalIgnoreCase))
                        {
                            resolvedFileName += $".{renderer.FileExtension}";
                        }
                        _consolidatedFilePath = Path.Combine(targetFolder, Path.GetFileName(resolvedFileName));
                    }

                    consolidatedPath = _consolidatedFilePath;

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

                string consolidatedContent = renderer.Render(consolidatedSummary, theme, includeMetadata);

                if (isDryRun)
                {
                    context.RegisterPlannedAction(new PlannedAction(
                        Guid.NewGuid(),
                        Id,
                        Name,
                        PlannedOperationType.Custom,
                        item.CurrentPath,
                        consolidatedPath,
                        $"Simulación: Se generaría/actualizaría reporte consolidado '{Path.GetFileName(consolidatedPath)}' ({consolidatedSummary.TotalFiles} archivos)"
                    ));
                }
                else
                {
                    string? targetDir = Path.GetDirectoryName(consolidatedPath);
                    if (!string.IsNullOrWhiteSpace(targetDir) && !Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                    await File.WriteAllTextAsync(consolidatedPath, consolidatedContent, cancellationToken);

                    // Emit to Report output port only once for the consolidated report
                    bool shouldEmit = false;
                    bool shouldOpen = false;
                    lock (_lock)
                    {
                        if (!_reportEmitted)
                        {
                            _reportEmitted = true;
                            shouldEmit = true;
                        }
                        if (autoOpen && !_reportAutoOpened)
                        {
                            _reportAutoOpened = true;
                            shouldOpen = true;
                        }
                    }

                    if (shouldEmit)
                    {
                        var reportContext = new FileItemContext(consolidatedPath);
                        reportContext.AddLog($"Consolidated report generated ({consolidatedSummary.TotalFiles} files): {consolidatedPath}");
                        await context.EmitAsync("Report", reportContext);
                    }

                    if (shouldOpen)
                    {
                        TryOpenReport(consolidatedPath);
                    }
                }
            }

            sw.Stop();
            item.AddLog($"OperationReportNode processed file. Format: {format}, Scope: {scope}");

            string detailsJson = $"{{\"format\": \"{format}\", \"scope\": \"{scope}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}}}";
            context.Log($"[Reporte] Reporte actualizado exitosamente (Formato: {format}, Ámbito: {scope})", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

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

    private static void TryOpenReport(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
        }
        catch
        {
            // Silently ignore if shell opener fails or is not available
        }
    }
}
