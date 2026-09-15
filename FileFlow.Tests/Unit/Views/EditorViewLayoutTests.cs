using Avalonia;
using FileFlow.App.Converters;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

public class EditorViewLayoutTests
{
    private PluginLoader CreateLoader()
    {
        var pluginLoader = new PluginLoader();
        pluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.FileSystem.FolderSourceNode).Assembly);
        pluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Logic.ExpressionFilterNode).Assembly);
        pluginLoader.ScanCurrentAppDomain();
        return pluginLoader;
    }

    [Fact]
    public void AddNode_ViewModelOnly_CompletesInstantly()
    {
        var pluginLoader = CreateLoader();
        var editorVm = new EditorViewModel(pluginLoader);

        var node = editorVm.AddNode("FolderSourceNode", new Point(150, 200));

        node.Should().NotBeNull();
        node!.Title.Should().NotBeNullOrWhiteSpace();
        node.Location.Should().Be(new Point(150, 200));
        node.InputPorts.Should().BeEmpty();
        node.OutputPorts.Should().HaveCount(1);
    }

    [Fact]
    public void Converters_UsedByNodeCard_ShouldWorkCorrectly()
    {
        var boolConv = new StringEqualsToBooleanConverter();
        boolConv.Convert("Running", typeof(bool), "Running", null!).Should().Be(true);
        boolConv.Convert("Idle", typeof(bool), "Running", null!).Should().Be(false);

        var msConv = new DurationMsToTextConverter();
        msConv.Convert(1500L, typeof(string), null!, null!).Should().NotBeNull();

        var bytesConv = new BytesToTextConverter();
        bytesConv.Convert(1048576L, typeof(string), null!, null!).Should().NotBeNull();
    }

    [Fact]
    public void WorkflowGraph_LoadAndConnectNodes_ShouldMaintainTopology()
    {
        var pluginLoader = CreateLoader();
        var editorVm = new EditorViewModel(pluginLoader);

        var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100));
        var node2 = editorVm.AddNode("DestinationSinkNode", new Point(500, 100));
        node1.Should().NotBeNull();
        node2.Should().NotBeNull();

        node1!.OutputPorts.Should().NotBeEmpty();
        node2!.InputPorts.Should().NotBeEmpty();

        editorVm.CreateConnection(node1.OutputPorts[0], node2.InputPorts[0]);
        editorVm.Connections.Should().HaveCount(1);

        // Add annotation & group
        editorVm.AddAnnotation(new Point(100, 350), "Nota de Prueba", "Contenido");
        editorVm.AddGroup(new Point(50, 50), "Grupo de prueba", 800, 400);

        // Export and reload
        var exportedGraph = editorVm.ExportToGraphModel("Test Graph");
        editorVm.LoadFromGraphModel(exportedGraph);

        editorVm.Nodes.Should().HaveCount(2);
        editorVm.Connections.Should().HaveCount(1);
        editorVm.Annotations.Should().HaveCount(1);
        editorVm.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Node_UpdateWidth_ShouldClampAndNotify()
    {
        var pluginLoader = CreateLoader();
        var editorVm = new EditorViewModel(pluginLoader);

        var node = editorVm.AddNode("FolderSourceNode", new Point(100, 100));
        node.Should().NotBeNull();

        double initialWidth = node!.Width;
        initialWidth.Should().BeGreaterThan(0);

        // Update width within limits
        node.UpdateWidth(350);
        node.Width.Should().Be(350);

        // Test lower bound clamp (180)
        node.UpdateWidth(50);
        node.Width.Should().Be(180);

        // Test upper bound clamp (600)
        node.UpdateWidth(1000);
        node.Width.Should().Be(600);
    }

    [Fact]
    public void Ports_ConnectionState_ShouldUpdateCorrectlyOnConnectAndDisconnect()
    {
        var pluginLoader = CreateLoader();
        var editorVm = new EditorViewModel(pluginLoader);

        var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100));
        var node2 = editorVm.AddNode("DestinationSinkNode", new Point(500, 100));

        var outPort = node1!.OutputPorts[0];
        var inPort = node2!.InputPorts[0];

        outPort.IsConnected.Should().BeFalse();
        inPort.IsConnected.Should().BeFalse();

        // Connect
        editorVm.CreateConnection(outPort, inPort);
        outPort.IsConnected.Should().BeTrue();
        inPort.IsConnected.Should().BeTrue();

        // Disconnect
        editorVm.DisconnectConnector(outPort);
        outPort.IsConnected.Should().BeFalse();
        inPort.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void NodifyConnectionCommands_ShouldHandleValueTuplesAndDirectPorts()
    {
        var pluginLoader = CreateLoader();
        var editorVm = new EditorViewModel(pluginLoader);

        var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100));
        var node2 = editorVm.AddNode("DestinationSinkNode", new Point(500, 100));

        var outPort = node1!.OutputPorts[0];
        var inPort = node2!.InputPorts[0];

        // 1. Start Connection via direct port
        editorVm.StartConnection(outPort);
        editorVm.PendingConnection.Should().NotBeNull();
        editorVm.PendingConnection!.Source.Should().Be(outPort);

        // 2. Finish Connection via Nodify ValueTuple (Source, Target)
        editorVm.FinishConnection((outPort, inPort));
        editorVm.Connections.Should().HaveCount(1);
        editorVm.PendingConnection.Should().BeNull();
        outPort.IsConnected.Should().BeTrue();
        inPort.IsConnected.Should().BeTrue();

        // 3. Disconnect via ValueTuple
        editorVm.DisconnectConnector((outPort, inPort));
        editorVm.Connections.Should().BeEmpty();
        outPort.IsConnected.Should().BeFalse();
        inPort.IsConnected.Should().BeFalse();

        // 4. Start & Finish Connection via Tuple with only Target (using PendingConnection.Source)
        editorVm.StartConnection(outPort);
        editorVm.FinishConnection(inPort);
        editorVm.Connections.Should().HaveCount(1);
        outPort.IsConnected.Should().BeTrue();
        inPort.IsConnected.Should().BeTrue();
    }
}
