using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Subflows;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Al abrir un flujo, cada conexión se reconstruye emparejando su nombre de puerto. Una arista cuyo puerto ya
/// no existe —porque se renombró, porque la definición del subflujo cambió— no encuentra dónde conectarse, y
/// hasta ahora se descartaba sin decírselo a nadie: el flujo reabierto parecía completo y ya no lo estaba.
///
/// Un cable perdido es poco para el flujo y mucho para el usuario, así que estas pruebas fijan las dos
/// mitades: que se cuenta lo que no se pudo reconstruir —con los dos extremos y el motivo, que es lo que
/// permite volver a conectarlo— y que <b>no</b> se cuenta lo que sí se reconstruyó. Un aviso falso en cada
/// apertura es peor que el silencio: enseña a ignorar el canal.
/// </summary>
public class DroppedConnectionsReportTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // El informe
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LoadingACableToAPortThatNoLongerExists_ShouldReportItInsteadOfDroppingItSilently()
    {
        var graph = new WorkflowGraph { Name = "Con un cable desfasado", Schema = WorkflowFormat.CurrentSchema };
        graph.Nodes.Add(Source("src"));
        graph.Nodes.Add(DanglingContainer("container", "Contenedor"));
        graph.Edges.Add(Edge("src", "Out", "container", "In"));
        graph.Edges.Add(Edge("src", "Out", "container", "Fantasma"));

        var editor = new EditorViewModel(CreateLoader());

        var report = editor.LoadFromGraphModel(WorkflowFileFixtures.SavedFile(graph));

        report.IsComplete.Should().BeFalse("el flujo tenía dos cables y sólo uno se pudo reconstruir");

        var dropped = report.DroppedConnections.Should().ContainSingle().Subject;
        dropped.Target.NodeId.Should().Be("container");
        dropped.Target.NodeName.Should().Be("Contenedor", "es el nombre con el que el usuario reconocería el nodo");
        dropped.Target.PortName.Should().Be("Fantasma");
        dropped.Target.Problem.Should().Be(DroppedConnectionEndProblem.MissingPort);
        dropped.Source.Problem.Should().Be(DroppedConnectionEndProblem.None, "el extremo de origen conectaba bien");
        dropped.Impediments.Should().ContainSingle("sólo un extremo explica la pérdida");

        editor.Connections.Should().ContainSingle("el cable que sí encontraba sus puertos se reconstruye igual");
    }

    [Fact]
    public void LoadingACableToANodeThatCouldNotBeCreated_ShouldSayWhichNodeIsMissing()
    {
        var graph = new WorkflowGraph { Name = "Con un plugin que falta", Schema = WorkflowFormat.CurrentSchema };
        graph.Nodes.Add(Source("src"));
        graph.Nodes.Add(UncreatableNode("ghost", "PluginQueNoEsta.NodoDesconocido", customTitle: "Nodo de un plugin que falta"));
        graph.Nodes.Add(UncreatableNode("anonymous", "PluginQueNoEsta.NodoSinTitulo", customTitle: null));
        graph.Nodes.Add(Sink("sink"));
        graph.Edges.Add(Edge("src", "Out", "ghost", "In"));
        graph.Edges.Add(Edge("ghost", "Out", "sink", "In"));
        graph.Edges.Add(Edge("src", "Out", "anonymous", "In"));

        var editor = new EditorViewModel(CreateLoader());

        var report = editor.LoadFromGraphModel(WorkflowFileFixtures.SavedFile(graph));

        editor.Nodes.Should().HaveCount(2, "los nodos cuyo tipo no está registrado no llegan al lienzo");

        var incoming = report.DroppedConnections.Single(connection => connection.Target.NodeId == "ghost");
        incoming.Target.Problem.Should().Be(DroppedConnectionEndProblem.MissingNode);
        incoming.Target.NodeName.Should().Be("Nodo de un plugin que falta",
            "el título que el usuario le puso es como lo reconoce");
        incoming.Source.Problem.Should().Be(DroppedConnectionEndProblem.None);

        var outgoing = report.DroppedConnections.Single(connection => connection.Source.NodeId == "ghost");
        outgoing.Source.Problem.Should().Be(DroppedConnectionEndProblem.MissingNode,
            "un nodo que no existe tampoco puede ser el origen de nada");

        // Sin título con el que reconocerlo, queda el tipo: es lo que se busca para saber qué plugin falta.
        var withoutTitle = report.DroppedConnections.Single(connection => connection.Target.NodeId == "anonymous");
        withoutTitle.Target.NodeName.Should().Be("PluginQueNoEsta.NodoSinTitulo");
    }

    [Fact]
    public void LoadingAHealthyWorkflow_ShouldReportNothing()
    {
        var graph = new WorkflowGraph { Name = "Entero", Schema = WorkflowFormat.CurrentSchema };
        graph.Nodes.Add(Source("src"));
        graph.Nodes.Add(Sink("sink"));
        graph.Edges.Add(Edge("src", "Out", "sink", "In"));

        var editor = new EditorViewModel(CreateLoader());

        var report = editor.LoadFromGraphModel(WorkflowFileFixtures.SavedFile(graph));

        report.IsComplete.Should().BeTrue();
        report.DroppedConnections.Should().BeEmpty();
        editor.Connections.Should().ContainSingle();
    }

    [Fact]
    public void LoadingAWorkflowOlderThanTheFormat_ShouldNotReportThePortsItsMigrationRecovers()
    {
        // El archivo es anterior al formato versionado, así que no guardaba los puertos del contenedor; los
        // recupera de sus propias aristas antes de emparejarlas. Avisar de esos cables sería avisar de un
        // problema que ya no existe.
        var graph = new WorkflowGraph { Name = "Antiguo" };
        graph.Nodes.Add(Source("src"));
        graph.Nodes.Add(DanglingContainer("container", "Contenedor"));
        graph.Nodes.Add(Sink("sink"));
        graph.Edges.Add(Edge("src", "Out", "container", "Alternate"));
        graph.Edges.Add(Edge("container", "Errores", "sink", "In"));

        var editor = new EditorViewModel(CreateLoader());

        var report = editor.LoadFromGraphModel(WorkflowFileFixtures.FileFromOlderFormat(graph));

        report.IsComplete.Should().BeTrue();
        editor.Connections.Should().HaveCount(2, "la migración del formato recuperó los puertos que el archivo no guardaba");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El aviso al usuario: abrir un archivo es el camino por el que se entera
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OpeningAFileWithACableThatCannotBeRebuilt_ShouldWarnNamingBothEndsAndTheReason()
    {
        var graph = new WorkflowGraph { Name = "Con un cable desfasado", Schema = WorkflowFormat.CurrentSchema };
        graph.Nodes.Add(Source("src"));
        graph.Nodes.Add(DanglingContainer("container", "Contenedor"));
        graph.Edges.Add(Edge("src", "Out", "container", "Fantasma"));

        var log = await OpenSavedFile(graph);

        var warning = log.Logs.Should().ContainSingle(record => record.Level == LogLevel.Warning).Subject;
        warning.Message.Should().Contain("Contenedor").And.Contain("Fantasma");
        warning.Message.Should().Contain("Fantasma", "el puerto que falta es lo que hay que volver a conectar");
        log.WarningCount.Should().Be(1);
    }

    [Fact]
    public async Task OpeningAHealthyFile_ShouldNotWarnAboutAnything()
    {
        var graph = new WorkflowGraph { Name = "Entero", Schema = WorkflowFormat.CurrentSchema };
        graph.Nodes.Add(Source("src"));
        graph.Nodes.Add(DanglingContainer("container", "Contenedor"));
        graph.Edges.Add(Edge("src", "Out", "container", "In"));

        var log = await OpenSavedFile(graph);

        log.Logs.Should().Contain(record => record.Level == LogLevel.Information,
            "el flujo se abrió de verdad: si no, no habría nada de lo que no avisar");
        log.Logs.Should().NotContain(record => record.Level == LogLevel.Warning,
            "un aviso falso en cada apertura enseña a ignorar el canal de avisos");
        log.WarningCount.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Guarda el flujo en un archivo temporal, lo abre por donde lo abre el usuario —el comando que pasa por
    /// el diálogo de archivos— y devuelve el registro, ya vaciado de la cola de la consola.
    /// </summary>
    private static async Task<LogViewModel> OpenSavedFile(WorkflowGraph graph)
    {
        string filePath = SubflowFixtures.TempFile();
        try
        {
            await new WorkflowStorageService().SaveWorkflowAsync(filePath, graph);

            var loader = CreateLoader();
            var editor = new EditorViewModel(loader);
            var log = new LogViewModel(new InMemoryLogStore());

            var fileDialog = new Mock<IFileDialogService>();
            fileDialog
                .Setup(dialog => dialog.ShowOpenFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(filePath);

            var controlBar = new ControlBarViewModel(
                editor,
                loader,
                log,
                new NodeInspectorViewModel(editor, fileDialog.Object, log),
                fileDialog.Object,
                new WorkflowStorageService());

            await controlBar.LoadWorkflowAsync();
            log.FlushAllPendingLogs();

            return log;
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static WorkflowNode Source(string id) => new()
    {
        Id = id,
        NodeTypeName = typeof(FolderSourceNode).FullName!
    };

    private static WorkflowNode Sink(string id) => new()
    {
        Id = id,
        NodeTypeName = typeof(DestinationSinkNode).FullName!
    };

    /// <summary>
    /// Contenedor de subflujo cuya definición no resuelve: expone los puertos genéricos, y sólo los que su
    /// memoria diga. Es el nodo con puertos que un archivo guardado puede nombrar y ya no existir.
    /// </summary>
    private static WorkflowNode DanglingContainer(string id, string title) => new()
    {
        Id = id,
        NodeTypeName = typeof(SubflowNode).FullName!,
        CustomTitle = title,
        Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["SubflowPath"] = SubflowFixtures.TempFile(),
            ["SubflowName"] = title,
            ["RememberedInputPorts"] = "In",
            ["RememberedOutputPorts"] = "Out"
        }
    };

    /// <summary>Nodo cuyo tipo no está registrado en el cargador: no se puede crear.</summary>
    private static WorkflowNode UncreatableNode(string id, string nodeTypeName, string? customTitle) => new()
    {
        Id = id,
        NodeTypeName = nodeTypeName,
        CustomTitle = customTitle
    };

    private static WorkflowEdge Edge(string sourceId, string sourcePort, string targetId, string targetPort) => new()
    {
        SourceNodeId = sourceId,
        SourcePortName = sourcePort,
        TargetNodeId = targetId,
        TargetPortName = targetPort
    };

    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);
        return loader;
    }
}
