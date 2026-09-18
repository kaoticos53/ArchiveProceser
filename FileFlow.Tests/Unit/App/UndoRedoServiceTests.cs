using Avalonia;
using FileFlow.App.Services.UndoRedo;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Images;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Pruebas unitarias completas para el motor de Undo/Redo y las acciones reversibles del editor DAG.
/// </summary>
public class UndoRedoServiceTests
{
    [Fact]
    public void UndoRedoService_InitialState_ShouldNotAllowUndoOrRedo()
    {
        var service = new UndoRedoService();

        service.CanUndo.Should().BeFalse();
        service.CanRedo.Should().BeFalse();
        service.NextUndoDescription.Should().BeNull();
        service.NextRedoDescription.Should().BeNull();
    }

    [Fact]
    public void Record_SingleAction_ShouldAllowUndo_AndClearRedo()
    {
        var service = new UndoRedoService();
        var wasUndone = false;
        var wasRedone = false;

        var mockAction = new MockUndoableAction("Test Action", () => wasUndone = true, () => wasRedone = true);
        service.Record(mockAction);

        service.CanUndo.Should().BeTrue();
        service.CanRedo.Should().BeFalse();
        service.NextUndoDescription.Should().Be("Test Action");

        service.Undo();
        wasUndone.Should().BeTrue();
        service.CanUndo.Should().BeFalse();
        service.CanRedo.Should().BeTrue();
        service.NextRedoDescription.Should().Be("Test Action");

        service.Redo();
        wasRedone.Should().BeTrue();
        service.CanUndo.Should().BeTrue();
        service.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void BeginTransaction_ShouldGroupMultipleActionsIntoSingleUndoStep()
    {
        var service = new UndoRedoService();
        int step1Undo = 0, step1Redo = 0;
        int step2Undo = 0, step2Redo = 0;

        using (service.BeginTransaction("Batch Operation"))
        {
            service.Record(new MockUndoableAction("Step 1", () => step1Undo++, () => step1Redo++));
            service.Record(new MockUndoableAction("Step 2", () => step2Undo++, () => step2Redo++));
        }

        service.CanUndo.Should().BeTrue();
        service.NextUndoDescription.Should().Be("Batch Operation");

        service.Undo();
        step1Undo.Should().Be(1);
        step2Undo.Should().Be(1);
        service.CanUndo.Should().BeFalse();
        service.CanRedo.Should().BeTrue();

        service.Redo();
        step1Redo.Should().Be(1);
        step2Redo.Should().Be(1);
        service.CanUndo.Should().BeTrue();
        service.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void CapacityLimit_ShouldTrimOldestActionsWhenExceeded()
    {
        var service = new UndoRedoService(maxCapacity: 3);

        service.Record(new MockUndoableAction("Action 1", () => { }, () => { }));
        service.Record(new MockUndoableAction("Action 2", () => { }, () => { }));
        service.Record(new MockUndoableAction("Action 3", () => { }, () => { }));
        service.Record(new MockUndoableAction("Action 4", () => { }, () => { }));

        service.NextUndoDescription.Should().Be("Action 4");
        service.Undo();
        service.NextUndoDescription.Should().Be("Action 3");
        service.Undo();
        service.NextUndoDescription.Should().Be("Action 2");
        service.Undo();
        service.CanUndo.Should().BeFalse(); // Action 1 was discarded
    }

    [Fact]
    public void EditorViewModel_AddNode_UndoRedo_ShouldWorkCorrectly()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        var editor = new EditorViewModel(loader);

        var node = editor.AddNode(typeof(FolderSourceNode).FullName!, new Point(100, 100));
        node.Should().NotBeNull();
        editor.Nodes.Should().Contain(node!);
        editor.CanUndo.Should().BeTrue();

        editor.Undo();
        editor.Nodes.Should().NotContain(node!);
        editor.CanUndo.Should().BeFalse();
        editor.CanRedo.Should().BeTrue();

        editor.Redo();
        editor.Nodes.Should().Contain(node!);
        editor.CanUndo.Should().BeTrue();
        editor.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void EditorViewModel_DeleteNodeWithConnections_UndoRedo_ShouldRestoreNodeAndConnections()
    {
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var node1 = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { ParentEditor = editor };
        var node2 = new NodeViewModel(new DestinationSinkNode(), new Point(300, 0)) { ParentEditor = editor };
        editor.Nodes.Add(node1);
        editor.Nodes.Add(node2);

        var outPort = node1.OutputPorts.First();
        var inPort = node2.InputPorts.First();
        editor.CreateConnection(outPort, inPort);

        editor.Nodes.Should().HaveCount(2);
        editor.Connections.Should().HaveCount(1);

        // Delete node1
        editor.DeleteNode(node1);
        editor.Nodes.Should().NotContain(node1);
        editor.Connections.Should().BeEmpty();

        // Undo deletion
        editor.Undo();
        editor.Nodes.Should().Contain(node1);
        editor.Connections.Should().HaveCount(1);
        editor.Connections.First().Source.Should().Be(outPort);
        editor.Connections.First().Target.Should().Be(inPort);

        // Redo deletion
        editor.Redo();
        editor.Nodes.Should().NotContain(node1);
        editor.Connections.Should().BeEmpty();
    }

    [Fact]
    public void EditorViewModel_CreateConnection_UndoRedo_ShouldConnectAndDisconnect()
    {
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var node1 = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { ParentEditor = editor };
        var node2 = new NodeViewModel(new DestinationSinkNode(), new Point(300, 0)) { ParentEditor = editor };
        editor.Nodes.Add(node1);
        editor.Nodes.Add(node2);

        var outPort = node1.OutputPorts.First();
        var inPort = node2.InputPorts.First();

        editor.CreateConnection(outPort, inPort);
        editor.Connections.Should().HaveCount(1);

        editor.Undo();
        editor.Connections.Should().BeEmpty();

        editor.Redo();
        editor.Connections.Should().HaveCount(1);
    }

    [Fact]
    public void EditorViewModel_MoveNodes_UndoRedo_ShouldRestorePositions()
    {
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var node = new NodeViewModel(new FolderSourceNode(), new Point(50, 50)) { ParentEditor = editor };
        editor.Nodes.Add(node);

        var oldPos = new Point(50, 50);
        var newPos = new Point(250, 300);

        node.Location = newPos;
        editor.UndoRedoService.Record(new MoveNodesAction([new NodeMoveItem(node, oldPos, newPos)]));

        node.Location.Should().Be(newPos);
        editor.CanUndo.Should().BeTrue();

        editor.Undo();
        node.Location.Should().Be(oldPos);

        editor.Redo();
        node.Location.Should().Be(newPos);
    }

    [Fact]
    public void EditorViewModel_ChangeParameter_UndoRedo_ShouldUpdateValue()
    {
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var node = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { ParentEditor = editor };
        editor.Nodes.Add(node);

        var pathParam = node.Parameters.FirstOrDefault(p => p.Key.Equals("FolderPath", StringComparison.OrdinalIgnoreCase) || p.Key.Equals("Path", StringComparison.OrdinalIgnoreCase))
            ?? node.Parameters.First();

        var initialValue = pathParam.Value;
        pathParam.Value = @"C:\TestInputPath";

        editor.CanUndo.Should().BeTrue();

        editor.Undo();
        pathParam.Value.Should().Be(initialValue);

        editor.Redo();
        pathParam.Value.Should().Be(@"C:\TestInputPath");
    }

    [Fact]
    public void EditorViewModel_AnnotationsAndGroups_UndoRedo_ShouldAddAndRemove()
    {
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        // Annotation
        var annotation = editor.AddAnnotation(new Point(100, 100), "Nota de Prueba", "Contenido", "#FEF08A");
        editor.Annotations.Should().Contain(annotation);

        editor.Undo();
        editor.Annotations.Should().NotContain(annotation);

        editor.Redo();
        editor.Annotations.Should().Contain(annotation);

        // Group
        var group = editor.AddGroup(new Point(200, 200), "Grupo de Prueba");
        editor.Groups.Should().Contain(group);

        editor.Undo();
        editor.Groups.Should().NotContain(group);

        editor.Redo();
        editor.Groups.Should().Contain(group);
    }

    private sealed class MockUndoableAction : IUndoableAction
    {
        private readonly Action _onUndo;
        private readonly Action _onRedo;

        public string Description { get; }

        public MockUndoableAction(string description, Action onUndo, Action onRedo)
        {
            Description = description;
            _onUndo = onUndo;
            _onRedo = onRedo;
        }

        public void Undo() => _onUndo();
        public void Redo() => _onRedo();
    }
}
