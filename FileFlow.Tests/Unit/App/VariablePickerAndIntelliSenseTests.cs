using System.Windows;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class VariablePickerAndIntelliSenseTests
{
    private class DummyNode(string id, string name) : IFlowNode
    {
        public string Id { get; set; } = id;
        public string Name { get; set; } = name;
        public string Category { get; set; } = "Testing";
        public string Description { get; set; } = "Test Dummy Node";
        public IReadOnlyList<NodePort> Inputs { get; set; } = [new("In", typeof(FileItemContext), PortDirection.Input, "Input")];
        public IReadOnlyList<NodePort> Outputs { get; set; } = [new("Out", typeof(FileItemContext), PortDirection.Output, "Output")];
        public Dictionary<string, object?> Parameters { get; } = [];

        public Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void UpstreamTraversal_MultipleChainedNodes_CollectsAllUpstreamVariables()
    {
        // Arrange: ImageOptimizer -> BackgroundRemover -> DummyNode
        var optimizerNode = new FileFlow.Plugin.Images.ImageOptimizerNode();
        var bgRemoverNode = new FileFlow.Plugin.AI.BackgroundRemoverNode();
        var targetNode = new DummyNode("dest-1", "Final Node");

        var optVm = new NodeViewModel(optimizerNode, new Point(0, 0));
        var bgVm = new NodeViewModel(bgRemoverNode, new Point(200, 0));
        var targetVm = new NodeViewModel(targetNode, new Point(400, 0));

        var conn1 = new ConnectionViewModel(optVm.OutputPorts.First(), bgVm.InputPorts.First());
        var conn2 = new ConnectionViewModel(bgVm.OutputPorts.First(), targetVm.InputPorts.First());

        var service = new VariableDiscoveryService();

        // Act
        var groups = service.GetAvailableVariables(targetVm, [conn1, conn2]);

        // Assert
        var upstreamGroups = groups.Where(g => g.IsUpstream).ToList();
        upstreamGroups.Should().HaveCount(2);

        // Optimizer group (identified by unique {SavedPercent})
        var optGroup = upstreamGroups.FirstOrDefault(g => g.Variables.Any(v => v.Token == "{SavedPercent}"));
        optGroup.Should().NotBeNull();
        optGroup!.Variables.Should().Contain(v => v.Token == "{OutputFileSize}");
        optGroup.Variables.Should().Contain(v => v.Token == "{SavedPercent}");

        // Background remover group (identified by unique {AI:BackgroundRemoved})
        var bgGroup = upstreamGroups.FirstOrDefault(g => g.Variables.Any(v => v.Token == "{AI:BackgroundRemoved}"));
        bgGroup.Should().NotBeNull();
        bgGroup!.Variables.Should().Contain(v => v.Token == "{AI:BackgroundRemoved}");
        bgGroup.Variables.Should().Contain(v => v.Token == "{AI:BackgroundModel}");
    }

    [Fact]
    public void CreatePreviewItem_ResolvesSystemAndSizeTemplatesAccurately()
    {
        // Arrange
        var service = new VariableDiscoveryService();
        var node = new NodeViewModel(new DummyNode("dummy-1", "Test"), new Point(0, 0));

        // Act
        var preview = service.CreatePreviewItem(node);

        // Assert
        string template = "File: {FileName}, Output: {OutputFileSizeKB} KB, Temp: {TempDir}";
        string resolved = VariableTemplateResolver.Resolve(template, preview);

        resolved.Should().NotContain("{FileName}");
        resolved.Should().NotContain("{OutputFileSizeKB}");
        resolved.Should().NotContain("{TempDir}");
        resolved.Should().Contain("KB");
    }

    [Fact]
    public void VariablePicker_Categories_FilterCorrectly()
    {
        // Arrange
        var groups = new List<VariableGroupItem>
        {
            new("🌐 Sistema & Rutas (System)", isUpstream: false)
            {
                Variables =
                {
                    new("FileName", "{FileName}", "Nombre del archivo", "Sistema", "test.jpg"),
                    new("TempDir", "{TempDir}", "Directorio temporal", "Sistema", "C:\\Temp")
                }
            },
            new("📅 Fechas & Tiempos", isUpstream: false)
            {
                Variables =
                {
                    new("Date", "{Date}", "Fecha actual", "Fechas", "20260906")
                }
            },
            new("🔗 Image Optimizer", isUpstream: true)
            {
                Variables =
                {
                    new("OutputFileSize", "{OutputFileSize}", "Tamaño salida", "Nodos Anteriores", "1024", IsUpstream: true, SourceNodeTitle: "Image Optimizer")
                }
            }
        };

        // Act & Assert
        var allVars = groups.SelectMany(g => g.Variables).ToList();
        allVars.Should().HaveCount(4);

        var upstreamVars = allVars.Where(v => v.IsUpstream).ToList();
        upstreamVars.Should().ContainSingle();
        upstreamVars[0].Token.Should().Be("{OutputFileSize}");
        upstreamVars[0].SourceNodeTitle.Should().Be("Image Optimizer");

        var dateVars = allVars.Where(v => v.Category.Contains("Fecha")).ToList();
        dateVars.Should().ContainSingle();
        dateVars[0].Token.Should().Be("{Date}");
    }

    [Fact]
    public void VariablePickerViewModel_CategoryFilterAndSearch_WorksCorrectly()
    {
        // Arrange
        var groups = new List<VariableGroupItem>
        {
            new("🌐 Sistema & Rutas", isUpstream: false)
            {
                Variables =
                {
                    new("FileName", "{FileName}", "Nombre del archivo", "Sistema", "sample.txt"),
                    new("TempDir", "{TempDir}", "Directorio temporal", "Sistema", "C:\\Temp")
                }
            },
            new("🔗 Image Optimizer", isUpstream: true)
            {
                Variables =
                {
                    new("OutputFileSize", "{OutputFileSize}", "Tamaño de salida optimizado", "Nodos Anteriores", "1024", IsUpstream: true, SourceNodeTitle: "Image Optimizer")
                }
            }
        };

        var vm = new VariablePickerViewModel(groups);

        // Assert initial
        vm.AllVariables.Should().HaveCount(3);
        vm.FilteredVariables.Should().HaveCount(3);
        vm.HasUpstreamNodes.Should().BeTrue();
        vm.UpstreamCount.Should().Be(1);

        // Filter by upstream
        vm.SetCategory("UPSTREAM");
        vm.FilteredVariables.Should().ContainSingle().Which.Token.Should().Be("{OutputFileSize}");

        // Filter by system
        vm.SetCategory("SYSTEM");
        vm.FilteredVariables.Should().HaveCount(2);

        // Search text
        vm.SetCategory("ALL");
        vm.SearchText = "Temp";
        vm.FilteredVariables.Should().ContainSingle().Which.Token.Should().Be("{TempDir}");

        // Clear search
        vm.ClearSearch();
        vm.FilteredVariables.Should().HaveCount(3);
    }

    [Fact]
    public void VariablePickerViewModel_ShouldDeduplicateByToken_AndCountUniqueUpstreamSources()
    {
        var groups = new List<VariableGroupItem>
        {
            new("🌐 Sistema", isUpstream: false)
            {
                Variables =
                {
                    new("FileName", "{FileName}", "Nombre", "Sistema", "a.txt"),
                    new("FileNameDuplicado", "{FileName}", "Nombre duplicado", "Sistema", "b.txt")
                }
            },
            new("🔗 Upstream A", isUpstream: true)
            {
                Variables =
                {
                    new("OutputFileSize", "{OutputFileSize}", "Size", "Nodos Anteriores", "1024", IsUpstream: true, SourceNodeTitle: "Node A")
                }
            },
            new("🔗 Upstream B", isUpstream: true)
            {
                Variables =
                {
                    new("SavedPercent", "{SavedPercent}", "Saved", "Nodos Anteriores", "66.7", IsUpstream: true, SourceNodeTitle: "Node B")
                }
            }
        };

        var vm = new VariablePickerViewModel(groups);

        vm.AllVariables.Should().HaveCount(3);
        vm.UpstreamCount.Should().Be(2);
        vm.HasUpstreamNodes.Should().BeTrue();
    }

    [Fact]
    public void VariablePickerViewModel_CategoryFilters_DatesSizesFunctions_ShouldWork()
    {
        var groups = new List<VariableGroupItem>
        {
            new("📅 Fechas", isUpstream: false)
            {
                Variables =
                {
                    new("DateNow", "{DateNow}", "Fecha", "Fechas", "2026-09-10")
                }
            },
            new("📐 Tamaños", isUpstream: false)
            {
                Variables =
                {
                    new("SizeMB", "{SizeMB}", "Tamaño en MB", "Tamaños", "3.0")
                }
            },
            new("🔤 Funciones", isUpstream: false)
            {
                Variables =
                {
                    new("Upper", "{Upper(FileName)}", "Función Upper", "Function", "ABC")
                }
            }
        };

        var vm = new VariablePickerViewModel(groups);

        vm.SetCategory("DATES");
        vm.FilteredVariables.Should().ContainSingle().Which.Token.Should().Be("{DateNow}");

        vm.SetCategory("SIZES");
        vm.FilteredVariables.Should().ContainSingle().Which.Token.Should().Be("{SizeMB}");

        vm.SetCategory("FUNCTIONS");
        vm.FilteredVariables.Should().ContainSingle().Which.Token.Should().Be("{Upper(FileName)}");
    }

    [Fact]
    public void VariablePickerViewModel_SelectingNullVariable_ShouldClearDetail()
    {
        var groups = new List<VariableGroupItem>
        {
            new("🌐 Sistema", isUpstream: false)
            {
                Variables =
                {
                    new("FileName", "{FileName}", "Nombre", "Sistema", "sample.txt")
                }
            }
        };

        var vm = new VariablePickerViewModel(groups);
        vm.SelectedVariable = vm.FilteredVariables.First();

        vm.HasSelectedVariable.Should().BeTrue();
        vm.SelectedVariable = null;

        vm.HasSelectedVariable.Should().BeFalse();
        vm.DetailToken.Should().BeEmpty();
        vm.DetailDescription.Should().BeEmpty();
        vm.DetailCategory.Should().BeEmpty();
        vm.DetailSource.Should().BeEmpty();
        vm.DetailEvaluated.Should().BeEmpty();
    }
}
