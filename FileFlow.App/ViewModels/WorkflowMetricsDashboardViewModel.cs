using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public record NodeDistributionBarViewModel(string Title, string Category, string AccentColor, double Percentage, double Value, string FormattedValue);

public partial class NodeMetricsRowViewModel : ObservableObject
{
    public required string NodeId { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string NodeIcon { get; init; }
    public required string CategoryIcon { get; init; }
    public required string CategoryBadgeBackground { get; init; }
    public required string CategoryBadgeBorder { get; init; }
    public required string CategoryBadgeForeground { get; init; }
    public required string AccentColor { get; init; }
    public required long ExecutionCount { get; init; }
    public required long ErrorCount { get; init; }
    public required double AvgDurationMs { get; init; }
    public required string FormattedAvgDuration { get; init; }
    public required double RollingAvgDurationMs { get; init; }
    public required string FormattedRollingDuration { get; init; }
    public required long AvgAllocatedBytes { get; init; }
    public required string FormattedAvgRam { get; init; }
    public required long PeakAllocatedBytes { get; init; }
    public required string FormattedPeakRam { get; init; }
    public required double AvgCpuPercentage { get; init; }
    public required bool IsGpuAccelerated { get; init; }
    public required bool IsBottleneck { get; init; }
    public required double RelativeBottleneckRatio { get; init; }
    public double BottleneckPercentage { get; set; }
    public string BottleneckPercentageText { get; set; } = "-";
    public string BottleneckBarBrush { get; set; } = "#38BDF8";
    public required IReadOnlyList<double> RecentDurations { get; init; }
}

public partial class WorkflowMetricsDashboardViewModel : ObservableObject
{
    private readonly EditorViewModel _editorViewModel;

    [ObservableProperty]
    private int _totalNodesCount;

    [ObservableProperty]
    private long _totalInvocations;

    [ObservableProperty]
    private double _totalFlowDurationMs;

    [ObservableProperty]
    private string _formattedTotalDuration = "0 ms";

    [ObservableProperty]
    private double _averageLatencyPerItemMs;

    [ObservableProperty]
    private string _formattedAvgLatency = "0 ms";

    [ObservableProperty]
    private long _totalAllocatedBytes;

    [ObservableProperty]
    private string _formattedTotalRam = "0 B";

    [ObservableProperty]
    private int _gpuAcceleratedOpsCount;

    [ObservableProperty]
    private int _bottleneckNodesCount;

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    public ObservableCollection<NodeMetricsRowViewModel> NodeRows { get; } = [];
    public ObservableCollection<NodeMetricsRowViewModel> FilteredNodeRows { get; } = [];
    public ObservableCollection<NodeDistributionBarViewModel> TimeDistributionBars { get; } = [];
    public ObservableCollection<NodeDistributionBarViewModel> RamDistributionBars { get; } = [];

    partial void OnSearchFilterChanged(string value)
    {
        ApplyFilter();
    }

    public WorkflowMetricsDashboardViewModel(EditorViewModel editorViewModel)
    {
        _editorViewModel = editorViewModel;
        RefreshMetrics();
    }

    private void ApplyFilter()
    {
        FilteredNodeRows.Clear();
        var q = SearchFilter?.Trim() ?? string.Empty;
        var query = string.IsNullOrWhiteSpace(q)
            ? NodeRows
            : NodeRows.Where(r => r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                  r.Category.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var r in query)
        {
            FilteredNodeRows.Add(r);
        }
    }

