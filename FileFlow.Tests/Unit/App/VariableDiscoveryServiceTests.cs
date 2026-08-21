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
}
