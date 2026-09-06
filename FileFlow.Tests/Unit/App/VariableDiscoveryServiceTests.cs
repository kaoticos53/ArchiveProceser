using System.Windows;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class VariableDiscoveryServiceTests
{
    private readonly VariableDiscoveryService _discoveryService = new();

    private class MockNode : IFlowNode
    {
        public string Id { get; set; } = "mock-1";
        public string Name { get; set; } = "Mock Node";
        public string Category { get; set; } = "General";
        public string Description { get; set; } = "Mock Description";
        public IReadOnlyList<NodePort> Inputs { get; set; } = [new("In", typeof(FileItemContext), PortDirection.Input, "Input")];
        public IReadOnlyList<NodePort> Outputs { get; set; } = [new("Out", typeof(FileItemContext), PortDirection.Output, "Output")];
        public Dictionary<string, object?> Parameters { get; } = [];

        public Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void GetAvailableVariables_ShouldIncludeSystemVariablesAndFunctions()
    {
        // Arrange
        var node = new NodeViewModel(new MockNode(), new Point(0, 0));
        var connections = new List<ConnectionViewModel>();

        // Act
        var variableGroups = _discoveryService.GetAvailableVariables(node, connections);

        // Assert
        variableGroups.Should().NotBeNullOrEmpty();
        variableGroups.Should().Contain(g => g.GroupName.Contains("Sistema"));
        variableGroups.Should().Contain(g => g.GroupName.Contains("Funciones"));
        variableGroups.Should().Contain(g => g.GroupName.Contains("Tamaño"));
        variableGroups.Should().Contain(g => g.GroupName.Contains("Fechas"));

        var systemGroup = variableGroups.First(g => g.GroupName.Contains("Sistema"));
        systemGroup.Variables.Should().Contain(v => v.Name == "FileName");
        systemGroup.Variables.Should().Contain(v => v.Name == "TempDir");
        systemGroup.Variables.Should().Contain(v => v.Name == "RandomId");

        var fnGroup = variableGroups.First(g => g.GroupName.Contains("Funciones"));
        fnGroup.Variables.Should().Contain(v => v.Name == "Sanitize");
        fnGroup.Variables.Should().Contain(v => v.Name == "FormatDate");
    }

    [Fact]
    public void GetAvailableVariables_ShouldDiscoverUpstreamVariableInjectorVariables()
    {
        // Arrange
        var injectorNode = new FileFlow.Plugin.FileSystem.VariableInjectorNode();
        injectorNode.Parameters["CustomProject"] = "Alfa";
        injectorNode.Parameters["ClientCode"] = "CLI_99";

        var injectorVm = new NodeViewModel(injectorNode, new Point(0, 0));
        var targetVm = new NodeViewModel(new MockNode(), new Point(200, 0));

        var outPort = injectorVm.OutputPorts.First();
        var inPort = targetVm.InputPorts.First();
        var conn = new ConnectionViewModel(outPort, inPort);

        // Act
        var variableGroups = _discoveryService.GetAvailableVariables(targetVm, [conn]);

        // Assert
        var injectorGroup = variableGroups.FirstOrDefault(g => g.GroupName.Contains(injectorVm.Title));
        injectorGroup.Should().NotBeNull();
        injectorGroup!.IsUpstream.Should().BeTrue();
        injectorGroup.Variables.Should().Contain(v => v.Name == "CustomProject" && v.Token == "{CustomProject}");
        injectorGroup.Variables.Should().Contain(v => v.Name == "ClientCode" && v.Token == "{ClientCode}");
    }

    [Fact]
    public void GetAvailableVariables_ShouldDiscoverUpstreamImageOptimizerVariables()
    {
        // Arrange
        var optimizerNode = new FileFlow.Plugin.Images.ImageOptimizerNode();
        var optimizerVm = new NodeViewModel(optimizerNode, new Point(0, 0));
        var targetVm = new NodeViewModel(new MockNode(), new Point(200, 0));

        var conn = new ConnectionViewModel(optimizerVm.OutputPorts.First(), targetVm.InputPorts.First());

        // Act
        var variableGroups = _discoveryService.GetAvailableVariables(targetVm, [conn]);

        // Assert
        var group = variableGroups.FirstOrDefault(g => g.GroupName.Contains(optimizerVm.Title));
        group.Should().NotBeNull();
        group!.IsUpstream.Should().BeTrue();
        group.Variables.Should().Contain(v => v.Token == "{OutputFileSize}");
        group.Variables.Should().Contain(v => v.Token == "{OriginalFileSize}");
        group.Variables.Should().Contain(v => v.Token == "{SavedPercent}");
        group.Variables.Should().Contain(v => v.Token == "{OutputFileSizeMB}");
    }

    [Fact]
    public void CreatePreviewItem_ShouldGenerateRealisticSampleDataForEvaluation()
    {
        // Arrange
        var node = new NodeViewModel(new MockNode(), new Point(0, 0));

        // Act
        var previewItem = _discoveryService.CreatePreviewItem(node);

        // Assert
        previewItem.Should().NotBeNull();
        previewItem.FileName.Should().NotBeNullOrWhiteSpace();
        previewItem.Metadata.Should().ContainKey("OriginalFileSize");
        previewItem.Metadata.Should().ContainKey("OutputFileSize");
        previewItem.Metadata.Should().ContainKey("ImageWidth");
        previewItem.Metadata.Should().ContainKey("ImageHeight");

        // Act: Evaluate templates using the preview item
        string evaluated = VariableTemplateResolver.Resolve("{FileName} - {OutputFileSizeMB} MB ({ImageWidth}x{ImageHeight})", previewItem);
        evaluated.Should().NotContain("{FileName}");
        evaluated.Should().NotContain("{OutputFileSizeMB}");
        evaluated.Should().Contain("MB");
        evaluated.Should().Contain("x");
    }

    [Fact]
    public void VariableItem_Properties_ShouldReflectAssignedValues()
    {
        // Arrange & Act
        var item = new VariableItem(
            Name: "TestVar",
            Token: "{TestVar}",
            Description: "Variable de prueba",
            Category: "Pruebas",
            SampleValue: "12345",
            IsUpstream: true,
            SourceNodeTitle: "Nodo Origen"
        );

        // Assert
        item.Name.Should().Be("TestVar");
        item.Token.Should().Be("{TestVar}");
        item.Description.Should().Be("Variable de prueba");
        item.Category.Should().Be("Pruebas");
        item.SampleValue.Should().Be("12345");
        item.IsUpstream.Should().BeTrue();
        item.SourceNodeTitle.Should().Be("Nodo Origen");
    }
}