    [RelayCommand]
    public void RefreshMetrics()
    {
        NodeRows.Clear();
        FilteredNodeRows.Clear();
        TimeDistributionBars.Clear();
        RamDistributionBars.Clear();

        var nodes = _editorViewModel.Nodes.ToList();
        TotalNodesCount = nodes.Count;

        long sumInvocations = 0;
        double sumDuration = 0;
        long sumBytes = 0;
        int gpuOps = 0;
        int bottlenecks = 0;

        var tempRows = new List<NodeMetricsRowViewModel>();

        foreach (var node in nodes)
        {
            var stats = node.CurrentStats;
            if (string.IsNullOrEmpty(stats.NodeId) && node.Id != null)
            {
                stats = stats with { NodeId = node.Id };
            }

            sumInvocations += stats.ProcessedCount;
            sumDuration += stats.TotalTimeMs;
            sumBytes += stats.RollingAvgAllocatedBytes * stats.ProcessedCount;
            if (stats.IsGpuAccelerated) gpuOps++;
            if (stats.IsBottleneck) bottlenecks++;

            var (badgeBg, badgeBorder, badgeFg) = GetCategoryBadgeColors(node.Category ?? "General");

            tempRows.Add(new NodeMetricsRowViewModel
            {
                NodeId = node.Id ?? string.Empty,
                Title = node.Title ?? "Node",
                Category = node.Category ?? "General",
                NodeIcon = NodeIconResolver.GetIconForNodeType(node.NodeTypeName ?? node.Title ?? string.Empty),
                CategoryIcon = NodeIconResolver.GetIconForCategory(node.Category ?? "General"),
                CategoryBadgeBackground = badgeBg,
                CategoryBadgeBorder = badgeBorder,
                CategoryBadgeForeground = badgeFg,
                AccentColor = node.AccentColor ?? "#818CF8",
                ExecutionCount = stats.ProcessedCount,
                ErrorCount = 0,
                AvgDurationMs = stats.AverageTimeMs,
                FormattedAvgDuration = FormatLatency(stats.AverageTimeMs),
                RollingAvgDurationMs = stats.RollingAvgDurationMs,
                FormattedRollingDuration = FormatLatency(stats.RollingAvgDurationMs),
                AvgAllocatedBytes = stats.RollingAvgAllocatedBytes,
                FormattedAvgRam = FormatBytes(stats.RollingAvgAllocatedBytes),
                PeakAllocatedBytes = stats.PeakAllocatedBytes,
                FormattedPeakRam = FormatBytes(stats.PeakAllocatedBytes),
                AvgCpuPercentage = stats.AvgCpuPercentage,
                IsGpuAccelerated = stats.IsGpuAccelerated,
                IsBottleneck = stats.IsBottleneck,
                RelativeBottleneckRatio = stats.RelativeBottleneckRatio,
                BottleneckPercentage = Math.Clamp(stats.RelativeBottleneckRatio * 100.0, 0, 100),
                BottleneckPercentageText = stats.RelativeBottleneckRatio > 0 ? $"{stats.RelativeBottleneckRatio * 100:F1}%" : "-",
                BottleneckBarBrush = stats.RelativeBottleneckRatio >= 0.25 ? "#F43F5E" : (stats.RelativeBottleneckRatio >= 0.1 ? "#F59E0B" : "#38BDF8"),
                RecentDurations = stats.RecentSamples?.Select(s => s.DurationMs).ToList() ?? []
            });
        }

        TotalInvocations = sumInvocations;
        TotalFlowDurationMs = sumDuration;
        FormattedTotalDuration = FormatLatency(sumDuration);
        AverageLatencyPerItemMs = sumInvocations > 0 ? sumDuration / sumInvocations : 0;
        FormattedAvgLatency = FormatLatency(AverageLatencyPerItemMs);
        TotalAllocatedBytes = sumBytes;
        FormattedTotalRam = FormatBytes(sumBytes);
        GpuAcceleratedOpsCount = gpuOps;
        BottleneckNodesCount = bottlenecks;

        foreach (var row in tempRows.OrderByDescending(r => r.AvgDurationMs))
        {
            NodeRows.Add(row);
        }

        ApplyFilter();

        // Distribución de Tiempo
        if (sumDuration > 0)
        {
            foreach (var row in tempRows.Where(r => r.ExecutionCount > 0).OrderByDescending(r => r.AvgDurationMs * r.ExecutionCount))
            {
                var nodeTotalTime = row.AvgDurationMs * row.ExecutionCount;
                var pct = (nodeTotalTime / sumDuration) * 100.0;
                TimeDistributionBars.Add(new NodeDistributionBarViewModel(
                    row.Title,
                    row.Category,
                    row.AccentColor,
                    pct,
                    nodeTotalTime,
                    $"{pct:F1}% ({FormatLatency(nodeTotalTime)})"
                ));
            }
        }

        // Distribución de Memoria RAM
        if (sumBytes > 0)
        {
            foreach (var row in tempRows.Where(r => r.AvgAllocatedBytes > 0).OrderByDescending(r => r.AvgAllocatedBytes * r.ExecutionCount))
            {
                var nodeTotalRam = row.AvgAllocatedBytes * row.ExecutionCount;
                var pct = ((double)nodeTotalRam / sumBytes) * 100.0;
                RamDistributionBars.Add(new NodeDistributionBarViewModel(
                    row.Title,
                    row.Category,
                    row.AccentColor,
                    pct,
                    nodeTotalRam,
                    $"{pct:F1}% ({FormatBytes(nodeTotalRam)})"
                ));
            }
        }
    }

