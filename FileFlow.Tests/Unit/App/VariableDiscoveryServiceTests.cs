using System.Windows;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
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
        variableGroups.Should().Contain(g => g.GroupName.Contains("System & Environment"));
        variableGroups.Should().Contain(g => g.GroupName.Contains("Expression Functions"));

        var systemGroup = variableGroups.First(g => g.GroupName.Contains("System & Environment"));
        systemGroup.Variables.Should().Contain(v => v.Name == "FileName");
        systemGroup.Variables.Should().Contain(v => v.Name == "DateNow");

        var fnGroup = variableGroups.First(g => g.GroupName.Contains("Expression Functions"));
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
        injectorGroup!.Variables.Should().Contain(v => v.Name == "CustomProject" && v.Token == "{CustomProject}");
        injectorGroup.Variables.Should().Contain(v => v.Name == "ClientCode" && v.Token == "{ClientCode}");
    }
}
