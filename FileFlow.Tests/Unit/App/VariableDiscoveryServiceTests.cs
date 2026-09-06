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

    [Fact]
    public void GetAvailableFileVersions_ShouldIncludeStandardVersionsAndDiscoverUpstreamOptimized()
    {
        // Arrange
        var optimizerNode = new FileFlow.Plugin.Images.ImageOptimizerNode();
        var optimizerVm = new NodeViewModel(optimizerNode, new Point(0, 0));
        var targetVm = new NodeViewModel(new MockNode(), new Point(200, 0));

        var conn = new ConnectionViewModel(optimizerVm.OutputPorts.First(), targetVm.InputPorts.First());

        // Act
        var versions = _discoveryService.GetAvailableFileVersions(targetVm, [conn]);

        // Assert
        versions.Should().NotBeNullOrEmpty();
        versions.Should().Contain(v => v.Tag == "Original" && v.Token == "{OriginalPath}");
        versions.Should().Contain(v => v.Tag == "Current" && v.Token == "{CurrentPath}");
        versions.Should().Contain(v => v.Tag == "Optimized" && v.Token == "{File:Optimized}" && v.IsUpstream);
    }

    [Fact]
    public void GetAvailableFileVersions_ShouldDiscoverAllUpstreamVersionsInChain()
    {
        // Arrange
        var bgNode = new FileFlow.Plugin.AI.BackgroundRemoverNode();
        var bgVm = new NodeViewModel(bgNode, new Point(0, 0));

        var optNode = new FileFlow.Plugin.Images.ImageOptimizerNode();
        var optVm = new NodeViewModel(optNode, new Point(200, 0));

        var targetNode = new FileFlow.Plugin.Logic.BestVersionSelectorNode();
        var targetVm = new NodeViewModel(targetNode, new Point(400, 0));

        var conn1 = new ConnectionViewModel(bgVm.OutputPorts.First(p => p.Name == "Out"), optVm.InputPorts.First(p => p.Name == "In"));
        var conn2 = new ConnectionViewModel(optVm.OutputPorts.First(p => p.Name == "Out"), targetVm.InputPorts.First(p => p.Name == "In"));

        // Act
        var versions = _discoveryService.GetAvailableFileVersions(targetVm, [conn1, conn2]);

        // Assert
        versions.Should().Contain(v => v.Tag == "Optimized" && v.Token == "{File:Optimized}");
        versions.Should().Contain(v => v.Tag == "NoBackground" && v.Token == "{File:NoBackground}");
    }

    [Fact]
    public void LoadFromGraphModel_WithUserGraph_ShouldExposeBothOptimizedAndNoBackgroundVersions()
    {
        string json = """
        {
          "Name": "Flujo de Procesamiento de Archivos",
          "GlobalOutputDir": "E:\\---- Test Data\\Salida",
          "TemporaryDirectory": "",
          "Nodes": [
            {
              "Id": "d182950e-62bc-4da1-aaa2-8be5fe1f9df5",
              "NodeTypeName": "FileFlow.Plugin.FileSystem.FolderSourceNode",
              "CustomTitle": null,
              "X": -32,
              "Y": 247,
              "HasBreakpoint": false,
              "IsLoggingEnabled": true,
              "Parameters": {}
            },
            {
              "Id": "294c475a-fe67-49cc-95fc-e9eed1801100",
              "NodeTypeName": "FileFlow.Plugin.Images.ImageOptimizerNode",
              "CustomTitle": null,
              "X": 405,
              "Y": 174,
              "HasBreakpoint": false,
              "IsLoggingEnabled": true,
              "Parameters": {}
            },
            {
              "Id": "3fd29794-75b6-4493-aac5-3fc3d442842b",
              "NodeTypeName": "FileFlow.Plugin.Logic.BestVersionSelectorNode",
              "CustomTitle": null,
              "X": 721,
              "Y": 52,
              "HasBreakpoint": false,
              "IsLoggingEnabled": true,
              "Parameters": {
                "CandidateA": "{CurrentPath}",
                "CandidateB": "{OriginalPath}"
              }
            },
            {
              "Id": "972b5374-9bc4-41e6-85ba-5eb4ed32e737",
              "NodeTypeName": "FileFlow.Plugin.AI.BackgroundRemoverNode",
              "CustomTitle": null,
              "X": 135,
              "Y": 12,
              "HasBreakpoint": false,
              "IsLoggingEnabled": true,
              "Parameters": {}
            }
          ],
          "Edges": [
            {
              "Id": "c30158b6-50b1-4d1d-8bc8-bac4648fa06c",
              "SourceNodeId": "d182950e-62bc-4da1-aaa2-8be5fe1f9df5",
              "SourcePortName": "Out",
              "TargetNodeId": "972b5374-9bc4-41e6-85ba-5eb4ed32e737",
              "TargetPortName": "In"
            },
            {
              "Id": "874e5b48-54d2-421f-b2b0-ad57df13383f",
              "SourceNodeId": "972b5374-9bc4-41e6-85ba-5eb4ed32e737",
              "SourcePortName": "Out",
              "TargetNodeId": "294c475a-fe67-49cc-95fc-e9eed1801100",
              "TargetPortName": "In"
            },
            {
              "Id": "bb4b7a99-7ce7-4b34-9383-295267eaec95",
              "SourceNodeId": "294c475a-fe67-49cc-95fc-e9eed1801100",
              "SourcePortName": "Out",
              "TargetNodeId": "3fd29794-75b6-4493-aac5-3fc3d442842b",
              "TargetPortName": "In"
            }
          ]
        }
        """;

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new FileFlow.Sdk.Serialization.ObjectToInferredTypesConverter() }
        };
        var graph = System.Text.Json.JsonSerializer.Deserialize<FileFlow.Core.Engine.WorkflowGraph>(json, options)!;

        var loader = new FileFlow.Core.Plugins.PluginLoader();
        loader.RegisterNodeType<FileFlow.Plugin.FileSystem.FolderSourceNode>();
        loader.RegisterNodeType<FileFlow.Plugin.Images.ImageOptimizerNode>();
        loader.RegisterNodeType<FileFlow.Plugin.Logic.BestVersionSelectorNode>();
        loader.RegisterNodeType<FileFlow.Plugin.AI.BackgroundRemoverNode>();

        var editor = new EditorViewModel(loader);
        editor.LoadFromGraphModel(graph);

        var bestNode = editor.Nodes.First(n => n.NodeTypeName.Contains("BestVersionSelectorNode"));
        var paramA = bestNode.Parameters.First(p => p.Key == "CandidateA");

        var versions = paramA.AvailableVersionOptions;
        versions.Should().Contain(v => v.Tag == "Optimized");
        versions.Should().Contain(v => v.Tag == "NoBackground");
    }

    [Fact]
    public void ConnectingUpstreamNodeLater_ShouldUpdateAvailableVersionsReactively()
    {
        // Arrange
        var loader = new FileFlow.Core.Plugins.PluginLoader();
        loader.RegisterNodeType<FileFlow.Plugin.Images.ImageOptimizerNode>();
        loader.RegisterNodeType<FileFlow.Plugin.Logic.BestVersionSelectorNode>();
        loader.RegisterNodeType<FileFlow.Plugin.AI.BackgroundRemoverNode>();

        var editor = new EditorViewModel(loader);

        var optNode = editor.AddNode("FileFlow.Plugin.Images.ImageOptimizerNode", new Point(100, 0))!;
        var bestNode = editor.AddNode("FileFlow.Plugin.Logic.BestVersionSelectorNode", new Point(300, 0))!;

        // 1. Connect ImageOptimizer -> BestVersionSelector
        editor.CreateConnection(optNode.OutputPorts.First(p => p.Name == "Out"), bestNode.InputPorts.First(p => p.Name == "In"));

        var paramA = bestNode.Parameters.First(p => p.Key == "CandidateA");
        paramA.AvailableVersionOptions.Should().Contain(v => v.Tag == "Optimized");
        paramA.AvailableVersionOptions.Should().NotContain(v => v.Tag == "NoBackground");

        // 2. Later add and connect BackgroundRemover -> ImageOptimizer
        var bgNode = editor.AddNode("FileFlow.Plugin.AI.BackgroundRemoverNode", new Point(0, 0))!;
        editor.CreateConnection(bgNode.OutputPorts.First(p => p.Name == "Out"), optNode.InputPorts.First(p => p.Name == "In"));

        // 3. Assert reactive update
        paramA.AvailableVersionOptions.Should().Contain(v => v.Tag == "Optimized");
        paramA.AvailableVersionOptions.Should().Contain(v => v.Tag == "NoBackground");
    }
}
