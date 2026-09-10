using System.Windows;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk.Telemetry;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class WorkflowMetricsDashboardViewModelTests
{
    [Fact]
    public void RefreshMetrics_ShouldComputeAggregatesAndApplyFilter()
    {
        // Arrange
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var nodeA = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { Title = "Alpha Node" };
        var nodeB = new NodeViewModel(new DestinationSinkNode(), new Point(280, 0)) { Title = "Beta Node" };

        nodeA.UpdateTelemetryStats(new NodeTelemetryStats(
            NodeId: nodeA.Id,
            ProcessedCount: 10,
            TotalTimeMs: 1000,
            AverageTimeMs: 100,
            RelativeBottleneckRatio: 0.3,
            IsBottleneck: true,
            HeatLevel: LatencyHeatLevel.High,
            RollingAvgDurationMs: 90,
            RollingAvgAllocatedBytes: 1_000_000,
            PeakAllocatedBytes: 2_000_000,
            AvgCpuPercentage: 45,
            IsGpuAccelerated: true,
            RecentSamples: [NodeExecutionSample.Create(90, 1_000_000)]));

        nodeB.UpdateTelemetryStats(new NodeTelemetryStats(
            NodeId: nodeB.Id,
            ProcessedCount: 5,
            TotalTimeMs: 250,
            AverageTimeMs: 50,
            RelativeBottleneckRatio: 0.05,
            IsBottleneck: false,
            HeatLevel: LatencyHeatLevel.Low,
            RollingAvgDurationMs: 45,
            RollingAvgAllocatedBytes: 500_000,
            PeakAllocatedBytes: 800_000,
            AvgCpuPercentage: 18,
            IsGpuAccelerated: false,
            RecentSamples: [NodeExecutionSample.Create(45, 500_000)]));

        editor.Nodes.Add(nodeA);
        editor.Nodes.Add(nodeB);

        // Act
        var vm = new WorkflowMetricsDashboardViewModel(editor);

        // Assert
        vm.TotalNodesCount.Should().Be(2);
        vm.TotalInvocations.Should().Be(15);
        vm.TotalFlowDurationMs.Should().Be(1250);
        vm.GpuAcceleratedOpsCount.Should().Be(1);
        vm.BottleneckNodesCount.Should().Be(1);
        vm.NodeRows.Should().HaveCount(2);
        vm.TimeDistributionBars.Should().HaveCount(2);
        vm.RamDistributionBars.Should().HaveCount(2);

        vm.SearchFilter = "Alpha";
        vm.FilteredNodeRows.Should().HaveCount(1);
        vm.FilteredNodeRows[0].Title.Should().Be("Alpha Node");
    }

    [Fact]
    public void ResetAllMetrics_ShouldClearExecutionCounters()
    {
        // Arrange
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var node = new NodeViewModel(new FolderSourceNode(), new Point(0, 0));
        node.UpdateTelemetryStats(new NodeTelemetryStats(
            NodeId: node.Id,
            ProcessedCount: 3,
            TotalTimeMs: 90,
            AverageTimeMs: 30,
            RelativeBottleneckRatio: 0.2,
            IsBottleneck: true,
            HeatLevel: LatencyHeatLevel.Medium,
            RollingAvgDurationMs: 30,
            RollingAvgAllocatedBytes: 1_024,
            PeakAllocatedBytes: 2_048,
            AvgCpuPercentage: 20,
            IsGpuAccelerated: false,
            RecentSamples: [NodeExecutionSample.Create(30, 1_024)]));

        editor.Nodes.Add(node);
        var vm = new WorkflowMetricsDashboardViewModel(editor);
        vm.TotalInvocations.Should().BeGreaterThan(0);

        // Act
        vm.ResetAllMetrics();

        // Assert
        vm.TotalInvocations.Should().Be(0);
        vm.TotalFlowDurationMs.Should().Be(0);
        vm.BottleneckNodesCount.Should().Be(0);
        vm.GpuAcceleratedOpsCount.Should().Be(0);
        vm.NodeRows.Should().ContainSingle();
        vm.NodeRows[0].ExecutionCount.Should().Be(0);
    }

    [Theory]
    [InlineData("integraciones", "#312E81", "#818CF8", "#C7D2FE")]
    [InlineData("archivos", "#064E3B", "#10B981", "#6EE7B7")]
    [InlineData("imágenes", "#1E1B4B", "#6366F1", "#A5B4FC")]
    [InlineData("audio", "#3B0764", "#A855F7", "#E9D5FF")]
    [InlineData("documentos", "#0C4A6E", "#0284C7", "#7DD3FC")]
    [InlineData("datos", "#451A03", "#F59E0B", "#FDE68A")]
    [InlineData("lenguaje", "#4C0519", "#F43F5E", "#FECDD3")]
    [InlineData("seguridad", "#3F1D38", "#EC4899", "#FBCFE8")]
    [InlineData("lógica", "#172554", "#3B82F6", "#93C5FD")]
    [InlineData("compresión", "#134E4A", "#14B8A6", "#99F6E4")]
    [InlineData("red", "#1E3A8A", "#60A5FA", "#BFDBFE")]
    [InlineData("unknown-category", "#1E293B", "#64748B", "#E2E8F0")]
    public void GetCategoryBadgeColors_ShouldReturnExpectedPalette(string category, string bg, string border, string fg)
    {
        var result = WorkflowMetricsDashboardViewModel.GetCategoryBadgeColors(category);

        result.bg.Should().Be(bg);
        result.border.Should().Be(border);
        result.fg.Should().Be(fg);
    }

    [Theory]
    [InlineData("alpha", 1)]
    [InlineData("NODE", 2)]
    [InlineData("", 2)]
    public void SearchFilter_ShouldFilterByTitleOrCategoryCaseInsensitive(string query, int expectedCount)
    {
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var nodeA = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { Title = "Alpha Node" };
        var nodeB = new NodeViewModel(new DestinationSinkNode(), new Point(280, 0)) { Title = "Beta Node" };

        nodeA.UpdateTelemetryStats(new NodeTelemetryStats(nodeA.Id, 1, 10, 10, 0, false, LatencyHeatLevel.Low));
        nodeB.UpdateTelemetryStats(new NodeTelemetryStats(nodeB.Id, 1, 10, 10, 0, false, LatencyHeatLevel.Low));

        editor.Nodes.Add(nodeA);
        editor.Nodes.Add(nodeB);

        var vm = new WorkflowMetricsDashboardViewModel(editor)
        {
            SearchFilter = query
        };

        vm.FilteredNodeRows.Should().HaveCount(expectedCount);
    }
}
