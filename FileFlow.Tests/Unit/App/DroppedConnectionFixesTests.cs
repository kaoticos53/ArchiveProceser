using Avalonia;
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
/// El aviso de un cable perdido, hecho accionable: la fila lleva el nodo cuyo puerto falta, el puerto vigente
/// que más se le parece y la reconexión a un clic.
///
/// Antes el aviso sólo se leía, y eso dejaba todo el trabajo al usuario: buscar el nodo a ojo, averiguar qué
/// puerto era y volver a trazar el cable. Lo que se fija aquí son las dos mitades y, sobre todo, sus
/// <b>límites</b>: lo que no se puede afirmar no se ofrece —un nodo que no está no se puede señalar, y un
/// puerto que ya tiene cable no es candidato, porque reconectar ahí tiraría el cable que ya estaba—.
///
/// El camino que se recorre es el de abrir un archivo, que es donde más cables se pierden y el que hasta ahora
/// sólo contaba la consola: el subflujo cambia entre que el flujo se guardó y se vuelve a abrir, que es
/// exactamente lo que pasa cuando el flujo viene de otra máquina.
/// </summary>
public class DroppedConnectionFixesTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // La fila: el nodo, la propuesta y el clic que lo arregla
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void OpeningAFileWhoseSubflowDrifted_ShouldOfferTheNodeAndTheClosestPort()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            // El flujo se guardó apuntando a un subflujo con «Alternate», y el subflujo ya dice «Alternates».
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out"), DateTime.UtcNow);
            var graph = SavedFileWithAContainerOn(path, cables: [("origen", "contenedor", "Alternate")]);
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternates", "Out"), DateTime.UtcNow.AddMinutes(1));

            var editor = new EditorViewModel(CreateLoader());

            editor.LoadFromGraphModel(AsSavedFile(graph));

            editor.Connections.Should().BeEmpty("el puerto nombrado ya no existe y el cable no se inventa");
            editor.HasCanvasNotice.Should().BeTrue("y eso hay que contarlo donde se puede arreglar");

            var fix = editor.CanvasNoticeFixes.Should().ContainSingle().Subject;
            fix.NodeTitle.Should().Be("Contenedor", "la fila nombra el nodo al que hay que ir");
            fix.MissingPortName.Should().Be("Alternate", "y el puerto que falta");
            fix.ProposedPortName.Should().Be("Alternates", "una letra de diferencia es la propuesta");
            fix.CanReconnect.Should().BeTrue();
            fix.ReconnectText.Should().Contain("Alternates", "el botón dice a qué va a reconectar, no sólo que reconecta");

            fix.ReconnectCommand.Execute(null);

            editor.Connections.Should().ContainSingle("el clic vuelve a trazar el cable");
            editor.Connections[0].Source.NodeOwner.Id.Should().Be("origen");
            editor.Connections[0].Target.NodeOwner.Title.Should().Be("Contenedor");
            editor.Connections[0].Target.Name.Should().Be("Alternates");
            editor.HasCanvasNotice.Should().BeFalse("era el único cable perdido: ya no queda nada que contar");
            editor.CanvasNoticeFixes.Should().BeEmpty();

            // Y el clic no es irreversible: es una conexión como cualquier otra, y el usuario puede haberse
            // equivocado al aceptar la propuesta.
            editor.Undo();
            editor.Connections.Should().BeEmpty("deshacer devuelve el grafo a como estaba antes del clic");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheFix_ShouldTakeTheViewToTheNodeWhosePortIsMissing()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out"), DateTime.UtcNow);
            var graph = SavedFileWithAContainerOn(path, cables: [("origen", "contenedor", "Alternate")]);
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternates", "Out"), DateTime.UtcNow.AddMinutes(1));

            var editor = new EditorViewModel(CreateLoader());
            editor.LoadFromGraphModel(AsSavedFile(graph));

            var container = editor.Nodes.Single(node => node.Title == "Contenedor");
            container.Location = new Point(4200, 2600);
            editor.ViewportZoom = 1.0;

            var fix = editor.CanvasNoticeFixes.Single();
            fix.GoToNodeCommand.Execute(null);

            container.IsSelected.Should().BeTrue("ir al nodo es llegar a él con él señalado");
            editor.Nodes.Where(node => node.Id != container.Id).Should().OnlyContain(node => !node.IsSelected);
            editor.SelectedNode.Should().BeSameAs(container, "el inspector tiene que mirar el nodo que se fue a ver");
            editor.ViewportLocation.Should().Be(EditorViewportCalculator.CenterOn(container, editor.ViewportZoom),
                "y el encuadre lo deja centrado, sin cambiar el zoom con el que el usuario estaba trabajando");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Los límites: lo que no se puede afirmar no se ofrece
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El puerto que más se parece a uno perdido es, a veces, uno que <b>ya tiene cable</b>. Proponerlo sería
    /// ofrecer un botón que tira el cable que sobrevivió, y el usuario no lo pidió: la fila se queda sin
    /// propuesta y con el botón que sí cumple —el que lleva al nodo—.
    /// </summary>
    [Fact]
    public void TheFix_ShouldNotProposeAPortThatAlreadyHasACable()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out"), DateTime.UtcNow);

            // Dos cables al contenedor: uno al puerto que existe —y lo ocupa— y otro a un nombre que no está.
            var graph = SavedFileWithAContainerOn(path, cables:
            [
                ("origen", "contenedor", "Alternate"),
                ("origen", "contenedor", "Alternat")
            ]);

            var editor = new EditorViewModel(CreateLoader());

            editor.LoadFromGraphModel(AsSavedFile(graph));

            editor.Connections.Should().ContainSingle("el cable al puerto que sí existe se reconstruye");
            editor.Connections[0].Target.Name.Should().Be("Alternate");

            var fix = editor.CanvasNoticeFixes.Should().ContainSingle().Subject;
            fix.MissingPortName.Should().Be("Alternat");
            fix.ProposedPortName.Should().BeNull("el único parecido está ocupado por el cable que se salvó");
            fix.CanReconnect.Should().BeFalse();
            fix.GoToNodeCommand.CanExecute(null).Should().BeTrue("ir al nodo sí se puede, y es lo que queda");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Un nodo que no se pudo crear no está en el lienzo, y a un nodo que no está no se puede ir: de ese cable
    /// queda el aviso en la consola —con su nombre, que es lo que se busca para saber qué plugin falta— y
    /// ninguna fila, porque una fila sin nada que ofrecer es un botón que no cumple.
    /// </summary>
    [Fact]
    public void ACableWhoseNodeCouldNotBeCreated_ShouldBeReportedOnTheConsoleWithoutAFix()
    {
        var graph = new WorkflowGraph { Name = "Nodo que no está" };
        graph.Nodes.Add(new WorkflowNode { Id = "origen", NodeTypeName = typeof(FolderSourceNode).FullName! });
        graph.Nodes.Add(new WorkflowNode { Id = "fantasma", NodeTypeName = "FileFlow.Plugin.Que.No.Existe.Nodo" });
        graph.Edges.Add(new WorkflowEdge
        {
            SourceNodeId = "origen",
            SourcePortName = "Out",
            TargetNodeId = "fantasma",
            TargetPortName = "In"
        });

        var log = new LogViewModel(new InMemoryLogStore());
        var editor = new EditorViewModel(CreateLoader(), logViewModel: log);

        editor.LoadFromGraphModel(graph);
        log.FlushAllPendingLogs();

        editor.HasCanvasNotice.Should().BeTrue("el cable se perdió y hay que contarlo");
        editor.CanvasNoticeFixes.Should().BeEmpty("pero no hay a dónde llevar al usuario");
        log.Logs.Should().Contain(record => record.Level == LogLevel.Warning && record.Message.Contains("No.Existe"),
            "el tipo del nodo que falta es lo que permite saber qué plugin hay que instalar");
    }

    /// <summary>
    /// El lienzo también puede rechazar la reconexión —no admite, por ejemplo, un cable de un nodo consigo
    /// mismo—, y entonces la fila <b>se queda</b>: quitarla sería contar como arreglado un cable que sigue sin
    /// estar. Un botón que miente es peor que un botón que no hace nada.
    /// </summary>
    [Fact]
    public void Reconnecting_WhenTheCanvasRefusesTheConnection_ShouldKeepTheRow()
    {
        var editor = new EditorViewModel(CreateLoader());
        var node = EditorFixtures.AddNode(editor, new SubflowNode());

        var fix = new DroppedConnectionFixViewModel(
            node,
            "Alternate",
            "Contenedor(Out) → Contenedor(Alternate)",
            "In",
            _ => { },
            editor.ReconnectLostConnection,
            FileFlow.Sdk.Localization.LocalizationManager.Instance)
        {
            CanReconnect = true,
            LiveEnds = (node.OutputPorts.Single(), node.InputPorts.Single())
        };

        editor.CanvasNoticeFixes.Add(fix);

        fix.ReconnectCommand.Execute(null);

        editor.Connections.Should().BeEmpty("el lienzo no admite una conexión de un nodo consigo mismo");
        editor.CanvasNoticeFixes.Should().Contain(fix, "y la fila se queda: quitarla sería decir que se arregló");
    }

    [Fact]
    public void AFileThatRebuildsEverything_ShouldLeaveNoNoticeAndNoFix()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out"), DateTime.UtcNow);
            var graph = SavedFileWithAContainerOn(path, cables: [("origen", "contenedor", "Alternate")]);

            var editor = new EditorViewModel(CreateLoader());

            editor.LoadFromGraphModel(AsSavedFile(graph));

            editor.Connections.Should().ContainSingle("el cable se reconstruyó");
            editor.HasCanvasNotice.Should().BeFalse("no hay nada que contar ni nada que arreglar");
            editor.CanvasNoticeFixes.Should().BeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Un flujo con un origen y un contenedor de subflujo que resuelve su frontera desde un archivo —no
    /// incrustada—, que es lo que permite que la frontera cambie entre que el flujo se guarda y se abre. Los
    /// cables se declaran por nombre de puerto, que es como los guarda el archivo.
    /// </summary>
    private static WorkflowGraph SavedFileWithAContainerOn(string definitionPath, (string Source, string Target, string Port)[] cables)
    {
        var graph = new WorkflowGraph { Name = "Flujo con subflujo en disco" };

        graph.Nodes.Add(new WorkflowNode
        {
            Id = "origen",
            NodeTypeName = typeof(FolderSourceNode).FullName!,
            X = 0
        });

        graph.Nodes.Add(new WorkflowNode
        {
            Id = "contenedor",
            NodeTypeName = typeof(SubflowNode).FullName!,
            CustomTitle = "Contenedor",
            X = 200,
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["EmbedDefinition"] = false,
                ["SubflowPath"] = definitionPath,
                ["SubflowName"] = "Contenedor"
            }
        });

        foreach (var (source, target, port) in cables)
        {
            graph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = source,
                SourcePortName = "Out",
                TargetNodeId = target,
                TargetPortName = port
            });
        }

        return graph;
    }

    /// <summary>Ida y vuelta por JSON: el grafo llega como llega desde un archivo guardado.</summary>
    private static WorkflowGraph AsSavedFile(WorkflowGraph graph) => WorkflowGraph.FromJson(graph.ToJson());

    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);
        return loader;
    }
}
