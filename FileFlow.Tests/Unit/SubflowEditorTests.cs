using Avalonia;
using FileFlow.App.Services.UndoRedo;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Logic;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit;

/// <summary>
/// Pruebas unitarias para la interacción, navegación de migas de pan y colapso de selecciones a subflujo en el Editor.
/// </summary>
public class SubflowEditorTests
{
    [Fact]
    public void Breadcrumbs_OpenSubflowAndNavigateBack_ShouldManageHierarchyCleanly()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);

        var undoRedo = new UndoRedoService();
        var editor = new EditorViewModel(loader, undoRedoService: undoRedo);

        var subflowNode = new SubflowNode { SubflowName = "Child Subflow" };
        var subflowVm = new NodeViewModel(subflowNode, new Point(100, 100))
        {
            ParentEditor = editor,
            Title = "Child Subflow"
        };
        editor.Nodes.Add(subflowVm);

        editor.Breadcrumbs.Count.Should().Be(0);
        editor.HasBreadcrumbs.Should().BeFalse();

        // Act 1: Abrir subflujo
        editor.OpenSubflow(subflowVm);

        // Assert 1
        editor.Breadcrumbs.Count.Should().Be(2);
        editor.HasBreadcrumbs.Should().BeTrue();
        editor.Breadcrumbs[0].Name.Should().Be("Root Workflow");
        editor.Breadcrumbs[1].Name.Should().Be("Child Subflow");
        editor.CurrentWorkflowTitle.Should().Be("Child Subflow");
        editor.Nodes.Count.Should().Be(2); // SubflowInputNode + SubflowOutputNode generados por defecto

        // Act 2: Volver al flujo raíz
        var rootBreadcrumb = editor.Breadcrumbs[0];
        editor.NavigateToBreadcrumb(rootBreadcrumb);

        // Assert 2
        editor.Breadcrumbs.Count.Should().Be(1);
        editor.HasBreadcrumbs.Should().BeFalse();
        editor.CurrentWorkflowTitle.Should().Be("Root Workflow");
        editor.Nodes.Count.Should().Be(1);
        editor.Nodes[0].Title.Should().Be("Child Subflow");
    }

    [Fact]
    public void CollapseSelectionToSubflow_ShouldReplaceSelectedNodesWithSubflowNode_AndSupportUndo()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);

        var undoRedo = new UndoRedoService();
        var editor = new EditorViewModel(loader, undoRedoService: undoRedo);

        var srcNode = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { ParentEditor = editor, Title = "Source" };
        var middle1 = new NodeViewModel(new VariableInjectorNode(), new Point(200, 0)) { ParentEditor = editor, Title = "Middle1" };
        var middle2 = new NodeViewModel(new VariableInjectorNode(), new Point(400, 0)) { ParentEditor = editor, Title = "Middle2" };
        var sinkNode = new NodeViewModel(new DestinationSinkNode(), new Point(600, 0)) { ParentEditor = editor, Title = "Sink" };

        editor.Nodes.Add(srcNode);
        editor.Nodes.Add(middle1);
        editor.Nodes.Add(middle2);
        editor.Nodes.Add(sinkNode);

        var conn1 = new ConnectionViewModel(srcNode.OutputPorts[0], middle1.InputPorts[0]);
        var conn2 = new ConnectionViewModel(middle1.OutputPorts[0], middle2.InputPorts[0]);
        var conn3 = new ConnectionViewModel(middle2.OutputPorts[0], sinkNode.InputPorts[0]);

        editor.Connections.Add(conn1);
        editor.Connections.Add(conn2);
        editor.Connections.Add(conn3);

        // Seleccionar los 2 nodos intermedios
        middle1.IsSelected = true;
        middle2.IsSelected = true;

        // Act: Colapsar a subflujo
        editor.CollapseSelectionToSubflow();

        // Assert: Ahora hay 3 nodos (Source, SubflowNode, Sink)
        editor.Nodes.Count.Should().Be(3);
        editor.Nodes.Should().Contain(n => n.IsSubflowNode);
        editor.Connections.Count.Should().Be(2);

        var subflowNodeVm = editor.Nodes.First(n => n.IsSubflowNode);
        editor.Connections.Should().Contain(c => c.Source.NodeOwner == srcNode && c.Target.NodeOwner == subflowNodeVm);
        editor.Connections.Should().Contain(c => c.Source.NodeOwner == subflowNodeVm && c.Target.NodeOwner == sinkNode);

        // Act 2: Deshacer (Undo)
        undoRedo.CanUndo.Should().BeTrue();
        undoRedo.Undo();

        // Assert 2: Nodos restaurados
        editor.Nodes.Count.Should().Be(4);
        editor.Nodes.Should().Contain(middle1);
        editor.Nodes.Should().Contain(middle2);
        editor.Connections.Count.Should().Be(3);
    }
}

