using System;
using System.Linq;
using Avalonia;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class LogConsoleViewModelTests
{
    [Fact]
    public void SelectedLog_ChangingProperty_ShouldRaiseLogSelectionChangedEvent()
    {
        // Arrange
        var logVm = new LogViewModel();
        StructuredLogRecord? receivedLog = null;
        logVm.LogSelectionChanged += log => receivedLog = log;

        var record = new StructuredLogRecord(
            1,
            "exec-001",
            DateTime.Now,
            LogLevel.Information,
            "Node-123",
            "FolderSourceNode",
            "Item-999",
            @"C:\Photos\photo.jpg",
            "photo.jpg",
            1024,
            12.5,
            "Test message",
            null);

        // Act
        logVm.SelectedLog = record;

        // Assert
        logVm.SelectedLog.Should().Be(record);
        receivedLog.Should().Be(record);
    }

    [Fact]
    public void FilterByNodeAndFile_ShouldUpdateSearchFilter()
    {
        // Arrange
        var logVm = new LogViewModel();

        // Act
        logVm.FilterByNode("FaceDetectorNode");

        // Assert
        logVm.SearchFilter.Should().Be("FaceDetectorNode");

        // Act
        logVm.FilterByFile("image_001.png");

        // Assert
        logVm.SearchFilter.Should().Be("image_001.png");
    }

    [Fact]
    public void InspectNodeById_ShouldSetInspectedNodeAndOpenInspector()
    {
        // Arrange
        var mockFileDialog = new Mock<IFileDialogService>();
        var editorVm = new EditorViewModel(new PluginLoader());
        var inspectorVm = new NodeInspectorViewModel(editorVm, mockFileDialog.Object);

        var node = new FolderSourceNode();
        var nodeVm = new NodeViewModel(node, new Point(100, 100));
        editorVm.Nodes.Add(nodeVm);

        // Act
        inspectorVm.InspectNodeById(nodeVm.Id);

        // Assert
        inspectorVm.IsOpen.Should().BeTrue();
        inspectorVm.InspectedNode.Should().Be(nodeVm);
    }

    [Fact]
    public void MainViewModel_SelectingLogWithNodeId_ShouldSyncWithNodeInspector()
    {
        // Arrange
        var mainVm = new MainViewModel();
        var folderNode = new FolderSourceNode();
        var nodeVm = new NodeViewModel(folderNode, new Point(50, 50));
        mainVm.Editor.Nodes.Add(nodeVm);

        var logRecord = new StructuredLogRecord(
            1,
            "exec-001",
            DateTime.Now,
            LogLevel.Information,
            nodeVm.Id,
            "FolderSourceNode",
            "Item-1",
            @"C:\Test\file.txt",
            "file.txt",
            2048,
            5.0,
            "Folder scanned successfully",
            null);

        // Act
        mainVm.LogConsole.SelectedLog = logRecord;

        // Assert
        mainVm.NodeInspector.InspectedNode.Should().Be(nodeVm);
        mainVm.NodeInspector.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void InspectLogRecord_WithDetailsJson_ShouldPopulateSelectedSnapshotAndMetadataDiffs()
    {
        // Arrange
        var mockFileDialog = new Mock<IFileDialogService>();
        var editorVm = new EditorViewModel(new PluginLoader());
        var inspectorVm = new NodeInspectorViewModel(editorVm, mockFileDialog.Object);

        var node = new FolderSourceNode();
        var nodeVm = new NodeViewModel(node, new Point(100, 100));
        editorVm.Nodes.Add(nodeVm);

        var logRecord = new StructuredLogRecord(
            1,
            "exec-001",
            DateTime.Now,
            LogLevel.Information,
            nodeVm.Id,
            "FolderSourceNode",
            "Item-999",
            @"C:\Photos\vacation.jpg",
            "vacation.jpg",
            4096,
            12.0,
            "Faces detected",
            "{\"AI:Category\":\"Landscapes\",\"AI:FaceCount\":3}");

        // Act
        inspectorVm.InspectLogRecord(logRecord);

        // Assert
        inspectorVm.IsOpen.Should().BeTrue();
        inspectorVm.InspectedNode.Should().Be(nodeVm);
        inspectorVm.SelectedSnapshot.Should().NotBeNull();
        inspectorVm.SelectedSnapshot!.ItemSnapshot.FileName.Should().Be("vacation.jpg");
        inspectorVm.MetadataDiffs.Should().Contain(d => d.Key == "AI:Category" && d.NewValue == "Landscapes");
        inspectorVm.MetadataDiffs.Should().Contain(d => d.Key == "AI:FaceCount" && d.NewValue == "3");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El latido: lo que hace que los registros aparezcan solos
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Todo el resto del suite vacía la consola a mano, así que el camino <b>diferido</b> —el que hace que los
    /// registros aparezcan por sí solos mientras la aplicación corre— no se ejercitaba en ninguna prueba. Es el
    /// latido de 40 ms: los productores encolan y él decide cuándo se pinta.
    /// </summary>
    [Fact]
    public void TheHeartbeat_ShouldBeWhatPutsLogsOnScreen_NotTheProducer()
    {
        using var logVm = new LogViewModel(new InMemoryLogStore());

        logVm.AddLog(LogLevel.Information, "uno");
        logVm.AddLog(LogLevel.Warning, "dos");

        logVm.Logs.Should().BeEmpty("el productor encola; quien pinta es el latido");

        logVm.FlushAllPendingLogs();

        logVm.Logs.Should().HaveCount(2, "el latido vuelca la cola en la lista visible");
        logVm.TotalLogsCount.Should().Be(2);
        logVm.InfoCount.Should().Be(1);
        logVm.WarningCount.Should().Be(1);
    }

    [Fact]
    public void TheHeartbeat_ShouldMoveABurstInOneBatch()
    {
        using var logVm = new LogViewModel(new InMemoryLogStore());
        int batches = 0;
        logVm.OnLogBatchAdded += () => batches++;

        for (int i = 0; i < 500; i++)
        {
            logVm.AddNodeLog(LogLevel.Debug, $"línea {i}", "nodo", "Nodo");
        }

        logVm.FlushAllPendingLogs();

        logVm.Logs.Should().HaveCount(500, "una ráfaga no puede perder líneas por el camino");
        logVm.DebugCount.Should().Be(500, "el latido cuenta la ráfaga una sola vez, no línea a línea");
        logVm.TotalLogsCount.Should().Be(500);
        batches.Should().Be(1, "un lote es una notificación, no una por registro");
    }

    [Fact]
    public void TheHeartbeat_ShouldCountWhatArrives_EvenWhenTheViewIsFiltered()
    {
        using var logVm = new LogViewModel(new InMemoryLogStore());
        logVm.SearchFilter = "algo-que-no-coincide";

        logVm.AddLog(LogLevel.Error, "fallo");

        logVm.FlushAllPendingLogs();

        logVm.Logs.Should().BeEmpty("con el buscador con texto, la fila no se pinta");
        logVm.ErrorCount.Should().Be(1, "pero el latido cuenta lo que llegó: los contadores no son del filtro");
        logVm.TotalLogsCount.Should().Be(1);
    }
}
