using System.Diagnostics;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace FileFlow.Tests.Performance;

/// <summary>
/// Estrés del resolvedor de plantillas y del historial de snapshots del nodo.
///
/// <para>El umbral de tiempo usa el patrón calibrado del suite (<see cref="CalibratedBenchmark"/>): la
/// medición se compara contra lo que esta máquina acaba de demostrar, no contra un número fijo que sólo
/// vale en la máquina que lo escribió. El test de snapshots es determinista (sin timing) pero su view
/// model se suscribe al singleton de localización: la prueba dispone el suscriptor con
/// <c>Cleanup</c> para no dejar zombis tras de sí.</para>
/// </summary>
public class PerformanceStressTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private FileFlow.App.ViewModels.NodeViewModel? _nodeUnderTest;

    public PerformanceStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        // El ctor de NodeViewModel se suscribe eternamente a LocalizationManager.LanguageChanged; sólo
        // Cleanup() desuscribe. Sin esto, cada pasada de la prueba deja un zombi del proceso.
        _nodeUnderTest?.Cleanup();
        _nodeUnderTest = null;
    }

    [Fact]
    public void Resolve_ShouldNotRegressOn10000Items()
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

        CalibratedBenchmark.MeasureAndAssert(
            _output,
            "TemplateResolver (10.000 interpolaciones)",
            unitsPerMeasurement: itemQuantity,
            unitName: "ops",
            work: () =>
            {
                foreach (var item in items)
                {
                    _ = VariableTemplateResolver.Resolve(template, item);
                }
            });
    }

    [Fact]
    public void NodeViewModel_SnapshotHistory_ShouldBeTrimmedToMaxRecordedSnapshots_UnderHighLoad()
    {
        // Arrange
        var mockNode = new MockFlowNode();
        _nodeUnderTest = new FileFlow.App.ViewModels.NodeViewModel(mockNode, new Avalonia.Point(0, 0));
        var nodeVm = _nodeUnderTest;

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
