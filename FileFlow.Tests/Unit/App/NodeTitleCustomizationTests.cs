using System.IO;
using System.Windows;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Images;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class NodeTitleCustomizationTests
{
    private readonly PluginLoader _loader = new();

    [Fact]
    public void StartRenaming_ShouldInitializeEditingTitleTextAndSetIsEditingTitleToTrue()
    {
        var node = new ImageOptimizerNode();
        var nodeVm = new NodeViewModel(node, new Point(0, 0));

        nodeVm.IsEditingTitle.Should().BeFalse();
        nodeVm.Title.Should().Be(node.Name);

        nodeVm.StartRenaming();

        nodeVm.IsEditingTitle.Should().BeTrue();
        nodeVm.EditingTitleText.Should().Be(node.Name);
    }

    [Fact]
    public void CommitTitleRename_WithValidCustomText_ShouldUpdateCustomTitleAndTitle()
    {
        var node = new ImageOptimizerNode();
        var nodeVm = new NodeViewModel(node, new Point(0, 0));

        nodeVm.StartRenaming();
        nodeVm.EditingTitleText = "  Optimizador de Alta Prioridad  ";
        nodeVm.CommitTitleRename();

        nodeVm.IsEditingTitle.Should().BeFalse();
        nodeVm.CustomTitle.Should().Be("Optimizador de Alta Prioridad");
        nodeVm.Title.Should().Be("Optimizador de Alta Prioridad");
    }

    [Fact]
    public void CommitTitleRename_WithEmptyOrOriginalName_ShouldResetCustomTitleToNullAndRestoreOriginalDescriptorName()
    {
        var node = new ImageOptimizerNode();
        var nodeVm = new NodeViewModel(node, new Point(0, 0));

        // 1. Asignar título custom
        nodeVm.StartRenaming();
        nodeVm.EditingTitleText = "Mi Nodo";
        nodeVm.CommitTitleRename();
        nodeVm.CustomTitle.Should().Be("Mi Nodo");

        // 2. Renombrar con espacios vacíos -> debe volver al default
        nodeVm.StartRenaming();
        nodeVm.EditingTitleText = "   ";
        nodeVm.CommitTitleRename();

        nodeVm.IsEditingTitle.Should().BeFalse();
        nodeVm.CustomTitle.Should().BeNull();
        nodeVm.Title.Should().Be(node.Name);

        // 3. Renombrar exactamente con el nombre por defecto -> CustomTitle debe ser null
        nodeVm.StartRenaming();
        nodeVm.EditingTitleText = node.Name;
        nodeVm.CommitTitleRename();

        nodeVm.CustomTitle.Should().BeNull();
        nodeVm.Title.Should().Be(node.Name);
    }

    [Fact]
    public void CancelTitleRename_ShouldDiscardChangesAndRestoreTitle()
    {
        var node = new ImageOptimizerNode();
        var nodeVm = new NodeViewModel(node, new Point(0, 0));

        nodeVm.StartRenaming();
        nodeVm.EditingTitleText = "Texto No Deseado";
        nodeVm.CancelTitleRename();

        nodeVm.IsEditingTitle.Should().BeFalse();
        nodeVm.CustomTitle.Should().BeNull();
        nodeVm.Title.Should().Be(node.Name);
        nodeVm.EditingTitleText.Should().Be(node.Name);
    }

    [Fact]
    public void WorkflowGraphSerializer_ShouldExportAndImportCustomTitleCorrectly()
    {
        var editor = new EditorViewModel(_loader);
        var node = new FolderSourceNode();
        var nodeVm = new NodeViewModel(node, new Point(100, 200))
        {
            ParentEditor = editor
        };
        nodeVm.StartRenaming();
        nodeVm.EditingTitleText = "Origen Fotos 2026";
        nodeVm.CommitTitleRename();

        editor.Nodes.Add(nodeVm);

        // Exportar a modelo de grafo
        var graph = editor.ExportToGraphModel("Test Graph");

        var exportedNode = graph.Nodes.FirstOrDefault(n => n.Id == nodeVm.Id);
        exportedNode.Should().NotBeNull();
        exportedNode!.CustomTitle.Should().Be("Origen Fotos 2026");

        // Importar en un nuevo editor
        var targetEditor = new EditorViewModel(_loader);
        targetEditor.LoadFromGraphModel(graph);

        targetEditor.Nodes.Should().HaveCount(1);
        var importedNode = targetEditor.Nodes[0];
        importedNode.CustomTitle.Should().Be("Origen Fotos 2026");
        importedNode.Title.Should().Be("Origen Fotos 2026");
    }

    [Fact]
    public void NodeClipboardService_ShouldPreserveCustomTitleAcrossCopyAndPaste()
    {
        var editor = new EditorViewModel(_loader);
        var clipboard = new NodeClipboardService(_loader);

        var node = new ImageOptimizerNode();
        var nodeVm = new NodeViewModel(node, new Point(50, 50))
        {
            ParentEditor = editor
        };
        nodeVm.StartRenaming();
        nodeVm.EditingTitleText = "Compresor Rápido";
        nodeVm.CommitTitleRename();

        editor.Nodes.Add(nodeVm);

        clipboard.Copy([nodeVm], editor.Connections);
        var pasted = clipboard.Paste(editor);

        pasted.Should().HaveCount(1);
        pasted[0].Id.Should().NotBe(nodeVm.Id);
        pasted[0].CustomTitle.Should().Be("Compresor Rápido");
        pasted[0].Title.Should().Be("Compresor Rápido");
    }

    [Fact]
    public async Task WorkflowExecutor_ShouldUseCustomTitleAsNodeNameInStructuredLogs()
    {
        var executor = new WorkflowExecutor();
        var graph = new WorkflowGraph { Name = "Custom Title Test" };

        var nodeDto = new WorkflowNode
        {
            Id = "test-node-1",
            NodeTypeName = typeof(FolderSourceNode).FullName!,
            CustomTitle = "Super Carpeta de Entrada"
        };
        nodeDto.Parameters["DirectoryPath"] = Path.GetTempPath();

        graph.Nodes.Add(nodeDto);

        var emittedRecords = new List<StructuredLogRecord>();
        executor.StructuredLogEmitted += r =>
        {
            if (r.NodeId == "test-node-1")
            {
                emittedRecords.Add(r);
            }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await executor.ExecuteAsync(graph, _loader, cts.Token);

        emittedRecords.Should().NotBeEmpty();
        emittedRecords.Should().OnlyContain(r => r.NodeName == "Super Carpeta de Entrada");
    }
}
