using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public record NodeDistributionBarViewModel(string Title, string Category, string AccentColor, double Percentage, double Value, string FormattedValue);

public partial class NodeMetricsRowViewModel : ObservableObject
{
    public required string NodeId { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
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
    public string BottleneckPercentageText => RelativeBottleneckRatio > 0 ? $"{RelativeBottleneckRatio * 100:F1}%" : "-";
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

    public ObservableCollection<NodeMetricsRowViewModel> NodeRows { get; } = [];
    public ObservableCollection<NodeDistributionBarViewModel> TimeDistributionBars { get; } = [];
    public ObservableCollection<NodeDistributionBarViewModel> RamDistributionBars { get; } = [];

    public WorkflowMetricsDashboardViewModel(EditorViewModel editorViewModel)
    {
        _editorViewModel = editorViewModel;
        RefreshMetrics();
    }

    [RelayCommand]
    public void RefreshMetrics()
    {
        NodeRows.Clear();
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

            tempRows.Add(new NodeMetricsRowViewModel
            {
                NodeId = node.Id ?? string.Empty,
                Title = node.Title ?? "Node",
                Category = node.Category ?? "General",
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
                RelativeBottleneckRatio = stats.RelativeBottleneckRatio
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
}