    [RelayCommand]
    public void ResetAllMetrics()
    {
        foreach (var node in _editorViewModel.Nodes)
        {
            node.UpdateTelemetryStats(FileFlow.Sdk.Telemetry.NodeTelemetryStats.Empty(node.Id));
        }
        RefreshMetrics();
    }

    [RelayCommand]
    public void ExportCsv()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"FileFlow_Metrics_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("NodeId,Title,Category,Invocations,Errors,AvgDurationMs,RollingAvgDurationMs,AvgAllocatedBytes,PeakAllocatedBytes,AvgCpuPercentage,IsGpuAccelerated,IsBottleneck,BottleneckRatio");
                foreach (var row in NodeRows)
                {
                    sb.AppendLine($"\"{row.NodeId}\",\"{row.Title}\",\"{row.Category}\",{row.ExecutionCount},{row.ErrorCount},{row.AvgDurationMs:F3},{row.RollingAvgDurationMs:F3},{row.AvgAllocatedBytes},{row.PeakAllocatedBytes},{row.AvgCpuPercentage:F2},{row.IsGpuAccelerated},{row.IsBottleneck},{row.RelativeBottleneckRatio:F4}");
                }
                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show(LocalizationManager.Instance.GetString("Metrics_ExportSuccess", "Métricas exportadas exitosamente a CSV."), "FileFlow Studio", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar CSV: {ex.Message}", "FileFlow Studio", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void ExportJson()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = $"FileFlow_Metrics_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                var exportData = new
                {
                    ExportTimestamp = DateTime.UtcNow,
                    Summary = new
                    {
                        TotalNodes = TotalNodesCount,
                        TotalInvocations,
                        TotalFlowDurationMs,
                        AverageLatencyPerItemMs,
                        TotalAllocatedBytes,
                        GpuAcceleratedOpsCount,
                        BottleneckNodesCount
                    },
                    Nodes = NodeRows
                };

                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
                MessageBox.Show(LocalizationManager.Instance.GetString("Metrics_ExportSuccess", "Métricas exportadas exitosamente a JSON."), "FileFlow Studio", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar JSON: {ex.Message}", "FileFlow Studio", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string FormatLatency(double ms)
    {
        if (ms < 1.0) return $"{ms * 1000:F0} µs";
        if (ms < 1000.0) return $"{ms:F1} ms";
        return $"{ms / 1000.0:F2} s";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F0} KB";
        return $"{bytes} B";
    }

    public static (string bg, string border, string fg) GetCategoryBadgeColors(string category)
    {
        return (category?.Trim().ToLowerInvariant()) switch
        {
            "files" or "filesystem" or "archivos" => ("#064E3B", "#10B981", "#6EE7B7"),
            "imagevision" or "images" or "imágenes" => ("#1E1B4B", "#6366F1", "#A5B4FC"),
            "audiovoice" or "audio" or "voz" => ("#3B0764", "#A855F7", "#E9D5FF"),
            "documents" or "documentos" or "pdf" => ("#0C4A6E", "#0284C7", "#7DD3FC"),
            "data" or "datos" or "tables" => ("#451A03", "#F59E0B", "#FDE68A"),
            "languageai" or "llm" or "lenguaje" => ("#4C0519", "#F43F5E", "#FECDD3"),
            "security" or "seguridad" => ("#3F1D38", "#EC4899", "#FBCFE8"),
            "logic" or "lógica" => ("#172554", "#3B82F6", "#93C5FD"),
            "archives" or "compresión" => ("#134E4A", "#14B8A6", "#99F6E4"),
            "network" or "red" => ("#1E3A8A", "#60A5FA", "#BFDBFE"),
            "integrations" or "integraciones" => ("#312E81", "#818CF8", "#C7D2FE"),
            _ => ("#1E293B", "#64748B", "#E2E8F0")
        };
    }
}
