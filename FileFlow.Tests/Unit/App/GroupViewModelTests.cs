using System.Windows;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class GroupViewModelTests
{
    [Fact]
    public void GroupViewModel_InitializesWithDefaultValues()
    {
        var group = new GroupViewModel();

        Assert.False(string.IsNullOrWhiteSpace(group.Id));
        Assert.Equal("Grupo de Nodos", group.Title);
        Assert.Equal("#3B82F6", group.Color);
        Assert.Equal(450, group.Width);
        Assert.Equal(320, group.Height);
    }

    [Fact]
    public void ChangeColorCommand_UpdatesColor()
    {
        var group = new GroupViewModel();
        group.ChangeColor("#10B981");

        Assert.Equal("#10B981", group.Color);
    }

    [Fact]
    public void EditorViewModel_AddAndDeleteGroup_UpdatesCollection()
    {
        var pluginLoader = new PluginLoader();
        var editor = new EditorViewModel(pluginLoader);

        var group = editor.AddGroup(new Point(200, 300), "Fase 1", 500, 350, "#8B5CF6", ["node-1", "node-2"]);

        Assert.Single(editor.Groups);
        Assert.Equal("Fase 1", editor.Groups[0].Title);
        Assert.Equal("#8B5CF6", editor.Groups[0].Color);
        Assert.Equal(2, editor.Groups[0].NodeIds.Count);
        Assert.Contains(group, editor.CanvasDecorators);

        group.DeleteCommand.Execute(null);

        Assert.Empty(editor.Groups);
        Assert.DoesNotContain(group, editor.CanvasDecorators);
    }

    [Fact]
    public void WorkflowGraphSerializer_ExportsAndImportsGroupsCorrectly()
    {
        var pluginLoader = new PluginLoader();
        var editor = new EditorViewModel(pluginLoader);

        editor.AddGroup(new Point(100, 100), "Entrada", 400, 300, "#3B82F6", ["src-node"]);
        editor.AddGroup(new Point(600, 100), "Salida", 400, 300, "#EF4444", ["sink-node"]);

        var graph = editor.ExportToGraphModel("Test Graph with Groups");

        Assert.NotNull(graph.Groups);
        Assert.Equal(2, graph.Groups.Count);
        Assert.Equal("Entrada", graph.Groups[0].Title);
        Assert.Equal("#3B82F6", graph.Groups[0].Color);

        var json = graph.ToJson();
        var deserializedGraph = WorkflowGraph.FromJson(json);

        Assert.Equal(2, deserializedGraph.Groups.Count);
        Assert.Equal("Salida", deserializedGraph.Groups[1].Title);

        var newEditor = new EditorViewModel(pluginLoader);
        newEditor.LoadFromGraphModel(deserializedGraph);

        Assert.Equal(2, newEditor.Groups.Count);
        Assert.Equal("Entrada", newEditor.Groups[0].Title);
        Assert.Equal("#3B82F6", newEditor.Groups[0].Color);
        Assert.Equal(2, newEditor.CanvasDecorators.Count);
    }
}
