using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Subflows;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Telemetry;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Las dos superficies que quedaban a medias en el informe de un cable perdido: el <b>registro</b> de la consola
/// y la <b>barra de estado</b>.
///
/// La consola ya contaba qué se perdió y por qué, pero no decía <b>de qué nodo</b>: la fila se leía y no llevaba
/// a ninguna parte, cuando el inspector sabe abrir un nodo a partir de un registro (ver
/// <see cref="NodeInspectorViewModel.InspectLogRecord"/>). Y el aviso del lienzo cuenta lo perdido donde está la
/// acción, que es su sitio, pero el lienzo se puede estar mirando desde otro sitio: la barra de estado —junto a
/// los nodos y las conexiones que ya cuenta— deja constancia de que el grafo que se está viendo tiene cables de
/// menos.
///
/// Lo que se fija aquí, además de las dos cosas funcionando, son sus <b>límites</b>: un nodo que no está en el
/// lienzo no se señala —ese identificador no lleva a ninguna parte, y su nombre podría coincidir con el de otro
/// nodo—, y una pérdida que el lienzo no puede arreglar no desaparece del recuento al arreglar las que sí.
/// </summary>
public class LostConnectionTracesTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // El registro: qué se perdió, y de qué nodo
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pegar es donde el identificador no se puede dar por sabido: los nodos pegados son <b>otros</b>, así que un
    /// registro que llevara el identificador del origen apuntaría a su gemelo —el que se copió— y no al que
    /// acaba de perder el cable.
    /// </summary>
    [Fact]
    public void PastingWithALostCable_ShouldLogTheNodeTheCableWasLostFrom()
    {
        var (editor, log, original, pasted) = PasteAfterTheFrontierChanged();

        var record = log.Logs.Single(logRecord => logRecord.Level == LogLevel.Warning);

        record.NodeId.Should().Be(pasted.Id, "el cable se perdió en el nodo pegado, no en el que se copió");
        record.NodeId.Should().NotBe(original.Id, "el original sigue con su cable: señalarle sería mentir");
        record.NodeName.Should().Be(pasted.Title, "y el nombre de la fila es con el que el usuario reconoce el nodo");
        record.FormattedLine.Should().Contain(pasted.Title, "el nombre del nodo se lee en la propia línea");

        editor.CanvasNoticeFixes.Should().ContainSingle(fix => fix.NodeTitle == pasted.Title,
            "la fila del aviso y la línea de la consola hablan del mismo nodo");
    }

    /// <summary>
    /// De nada sirve llevar el identificador si no lleva a ninguna parte: lo que compra es que seleccionar la
    /// fila abra <b>ese</b> nodo en el inspector, que es donde están sus salidas y sus metadatos.
    /// </summary>
    [Fact]
    public void TheRecordOfALostCable_ShouldOpenThatNodeInTheInspector()
    {
        var (editor, log, _, pasted) = PasteAfterTheFrontierChanged();

        var inspector = new NodeInspectorViewModel(editor, new NullFileDialogService(), log);
        inspector.InspectLogRecord(log.Logs.Single(logRecord => logRecord.Level == LogLevel.Warning));

        inspector.InspectedNode.Should().BeSameAs(pasted, "la fila de la consola abre el nodo del cable que se perdió");
        inspector.IsOpen.Should().BeTrue("abrir el nodo es enseñarlo");
    }

    /// <summary>
    /// Un nodo que no se pudo crear no está en el lienzo, y señalarlo sería mandar al inspector a un nodo que no
    /// es: el nombre podría coincidir con el de otro nodo del grafo. Ese registro va sin nodo, y el nombre del que
    /// falta sigue donde estaba, en el motivo —que es la frase que dice qué plugin hay que instalar—.
    /// </summary>
    [Fact]
    public void ALossWhoseNodeIsNotThere_ShouldBeLoggedWithoutANodeToPointAt()
    {
        const string missingType = "FileFlow.Plugin.Que.No.Existe.Nodo";

        var graph = new WorkflowGraph { Name = "Nodo que no está" };
        graph.Nodes.Add(Source("origen"));
        graph.Nodes.Add(new WorkflowNode { Id = "fantasma", NodeTypeName = missingType, CustomTitle = "Fantasma" });
        graph.Edges.Add(Edge("origen", "Out", "fantasma", "In"));

        var log = new LogViewModel(new InMemoryLogStore());
        var editor = new EditorViewModel(CreateLoader(), logViewModel: log);

        editor.LoadFromGraphModel(graph);
        log.FlushAllPendingLogs();

        var record = log.Logs.Single(logRecord => logRecord.Level == LogLevel.Warning);
        record.NodeId.Should().BeNull("ese identificador no está en el lienzo: llevar a un nodo que no está es no llevar a ninguno");
        record.NodeName.Should().BeNull("y sin nombre no hay forma de abrir por coincidencia el nodo que no es");
        record.Message.Should().Contain("Fantasma", "el nodo que falta se sigue nombrando donde estaba, en el motivo");

        // La coincidencia es posible: otro nodo del lienzo puede llamarse como el que falta. Y no se abre,
        // porque el registro no lo nombra.
        var lookalike = EditorFixtures.AddNode(editor, new SubflowNode());
        lookalike.Title = "Fantasma";

        var inspector = new NodeInspectorViewModel(editor, new NullFileDialogService(), log);
        inspector.InspectLogRecord(record);

        inspector.InspectedNode.Should().BeNull("un registro sin nodo no abre nada, ni siquiera al homónimo");
    }

    /// <summary>
    /// Un cable puede fallar por sus dos extremos, y entonces son <b>dos</b> nodos los que hay que arreglar. Una
    /// sola línea con los dos motivos no puede llevar los dos identificadores, así que se cuenta una vez por
    /// nodo: cada línea lleva al suyo.
    /// </summary>
    [Fact]
    public void ACableThatFailsByBothEnds_ShouldBeLoggedOncePerNode()
    {
        var graph = new WorkflowGraph { Name = "Dos extremos", Schema = WorkflowFormat.CurrentSchema };
        graph.Nodes.Add(DanglingContainer("a", "A"));
        graph.Nodes.Add(DanglingContainer("b", "B"));

        // Los dos puertos que el archivo nombra existen en un contenedor que no resuelve su definición: ninguno
        // de los dos extremos empareja, que es el cable que hay que volver a conectar por sus dos lados.
        graph.Edges.Add(Edge("a", "Salida", "b", "Entrada"));

        var log = new LogViewModel(new InMemoryLogStore());
        var editor = new EditorViewModel(CreateLoader(), logViewModel: log);

        editor.LoadFromGraphModel(graph);
        log.FlushAllPendingLogs();

        var warnings = log.Logs.Where(record => record.Level == LogLevel.Warning).ToList();
        warnings.Should().HaveCount(2, "un cable que falla por sus dos extremos son dos nodos que arreglar");
        warnings.Select(record => record.NodeId!).Should().BeEquivalentTo(["a", "b"],
            "cada línea lleva al nodo que tiene que arreglarla");
        warnings.Should().OnlyContain(record => record.Message.Contains(record.NodeName!),
            "y el nombre del nodo de la línea es el del motivo de esa misma línea");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La barra de estado: cuánto le falta al grafo
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El resumen se lee de lo que el lienzo ya sabe: quién pierde cables es el aviso, y la barra no lleva su
    /// propia cuenta de nada.
    /// </summary>
    [Fact]
    public void OneLostCable_ShouldBeSummedUpOnTheStatusBar()
    {
        var (editor, log, _, pasted) = PasteAfterTheFrontierChanged();

        var statusBar = CreateStatusBar(editor, log);

        statusBar.HasUnrebuiltConnections.Should().BeTrue("el grafo que se está viendo tiene un cable de menos");
        statusBar.UnrebuiltConnectionsCount.Should().Be(1);
        statusBar.UnrebuiltConnectionsText.Should().Be(
            LocalizationManager.Instance.GetString("StatusBar_UnrebuiltConnection", "🔌 1 conexión perdida"),
            "un solo cable se cuenta en singular");
        statusBar.UnrebuiltConnectionsToolTip.Should().NotBeNullOrWhiteSpace("y la píldora dice dónde está el arreglo");

        editor.CanvasNoticeFixes.Should().Contain(fix => fix.NodeTitle == pasted.Title,
            "el resumen es del mismo hecho que el aviso, no de otro");
    }

    /// <summary>
    /// El recuento es de lo que <b>sigue</b> perdido: baja al reconectar un cable y desaparece con el último, que
    /// es cuando el grafo vuelve a estar entero. Es el mismo dato que el aviso del lienzo, que se retira con la
    /// última fila.
    /// </summary>
    [Fact]
    public void TheSummary_ShouldCountWhatIsStillLostAndGoOffWithTheLastFix()
    {
        var (editor, log) = PasteWithTwoLostCables();

        var statusBar = CreateStatusBar(editor, log);

        editor.CanvasNoticeFixes.Should().HaveCount(2, "los dos cables al contenedor apuntaban a puertos que ya no están");
        statusBar.UnrebuiltConnectionsCount.Should().Be(2);
        statusBar.UnrebuiltConnectionsText.Should().Be(
            LocalizationManager.Instance.GetFormattedString("StatusBar_UnrebuiltConnections", "🔌 {0} conexiones perdidas", 2),
            "dos cables se cuentan en plural");

        editor.CanvasNoticeFixes[0].ReconnectCommand.Execute(null);

        statusBar.UnrebuiltConnectionsCount.Should().Be(1, "el cable que volvió ya no está perdido");

        editor.CanvasNoticeFixes[0].ReconnectCommand.Execute(null);

        statusBar.UnrebuiltConnectionsCount.Should().Be(0);
        statusBar.HasUnrebuiltConnections.Should().BeFalse("sin cables perdidos no hay nada que resumir");
        editor.HasCanvasNotice.Should().BeFalse("y el aviso del lienzo se retira con la última fila");
        editor.Connections.Should().HaveCount(4, "los dos originales, que nunca se perdieron, y los dos que volvieron");
    }

    /// <summary>
    /// El caso que el lienzo no puede arreglar: el nodo no está, así que no hay a dónde ir ni qué reconectar. La
    /// barra lo cuenta igual, porque lo que cuenta no es lo que se puede pulsar sino lo que le falta al grafo —y
    /// es justo el cable que se quedaría fuera si el resumen saliera de las filas.
    /// </summary>
    [Fact]
    public void ALossTheCanvasCannotFix_ShouldStillBeSummedUp()
    {
        var graph = new WorkflowGraph { Name = "Nodo que no está" };
        graph.Nodes.Add(Source("origen"));
        graph.Nodes.Add(new WorkflowNode { Id = "fantasma", NodeTypeName = "FileFlow.Plugin.Que.No.Existe.Nodo", CustomTitle = "Fantasma" });
        graph.Edges.Add(Edge("origen", "Out", "fantasma", "In"));

        var log = new LogViewModel(new InMemoryLogStore());
        var editor = new EditorViewModel(CreateLoader(), logViewModel: log);

        // La barra se monta antes de que la pérdida ocurra: es lo que comprueba que el resumen sigue al editor en
        // vez de leerlo una sola vez al arrancar.
        var statusBar = CreateStatusBar(editor, log);
        statusBar.HasUnrebuiltConnections.Should().BeFalse("todavía no se ha abierto nada");

        editor.LoadFromGraphModel(graph);

        editor.CanvasNoticeFixes.Should().BeEmpty("a un nodo que no está no se puede llevar al usuario");
        statusBar.UnrebuiltConnectionsCount.Should().Be(1, "y sin embargo el grafo tiene un cable de menos");
        statusBar.HasUnrebuiltConnections.Should().BeTrue();
    }

    /// <summary>
    /// Lo que el lienzo puede arreglar y lo que no pueden acabar en el mismo aviso, y entonces el último arreglo
    /// <b>no</b> puede llevarse el aviso por delante: quedaría sin contarse justo el cable que no se puede
    /// recuperar, que es el que más falta hace saber.
    /// </summary>
    [Fact]
    public void FixingTheLastRepairableCable_ShouldNotRetireTheNoticeOfTheOneThatIsNot()
    {
        var graph = new WorkflowGraph { Name = "Dos pérdidas, una sin arreglo", Schema = WorkflowFormat.CurrentSchema };
        graph.Nodes.Add(Source("origen"));
        graph.Nodes.Add(DanglingContainer("contenedor", "Contenedor", inputs: "In;Alternate"));
        graph.Nodes.Add(new WorkflowNode { Id = "fantasma", NodeTypeName = "FileFlow.Plugin.Que.No.Existe.Nodo", CustomTitle = "Fantasma" });

        // Un cable a un puerto que ya no existe —reparable proponiendo el vigente, que se le parece— y otro a un
        // nodo que no está, del que no hay nada que proponer.
        graph.Edges.Add(Edge("origen", "Out", "contenedor", "Alternates"));
        graph.Edges.Add(Edge("origen", "Out", "fantasma", "In"));

        var log = new LogViewModel(new InMemoryLogStore());
        var editor = new EditorViewModel(CreateLoader(), logViewModel: log);

        editor.LoadFromGraphModel(graph);

        var statusBar = CreateStatusBar(editor, log);
        statusBar.UnrebuiltConnectionsCount.Should().Be(2, "el grafo perdió dos cables, uno arreglable y otro no");

        editor.CanvasNoticeFixes.Should().ContainSingle("sólo el cable del puerto que falta se puede arreglar aquí");
        editor.CanvasNoticeFixes[0].ReconnectCommand.Execute(null);

        editor.Connections.Should().ContainSingle("el cable reparable volvió");
        statusBar.UnrebuiltConnectionsCount.Should().Be(1, "y el que no tiene arreglo sigue perdido, y sigue contado");
        editor.HasCanvasNotice.Should().BeTrue("retirar el aviso escondería el cable que no se puede recuperar");
    }

    /// <summary>
    /// Un aviso cuenta la última acción: deshacerla lo retira, y con él su resumen, que si no quedaría contando
    /// una pérdida que el usuario ya revirtió.
    /// </summary>
    [Fact]
    public void UndoingThePaste_ShouldClearTheSummary()
    {
        var (editor, log, _, _) = PasteAfterTheFrontierChanged();

        var statusBar = CreateStatusBar(editor, log);
        statusBar.UnrebuiltConnectionsCount.Should().Be(1, "primero hay una pérdida que resumir");

        editor.Undo();

        statusBar.UnrebuiltConnectionsCount.Should().Be(0);
        statusBar.HasUnrebuiltConnections.Should().BeFalse("lo que ya no está en el grafo no se resume");
    }

    /// <summary>Y una acción sana retira el resumen de la anterior, por el mismo motivo.</summary>
    [Fact]
    public void AHealthyPaste_ShouldRetireTheOlderSummary()
    {
        var (editor, log, _, _) = PasteAfterTheFrontierChanged();

        var statusBar = CreateStatusBar(editor, log);
        statusBar.UnrebuiltConnectionsCount.Should().Be(1);

        var source = EditorFixtures.AddNode(editor, new FolderSourceNode(), y: 500);
        var sink = EditorFixtures.AddNode(editor, new DestinationSinkNode(), x: 200, y: 500);
        editor.CreateConnection(source.OutputPorts.Single(), sink.InputPorts.Single());
        editor.ClipboardService.Copy([source, sink], editor.Connections);

        editor.PasteNodes();

        statusBar.UnrebuiltConnectionsCount.Should().Be(0, "el pegado sano no pierde ningún cable");
        statusBar.HasUnrebuiltConnections.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El pegado cuyo cable se pierde, ejecutado por donde lo ejecuta el usuario —el comando del lienzo—:
    /// devuelve el editor, la consola, el contenedor original y el pegado. Los dos contenedores se distinguen por
    /// identificador, que es de lo que va esta prueba.
    /// </summary>
    private static (EditorViewModel Editor, LogViewModel Log, NodeViewModel Original, NodeViewModel Pasted) PasteAfterTheFrontierChanged()
    {
        string definitionPath = SubflowFixtures.TempFile();
        SubflowFixtures.WriteFile(definitionPath, SubflowFixtures.DefinitionJson("In;Alternate", "Out"), DateTime.UtcNow);

        try
        {
            var loader = CreateLoader();
            var log = new LogViewModel(new InMemoryLogStore());
            var editor = new EditorViewModel(loader, logViewModel: log);

            var (original, _) = CopyAContainerWithCables(editor, definitionPath, "In;Alternate");

            // Entre copiar y pegar, el subflujo cambia su frontera: el puerto al que apuntaba el cable ya no
            // existe cuando el pegado intenta reconstruirlo.
            SubflowFixtures.WriteFile(definitionPath, SubflowFixtures.DefinitionJson("Entrada", "Salida"), DateTime.UtcNow.AddMinutes(1));

            editor.PasteNodes();
            log.FlushAllPendingLogs();

            var pasted = editor.Nodes.Single(node => node.Id != original.Id && node.Title == original.Title);
            return (editor, log, original, pasted);
        }
        finally
        {
            File.Delete(definitionPath);
        }
    }

    /// <summary>
    /// El mismo pegado, pero con <b>dos</b> cables al contenedor y una frontera que sigue ofreciendo los mismos
    /// puertos con una letra de más: los dos se pierden y los dos se pueden reconectar, que es lo que hace falta
    /// para ver bajar el recuento de dos a cero.
    /// </summary>
    private static (EditorViewModel Editor, LogViewModel Log) PasteWithTwoLostCables()
    {
        string definitionPath = SubflowFixtures.TempFile();
        SubflowFixtures.WriteFile(definitionPath, SubflowFixtures.DefinitionJson("In;Alternate;Alternate2", "Out"), DateTime.UtcNow);

        try
        {
            var loader = CreateLoader();
            var log = new LogViewModel(new InMemoryLogStore());
            var editor = new EditorViewModel(loader, logViewModel: log);

            CopyAContainerWithCables(editor, definitionPath, "In;Alternate;Alternate2");

            SubflowFixtures.WriteFile(definitionPath, SubflowFixtures.DefinitionJson("In;Alternates;Alternates2", "Out"), DateTime.UtcNow.AddMinutes(1));

            editor.PasteNodes();
            log.FlushAllPendingLogs();

            return (editor, log);
        }
        finally
        {
            File.Delete(definitionPath);
        }
    }

    /// <summary>
    /// Deja en el lienzo un origen y un contenedor unidos por tantos cables como puertos <paramref name="ports"/>
    /// nombre, y los copia. El contenedor se materializa desde el archivo, así que expone esos puertos —y el
    /// paquete del portapapeles se lleva los nombres, que son lo único que el pegado tiene para emparejar.
    /// </summary>
    private static (NodeViewModel Container, NodeViewModel Source) CopyAContainerWithCables(EditorViewModel editor, string definitionPath, string ports)
    {
        var source = EditorFixtures.AddNode(editor, new FolderSourceNode());

        var container = new SubflowNode
        {
            EmbedDefinition = false,
            SubflowPath = definitionPath,
            SubflowName = "Contenedor"
        };
        SubflowPortResolver.Materialize(container);
        var containerVm = EditorFixtures.AddNode(editor, container, x: 200);

        foreach (string port in ports.Split(';').Skip(1))
        {
            editor.CreateConnection(source.OutputPorts.Single(), containerVm.InputPorts.Single(input => input.Name == port));
        }

        editor.ClipboardService.Copy([source, containerVm], editor.Connections);
        return (containerVm, source);
    }

    private static StatusBarViewModel CreateStatusBar(EditorViewModel editor, LogViewModel log)
    {
        var loader = CreateLoader();
        var inspector = new NodeInspectorViewModel(editor, new NullFileDialogService(), log);

        var controlBar = new ControlBarViewModel(
            editor,
            loader,
            log,
            inspector,
            new NullFileDialogService(),
            new InMemoryWorkflowStorageService());

        return new StatusBarViewModel(editor, controlBar, new FrozenPerformanceMonitor(), log);
    }

    private static WorkflowNode Source(string id) => new()
    {
        Id = id,
        NodeTypeName = typeof(FolderSourceNode).FullName!
    };

    private static WorkflowEdge Edge(string sourceId, string sourcePort, string targetId, string targetPort) => new()
    {
        SourceNodeId = sourceId,
        SourcePortName = sourcePort,
        TargetNodeId = targetId,
        TargetPortName = targetPort
    };

    /// <summary>
    /// Contenedor de subflujo cuya definición no resuelve: expone los puertos genéricos y sólo los que su memoria
    /// diga. Es el nodo con puertos que un archivo guardado puede nombrar y ya no existir.
    /// </summary>
    private static WorkflowNode DanglingContainer(string id, string title, string inputs = "In") => new()
    {
        Id = id,
        NodeTypeName = typeof(SubflowNode).FullName!,
        CustomTitle = title,
        Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["SubflowPath"] = "/definicion/que/no/existe.json",
            ["SubflowName"] = title,
            ["RememberedInputPorts"] = inputs,
            ["RememberedOutputPorts"] = "Out"
        }
    };

    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);
        return loader;
    }
}
