using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Logic;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class WorkflowCheckpointTests : IDisposable
{
    private const string WorkflowName = "Checkpoint Batching Test";

    private readonly string _testBaseDir;
    private readonly string _tempFilesDir;

    public WorkflowCheckpointTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "FileFlow_CP_Mgr_" + Guid.NewGuid().ToString("N"));
        _tempFilesDir = Path.Combine(Path.GetTempPath(), "FileFlow_CP_Files_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testBaseDir);
        Directory.CreateDirectory(_tempFilesDir);
    }

    [Fact]
    public void CheckpointManager_SaveRetrieveAndClear_OperatesCorrectly()
    {
        // Arrange
        var manager = new WorkflowCheckpointManager(_testBaseDir);
        string wfName = "Image Batch Pipeline";

        var cp = new WorkflowCheckpointData
        {
            WorkflowName = wfName,
            ProcessedItemsCount = 10,
            CompletedFileKeys = ["C:\\Data\\file1.png", "C:\\Data\\file2.png"]
        };

        // Act & Assert 1: Guardar y verificar existencia
        manager.SaveCheckpoint(cp);
        bool hasPending = manager.HasPendingCheckpoint(wfName, out var retrieved);

        hasPending.Should().BeTrue();
        retrieved.Should().NotBeNull();
        retrieved!.CompletedFileKeys.Should().HaveCount(2);
        retrieved.CompletedFileKeys.Should().Contain("C:\\Data\\file1.png");

        // Act & Assert 2: Limpiar checkpoint
        manager.ClearCheckpoint(wfName);
        manager.HasPendingCheckpoint(wfName, out var emptyCheck).Should().BeFalse();
    }

    [Fact]
    public void CheckpointManager_ClearAllCheckpoints_RemovesAllStoredCheckpoints()
    {
        // Arrange
        var manager = new WorkflowCheckpointManager(_testBaseDir);
        manager.SaveCheckpoint(new WorkflowCheckpointData
        {
            WorkflowName = "Flow 1",
            ProcessedItemsCount = 5,
            CompletedFileKeys = ["C:\\test1.txt"]
        });
        manager.SaveCheckpoint(new WorkflowCheckpointData
        {
            WorkflowName = "Flow 2",
            ProcessedItemsCount = 3,
            CompletedFileKeys = ["C:\\test2.txt"]
        });

        manager.HasPendingCheckpoint("Flow 1", out _).Should().BeTrue();
        manager.HasPendingCheckpoint("Flow 2", out _).Should().BeTrue();

        // Act
        manager.ClearAllCheckpoints();

        // Assert
        manager.HasPendingCheckpoint("Flow 1", out _).Should().BeFalse();
        manager.HasPendingCheckpoint("Flow 2", out _).Should().BeFalse();
    }

    [Fact]
    public async Task WorkflowExecutor_WithExistingCheckpoint_SkipsCompletedItems()
    {
        // Arrange
        string file1 = Path.Combine(_tempFilesDir, "done1.txt");
        string file2 = Path.Combine(_tempFilesDir, "new2.txt");
        await File.WriteAllTextAsync(file1, "Old Completed Content");
        await File.WriteAllTextAsync(file2, "New Pending Content");

        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(ThrottleDelayNode).Assembly);

        var graph = new WorkflowGraph { Name = "Resumption Test Workflow" };
        var src = new WorkflowNode
        {
            Id = "src-1",
            NodeTypeName = typeof(FolderSourceNode).FullName!,
            Parameters = new Dictionary<string, object?> { ["SourcePath"] = _tempFilesDir }
        };
        var throttle = new WorkflowNode
        {
            Id = "th-1",
            NodeTypeName = typeof(ThrottleDelayNode).FullName!,
            Parameters = new Dictionary<string, object?> { ["DelayMilliseconds"] = 1 }
        };
        graph.Nodes.Add(src);
        graph.Nodes.Add(throttle);
        graph.Edges.Add(new WorkflowEdge
        {
            SourceNodeId = "src-1",
            SourcePortName = "Out",
            TargetNodeId = "th-1",
            TargetPortName = "In"
        });

        // Simular un checkpoint existente donde file1 ya fue completado
        var existingCp = new WorkflowCheckpointData
        {
            WorkflowName = graph.Name,
            CompletedFileKeys = [file1],
            ProcessedItemsCount = 1
        };

        var executor = new WorkflowExecutor
        {
            IsDryRun = false,
            EnableCheckpointing = true,
            Checkpoint = existingCp,
            // Un gestor en directorio temporal: el de por omisión escribe en el perfil real del usuario.
            CheckpointManager = new WorkflowCheckpointManager(_testBaseDir)
        };

        // Act
        await executor.ExecuteAsync(graph, loader, CancellationToken.None);

        // Assert: el archivo ya completado en la ejecución anterior se salta y el nuevo se procesa. Y al
        // terminar bien no queda nada en memoria: el punto de control se limpió con la ejecución.
        var nodeStats = executor.GetNodeTelemetryStats();
        nodeStats.Should().ContainKey("th-1");
        nodeStats["th-1"].ProcessedCount.Should().Be(1,
            "sólo el archivo nuevo tenía que llegar al nodo: el otro ya estaba completado en la ejecución anterior");
        executor.Checkpoint.Should().BeNull(
            "una ejecución que terminó bien no deja punto de control: no hay nada que reanudar");
    }

    [Fact]
    public async Task WorkflowExecutor_BranchingWorkflow_DoesNotTriggerSpuriousCheckpointSkipDuringExecution()
    {
        // Arrange
        string file1 = Path.Combine(_tempFilesDir, "branch_item.txt");
        await File.WriteAllTextAsync(file1, "Branching item content");

        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(ThrottleDelayNode).Assembly);

        var graph = new WorkflowGraph { Name = "Branching Checkpoint Test" };
        var src = new WorkflowNode
        {
            Id = "src-1",
            NodeTypeName = typeof(FolderSourceNode).FullName!,
            Parameters = new Dictionary<string, object?> { ["SourcePath"] = _tempFilesDir }
        };
        var branchA = new WorkflowNode
        {
            Id = "branch-a",
            NodeTypeName = typeof(ThrottleDelayNode).FullName!,
            Parameters = new Dictionary<string, object?> { ["DelayMilliseconds"] = 1 }
        };
        var branchB = new WorkflowNode
        {
            Id = "branch-b",
            NodeTypeName = typeof(ThrottleDelayNode).FullName!,
            Parameters = new Dictionary<string, object?> { ["DelayMilliseconds"] = 10 }
        };
        graph.Nodes.Add(src);
        graph.Nodes.Add(branchA);
        graph.Nodes.Add(branchB);

        // src -> branchA y src -> branchB (2 ramas paralelas)
        graph.Edges.Add(new WorkflowEdge { SourceNodeId = "src-1", SourcePortName = "Out", TargetNodeId = "branch-a", TargetPortName = "In" });
        graph.Edges.Add(new WorkflowEdge { SourceNodeId = "src-1", SourcePortName = "Out", TargetNodeId = "branch-b", TargetPortName = "In" });

        var executor = new WorkflowExecutor
        {
            IsDryRun = false,
            EnableCheckpointing = true,
            CheckpointManager = new WorkflowCheckpointManager(_testBaseDir)
        };

        var logMessages = new List<string>();
        executor.LogEmitted += (msg, level) => logMessages.Add(msg);

        // Act
        await executor.ExecuteAsync(graph, loader, CancellationToken.None);

        // Assert: Durante la ejecución limpia, NINGUNA rama debe emitir "Omitiendo archivo completado previamente"
        logMessages.Should().NotContain(msg => msg.Contains("Omitiendo archivo completado previamente"));
    }

    [Fact]
    public void CheckpointHandler_PersistsInBatches_NotOncePerCompletedFile()
    {
        // El defecto: cada archivo completado persistía el conjunto entero de claves. Con 1.000 archivos eran
        // 1.000 escrituras de un conjunto que crecía hasta 1.000 claves (y una serialización por ítem).
        var manager = new WorkflowCheckpointManager(_testBaseDir);
        var handler = new WorkflowCheckpointHandler { Manager = manager, FilesPerCheckpointWrite = 100 };
        handler.InitializeCheckpoint(WorkflowName, "exec-1", isDryRun: false, (_, _) => { });

        for (int i = 0; i < 1_000; i++)
        {
            handler.RecordCompletedFile($@"C:\Data\f{i:D4}.png", i + 1);
        }

        manager.SaveCount.Should().Be(10, "con lotes de 100 archivos, 1.000 archivos son 10 volcados del conjunto");

        // El estado en disco es un conjunto completo: el lote no deja huecos, sólo escribe menos veces.
        manager.HasPendingCheckpoint(WorkflowName, out var saved).Should().BeTrue();
        saved!.CompletedFileKeys.Should().HaveCount(1_000);
        saved.ProcessedItemsCount.Should().Be(1_000);
    }

    [Fact]
    public void CheckpointHandler_WithOnePerWrite_PersistsOncePerCompletedFile()
    {
        // El «antes» conservado a propósito: es el comportamiento que el lote sustituye y contra el que se mide.
        const int fileCount = 200;
        var manager = new WorkflowCheckpointManager(_testBaseDir);
        var handler = new WorkflowCheckpointHandler { Manager = manager, FilesPerCheckpointWrite = 1 };
        handler.InitializeCheckpoint(WorkflowName, "exec-1", isDryRun: false, (_, _) => { });

        for (int i = 0; i < fileCount; i++)
        {
            handler.RecordCompletedFile($@"C:\Data\one{i:D4}.png", i + 1);
        }

        manager.SaveCount.Should().Be(fileCount, "con lote de 1, cada archivo reescribe el conjunto entero");
    }

    [Fact]
    public void CheckpointHandler_FlushPendingSaves_PersistsWhatIsLeft()
    {
        var manager = new WorkflowCheckpointManager(_testBaseDir);
        var handler = new WorkflowCheckpointHandler { Manager = manager, FilesPerCheckpointWrite = 100 };
        handler.InitializeCheckpoint(WorkflowName, "exec-1", isDryRun: false, (_, _) => { });

        for (int i = 0; i < 250; i++)
        {
            handler.RecordCompletedFile($@"C:\Data\left{i:D4}.png", i + 1);
        }

        manager.SaveCount.Should().Be(2, "dos lotes completos (100 y 200)");

        // Lo que el motor hace al cerrar una ejecución interrumpida: sacar lo que quedaba pendiente.
        handler.FlushPendingSaves();

        manager.SaveCount.Should().Be(3);
        manager.HasPendingCheckpoint(WorkflowName, out var saved).Should().BeTrue();
        saved!.CompletedFileKeys.Should().HaveCount(250);
    }

    [Fact]
    public void CheckpointHandler_ClearCheckpoint_ForgetsWhatWasPending()
    {
        // Sin esto, el volcado de cierre resucitaría el fichero que el final de la ejecución acaba de borrar.
        var manager = new WorkflowCheckpointManager(_testBaseDir);
        var handler = new WorkflowCheckpointHandler { Manager = manager, FilesPerCheckpointWrite = 100 };
        handler.InitializeCheckpoint(WorkflowName, "exec-1", isDryRun: false, (_, _) => { });

        for (int i = 0; i < 40; i++)
        {
            handler.RecordCompletedFile($@"C:\Data\clean{i:D4}.png", i + 1);
        }

        handler.ClearCheckpoint(WorkflowName, isDryRun: false);
        handler.FlushPendingSaves();

        manager.SaveCount.Should().Be(0, "el cierre no tiene que volver a escribir lo que se acaba de borrar");
        manager.HasPendingCheckpoint(WorkflowName, out _).Should().BeFalse();
    }

    [Fact]
    public void CheckpointHandler_ClearCheckpoint_ForgetsTheInMemoryState()
    {
        var manager = new WorkflowCheckpointManager(_testBaseDir);
        var handler = new WorkflowCheckpointHandler { Manager = manager };
        handler.InitializeCheckpoint(WorkflowName, "exec-1", isDryRun: false, (_, _) => { });
        handler.RecordCompletedFile(@"C:\Data\state.png", 1);

        handler.Checkpoint.Should().NotBeNull();

        handler.ClearCheckpoint(WorkflowName, isDryRun: false);

        handler.Checkpoint.Should().BeNull(
            "el estado de una ejecución terminada es estado muerto: dejarlo es lo que hacía que la siguiente «se creyera todo hecho»");
        manager.HasPendingCheckpoint(WorkflowName, out _).Should().BeFalse();
    }

    [Fact]
    public async Task WorkflowExecutor_ReusedForASecondRun_ShouldProcessEveryFileAgain()
    {
        // El defecto reportado: limpiar el punto de control borraba el fichero pero no el objeto en memoria, así
        // que la segunda ejecución del mismo motor daba cada archivo por completado, no entregaba nada y
        // terminaba en verde en milisegundos.
        string sourceDir = Path.Combine(_tempFilesDir, "entrada");
        string destination = Path.Combine(_tempFilesDir, "salida");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destination);
        for (int i = 0; i < 3; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(sourceDir, $"repetido{i}.txt"), "contenido");
        }

        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);

        var graph = new WorkflowGraph { Name = "Reused Executor Test" };
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "src-1",
            NodeTypeName = typeof(FolderSourceNode).FullName!,
            Parameters = new Dictionary<string, object?> { ["SourcePath"] = sourceDir }
        });
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "sink-1",
            NodeTypeName = "DestinationSinkNode",
            Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = destination }
        });
        graph.Edges.Add(new WorkflowEdge
        {
            SourceNodeId = "src-1",
            SourcePortName = "Out",
            TargetNodeId = "sink-1",
            TargetPortName = "In"
        });

        var executor = new WorkflowExecutor
        {
            EnableCheckpointing = true,
            CheckpointManager = new WorkflowCheckpointManager(_testBaseDir)
        };

        // Primera ejecución: los tres archivos se procesan y el punto de control se limpia al terminar.
        await executor.ExecuteAsync(graph, loader, CancellationToken.None);
        Directory.EnumerateFiles(destination).Should().HaveCount(3, "la primera ejecución tiene que entregar los tres archivos");

        foreach (string stale in Directory.EnumerateFiles(destination)) File.Delete(stale);

        // Segunda ejecución con EL MISMO motor: es lo que hace la interfaz al volver a pulsar Ejecutar.
        await executor.ExecuteAsync(graph, loader, CancellationToken.None);

        Directory.EnumerateFiles(destination).Should().HaveCount(3,
            "una ejecución nueva vuelve a hacer el trabajo: nada de la anterior está hecho todavía");
        executor.GetNodeTelemetryStats()["sink-1"].ProcessedCount.Should().Be(3,
            "los tres archivos tienen que volver a pasar por el nodo en la segunda ejecución");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir)) Directory.Delete(_testBaseDir, true);
            if (Directory.Exists(_tempFilesDir)) Directory.Delete(_tempFilesDir, true);
        }
        catch { }
    }
}
