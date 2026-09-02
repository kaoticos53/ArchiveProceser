using System.Windows;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class AnnotationViewModelTests
{
    [Fact]
    public void AnnotationViewModel_InitializesWithDefaultValues()
    {
        var annotation = new AnnotationViewModel();

        Assert.False(string.IsNullOrWhiteSpace(annotation.Id));
        Assert.Equal("Nota", annotation.Title);
        Assert.Equal(string.Empty, annotation.Content);
        Assert.Equal("#FEF08A", annotation.Color);
        Assert.Equal(250, annotation.Width);
        Assert.Equal(180, annotation.Height);
    }

    [Fact]
    public void ChangeColorCommand_UpdatesColor()
    {
        var annotation = new AnnotationViewModel();
        annotation.ChangeColor("#BAE6FD");

        Assert.Equal("#BAE6FD", annotation.Color);
    }

    [Fact]
    public void EditorViewModel_AddAndDeleteAnnotation_UpdatesCollection()
    {
        var pluginLoader = new PluginLoader();
        var editor = new EditorViewModel(pluginLoader);

        var note = editor.AddAnnotation(new Point(150, 200), "Revisión", "Comprobar hashes", "#BBF7D0");

        Assert.Single(editor.Annotations);
        Assert.Equal("Revisión", editor.Annotations[0].Title);
        Assert.Equal("Comprobar hashes", editor.Annotations[0].Content);
        Assert.Equal("#BBF7D0", editor.Annotations[0].Color);
        Assert.Equal(150, editor.Annotations[0].Location.X);
        Assert.Equal(200, editor.Annotations[0].Location.Y);

        note.DeleteCommand.Execute(null);

        Assert.Empty(editor.Annotations);
    }

    [Fact]
    public void WorkflowGraphSerializer_ExportsAndImportsAnnotationsCorrectly()
    {
        var pluginLoader = new PluginLoader();
        var editor = new EditorViewModel(pluginLoader);

        editor.AddAnnotation(new Point(100, 150), "Paso 1", "Limpieza de nombres", "#DDD6FE");
        editor.AddAnnotation(new Point(400, 150), "Paso 2", "Compresión ZIP", "#FECDD3");

        var graph = editor.ExportToGraphModel("Test Graph");

        Assert.NotNull(graph.Annotations);
        Assert.Equal(2, graph.Annotations.Count);
        Assert.Equal("Paso 1", graph.Annotations[0].Title);
        Assert.Equal("#DDD6FE", graph.Annotations[0].Color);

        var json = graph.ToJson();
        var deserializedGraph = WorkflowGraph.FromJson(json);

        Assert.Equal(2, deserializedGraph.Annotations.Count);
        Assert.Equal("Paso 2", deserializedGraph.Annotations[1].Title);

        var newEditor = new EditorViewModel(pluginLoader);
        newEditor.LoadFromGraphModel(deserializedGraph);

        Assert.Equal(2, newEditor.Annotations.Count);
        Assert.Equal("Paso 1", newEditor.Annotations[0].Title);
        Assert.Equal("Limpieza de nombres", newEditor.Annotations[0].Content);
        Assert.Equal("#DDD6FE", newEditor.Annotations[0].Color);
    }
}
