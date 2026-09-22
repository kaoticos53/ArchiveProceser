using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Subflows;
using FileFlow.Sdk;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Pegar o duplicar nodos reconstruye las conexiones internas igual que abrir un flujo: emparejando
/// <b>nombres de puerto</b>. Cuando ese emparejamiento falla —el contenedor de subflujo cambió su frontera
/// entre copiar y pegar, o el nodo no existe en esta máquina porque falta su plugin— el cable se perdía
/// <b>en silencio</b>: era el último camino de reconstrucción que descartaba sin decirlo.
///
/// Ahora pegar devuelve el mismo informe que abrir (<see cref="ConnectionRebuildReport"/>, producido por la
/// misma regla, <see cref="ConnectionReconstructor"/>) y lo cuenta donde está la acción —el lienzo— y en la
/// consola. Estas pruebas fijan las dos mitades: lo que se cuenta, y <b>lo que no</b> —un aviso falso en cada
/// Ctrl+V es peor que el silencio, porque enseña a ignorar el canal—.
///
/// El caso «el nodo no está» no se reproduce aquí por el camino del pegado: el cargador resuelve cualquier
/// tipo de nodo presente en el proceso, así que un nodo ausente sólo llega con los nodos de otra máquina. Su
/// rama del informe es la misma que ya fija el camino de la apertura
/// (<c>DroppedConnectionsReportTests.LoadingACableToANodeThatCouldNotBeCreated_ShouldSayWhichNodeIsMissing</c>),
/// porque la regla es una sola.
/// </summary>
public class ClipboardDroppedConnectionsTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // El informe
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PastingAfterTheSubflowFrontierChanged_ShouldReportTheCableInsteadOfDroppingItSilently()
    {
        string definitionPath = SubflowFixtures.TempFile();

        try
        {
            SubflowFixtures.WriteFile(definitionPath, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), DateTime.UtcNow);

            var loader = CreateLoader();
            var origin = new EditorViewModel(loader);
            var clipboard = new NodeClipboardService(loader);

            var sourceVm = EditorFixtures.AddNode(origin, new FolderSourceNode());
            var container = Container(definitionPath);
            SubflowPortResolver.Materialize(container);
            var containerVm = EditorFixtures.AddNode(origin, container, x: 200);

            containerVm.InputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");
            origin.CreateConnection(sourceVm.OutputPorts.Single(), containerVm.InputPorts.Single(port => port.Name == "Alternate"));

            clipboard.Copy([sourceVm, containerVm], origin.Connections);

            // Entre copiar y pegar, el subflujo cambia su frontera: el puerto al que apuntaba el cable ya no
            // existe cuando el pegado intenta reconstruirlo.
            SubflowFixtures.WriteFile(definitionPath, SubflowFixtures.DefinitionJson("Entrada", "Salida"), DateTime.UtcNow.AddMinutes(1));

            var target = new EditorViewModel(loader);

            var result = clipboard.Paste(target);

            result.Nodes.Should().HaveCount(2, "los nodos sí se pegaron: lo que se pierde es el cable");
            target.Connections.Should().BeEmpty("el puerto 'Alternate' ya no existe y no se inventa un cable");

            result.Report.IsComplete.Should().BeFalse();
            var dropped = result.Report.DroppedConnections.Should().ContainSingle().Subject;
            dropped.Source.Problem.Should().Be(DroppedConnectionEndProblem.None, "el extremo de origen emparejaba bien");
            dropped.Target.Problem.Should().Be(DroppedConnectionEndProblem.MissingPort);
            dropped.Target.PortName.Should().Be("Alternate", "el puerto que falta es lo que hay que volver a conectar");
            dropped.Target.NodeName.Should().Be(containerVm.Title, "es el nombre con el que el usuario reconoce el nodo");
            dropped.Impediments.Should().ContainSingle("sólo un extremo explica la pérdida");
        }
        finally
        {
            File.Delete(definitionPath);
        }
    }

    [Fact]
    public void PastingNodesWhoseCablesAllMatch_ShouldReportNothing()
    {
        var loader = CreateLoader();
        var origin = new EditorViewModel(loader);
        var clipboard = new NodeClipboardService(loader);

        var sourceVm = EditorFixtures.AddNode(origin, new FolderSourceNode());
        var sinkVm = EditorFixtures.AddNode(origin, new DestinationSinkNode(), x: 200);
        origin.CreateConnection(sourceVm.OutputPorts.Single(), sinkVm.InputPorts.Single());

        clipboard.Copy([sourceVm, sinkVm], origin.Connections);

        var target = new EditorViewModel(loader);

        var result = clipboard.Paste(target);

        result.Report.IsComplete.Should().BeTrue("los dos puertos existen: no hay nada que contar");
        result.Report.DroppedConnections.Should().BeEmpty();
        target.Connections.Should().ContainSingle("el cable interno se reconstruyó");
    }

    /// <summary>
    /// Duplicar construye su paquete y lo pega en el mismo instante, así que no hay nada que pueda cambiar
    /// por medio: comparte la regla con pegar, y su mitad sana también calla.
    /// </summary>
    [Fact]
    public void DuplicatingNodesWhoseCablesAllMatch_ShouldReportNothing()
    {
        var loader = CreateLoader();
        var editor = new EditorViewModel(loader);

        var sourceVm = EditorFixtures.AddNode(editor, new FolderSourceNode());
        var sinkVm = EditorFixtures.AddNode(editor, new DestinationSinkNode(), x: 200);
        editor.CreateConnection(sourceVm.OutputPorts.Single(), sinkVm.InputPorts.Single());

        var result = editor.ClipboardService.Duplicate([sourceVm, sinkVm], editor.Connections, editor);

        result.Nodes.Should().HaveCount(2);
        result.Report.IsComplete.Should().BeTrue();
        editor.Connections.Should().HaveCount(2, "el cable original y el duplicado");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El aviso: pegar se cuenta donde está la acción, y también en la consola
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PastingWithALostCable_ShouldLeaveTheNoticeOnTheCanvasAndInTheConsole()
    {
        var (editor, log, containerVm) = PasteAfterTheFrontierChanged();

        editor.HasCanvasNotice.Should().BeTrue("el usuario acaba de pegar: se entera aquí, no dentro de diez acciones");

        // El detalle —qué nodo y qué puerto— vive en la fila que se puede arreglar, no en la cabecera del cartel.
        var fix = editor.CanvasNoticeFixes.Should().ContainSingle().Subject;
        fix.NodeTitle.Should().Be(containerVm.Title);
        fix.MissingPortName.Should().Be("Alternate", "el aviso dice qué nodo y qué puerto hay que volver a conectar");
        fix.Description.Should().Contain(containerVm.Title).And.Contain("Alternate");

        log.Logs.Should().Contain(record => record.Level == LogLevel.Warning && record.Message.Contains("Alternate"),
            "el registro es lo que queda cuando el aviso del lienzo se retira");
        editor.Connections.Should().ContainSingle("el cable perdido no se inventa, y el original sigue donde estaba");
    }

    [Fact]
    public void PastingNodesWithNothingToRebuild_ShouldNotWarnAboutAnything()
    {
        var (editor, log, _) = PasteAfterTheFrontierChanged();
        int warningsFromThePasteThatLostACable = log.WarningCount;
        warningsFromThePasteThatLostACable.Should().Be(1, "la otra mitad de la prueba: el pegado anterior sí avisó");

        // Un pegado sano, y con cable: dos nodos nuevos cuyos puertos sí emparejan. Tiene que reconstruir
        // su cable y no decir nada de él.
        var source = EditorFixtures.AddNode(editor, new FolderSourceNode(), y: 400);
        var sink = EditorFixtures.AddNode(editor, new DestinationSinkNode(), x: 200, y: 400);
        editor.CreateConnection(source.OutputPorts.Single(), sink.InputPorts.Single());
        editor.ClipboardService.Copy([source, sink], editor.Connections);
        int connectionsBeforePasting = editor.Connections.Count;

        editor.PasteNodes();

        editor.Connections.Should().HaveCount(connectionsBeforePasting + 1, "el pegado sano sí rehízo su cable");
        log.WarningCount.Should().Be(warningsFromThePasteThatLostACable,
            "un pegado que no pierde nada no añade avisos: uno falso en cada Ctrl+V enseña a ignorar el canal");
        editor.HasCanvasNotice.Should().BeFalse("el aviso cuenta la última acción, y ésta no perdió nada");
    }

    [Fact]
    public void UndoingThePaste_ShouldRetireTheNotice()
    {
        var (editor, _, _) = PasteAfterTheFrontierChanged();

        int nodesAfterPasting = editor.Nodes.Count;
        editor.HasCanvasNotice.Should().BeTrue("primero hay un aviso que retirar");

        editor.Undo();

        editor.Nodes.Should().HaveCount(nodesAfterPasting - 2, "el deshacer revirtió el pegado, que es lo que contaba el aviso");
        editor.HasCanvasNotice.Should().BeFalse("un aviso sobre algo que el usuario ya revirtió es una mentira");
    }

    [Fact]
    public void UndoingAndRedoingThePaste_ShouldLeaveTheGraphWhereItWas()
    {
        var (editor, _, _) = PasteAfterTheFrontierChanged();

        int nodesAfterPasting = editor.Nodes.Count;

        editor.Undo();
        editor.Redo();

        editor.Nodes.Should().HaveCount(nodesAfterPasting, "un Ctrl+Z deshace el pegado entero —nodos incluidos— y un Ctrl+Y lo repone");
        editor.Connections.Should().ContainSingle("los nodos vuelven con sus cables, y el que se perdió sigue perdido");
    }

    [Fact]
    public void DismissingTheNotice_ShouldLeaveTheConsoleAsTheOnlyTrace()
    {
        var (editor, log, containerVm) = PasteAfterTheFrontierChanged();

        editor.DismissCanvasNoticeCommand.Execute(null);

        editor.HasCanvasNotice.Should().BeFalse();
        log.Logs.Should().Contain(record => record.Level == LogLevel.Warning && record.Message.Contains("Alternate"),
            "el registro no depende de que el banner siga a la vista");
        editor.Nodes.Should().Contain(node => node.Title == containerVm.Title, "descartar el aviso no toca el grafo");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Monta el pegado cuyo cable se pierde y lo ejecuta por donde lo ejecuta el usuario —el comando del
    /// lienzo—, devolviendo el editor, la consola y el contenedor pegado. El cable original queda en el
    /// lienzo: sin él no habría nada que perder al pegar.
    /// </summary>
    private static (EditorViewModel Editor, LogViewModel Log, NodeViewModel Container) PasteAfterTheFrontierChanged()
    {
        string definitionPath = SubflowFixtures.TempFile();
        SubflowFixtures.WriteFile(definitionPath, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), DateTime.UtcNow);

        try
        {
            var loader = CreateLoader();
            var log = new LogViewModel(new InMemoryLogStore());
            var editor = new EditorViewModel(loader, logViewModel: log);

            var sourceVm = EditorFixtures.AddNode(editor, new FolderSourceNode());
            var container = Container(definitionPath);
            SubflowPortResolver.Materialize(container);
            var containerVm = EditorFixtures.AddNode(editor, container, x: 200);
            editor.CreateConnection(sourceVm.OutputPorts.Single(), containerVm.InputPorts.Single(port => port.Name == "Alternate"));

            editor.ClipboardService.Copy([sourceVm, containerVm], editor.Connections);

            SubflowFixtures.WriteFile(definitionPath, SubflowFixtures.DefinitionJson("Entrada", "Salida"), DateTime.UtcNow.AddMinutes(1));

            editor.PasteNodes();
            log.FlushAllPendingLogs();

            return (editor, log, containerVm);
        }
        finally
        {
            File.Delete(definitionPath);
        }
    }

    /// <summary>
    /// Contenedor que resuelve su frontera desde un archivo —no incrustada—, que es lo que permite que la
    /// frontera cambie entre copiar y pegar sin que el paquete se entere.
    /// </summary>
    private static SubflowNode Container(string definitionPath) => new()
    {
        EmbedDefinition = false,
        SubflowPath = definitionPath,
        SubflowName = "Contenedor"
    };

    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);
        return loader;
    }
}
