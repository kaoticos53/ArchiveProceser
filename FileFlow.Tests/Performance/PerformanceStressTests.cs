using System.Diagnostics;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Performance;

public class PerformanceStressTests
{
    [Fact]
    public void Resolve_ShouldEvaluate10000ItemsUnder1000Milliseconds()
    {
        // Arrange
        const int itemQuantity = 10_000;
        var items = new List<FileItemContext>(itemQuantity);

        for (int i = 0; i < itemQuantity; i++)
        {
            var item = new FileItemContext($@"C:\Photos\Batch_{i}\image_{i}.jpg", isDirectory: false);
            item.Metadata["SourceRootPath"] = @"C:\Photos";
            item.Metadata["DateTaken"] = "2026-08-20 12:00:00";
            item.Metadata["Counter"] = i;
            items.Add(item);
        }

        string template = @"C:\Output\{Year(DateTaken)}/Folder_{PadLeft(Counter, 4, ""0"")}/{RelativePath}/{FileNameNoExt}.{Extension}";

        // Act
        var sw = Stopwatch.StartNew();
        foreach (var item in items)
        {
            _ = VariableTemplateResolver.Resolve(template, item);
        }
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(1000, "10,000 template resolutions should complete in less than 1 second");
    }

    [Fact]
    public void NodeViewModel_SnapshotHistory_ShouldBeTrimmedToMaxRecordedSnapshots_UnderHighLoad()
    {
        // Arrange
        var mockNode = new MockFlowNode();
        var nodeVm = new FileFlow.App.ViewModels.NodeViewModel(mockNode, new System.Windows.Point(0, 0));

        // Act - Add 1,000 snapshots (exceeding MaxRecordedSnapshots of 500)
        for (int i = 0; i < 1_000; i++)
        {
            var item = new FileItemContext($@"C:\Test\File_{i}.txt");
            var snap = NodeDataSnapshot.CreateInput(nodeVm.Id, "In", item);
            nodeVm.InputSnapshots.Add(snap);

            if (nodeVm.InputSnapshots.Count > FileFlow.App.ViewModels.NodeViewModel.MaxRecordedSnapshots)
            {
                nodeVm.InputSnapshots.RemoveAt(0);
            }
        }

        // Assert
        nodeVm.InputSnapshots.Should().HaveCount(FileFlow.App.ViewModels.NodeViewModel.MaxRecordedSnapshots);
        nodeVm.InputSnapshots[0].ItemSnapshot.CurrentPath.Should().Be(@"C:\Test\File_500.txt");
        nodeVm.InputSnapshots.Last().ItemSnapshot.CurrentPath.Should().Be(@"C:\Test\File_999.txt");
    }

    private class MockFlowNode : IFlowNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name => "Mock Node";
        public string Category => "Testing";
        public string Description => "Mock Node for Stress Testing";
        public IReadOnlyList<NodePort> Inputs { get; } = Array.Empty<NodePort>();
        public IReadOnlyList<NodePort> Outputs { get; } = Array.Empty<NodePort>();
        public Dictionary<string, object?> Parameters { get; } = new();
        public Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

