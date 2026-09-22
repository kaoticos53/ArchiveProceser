using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Scripting;
using FileFlow.Plugin.Subflows;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Al reabrir un flujo (y al pegar o duplicar nodos) las conexiones se reconstruyen emparejando
/// <b>nombres de puerto</b>. Un nodo cuyos puertos derivan de su configuración —los declarados de un
/// script, la frontera de un subflujo contenedor— no los tiene hasta que alguien se los pide, así que si
/// el emparejamiento ocurre antes de materializarlos, la arista no encuentra su puerto y el cable se pierde
/// <b>en silencio</b>: el flujo reabierto parece completo y ya no lo está.
///
/// Estas pruebas fijan que la materialización ocurre <i>antes</i> de reconstruir conexiones, y describen el
/// grafo como lo deja un archivo guardado (pasa por JSON, así que los parámetros vuelven como
/// <see cref="System.Text.Json.JsonElement"/> y no como los objetos que se escribieron).
/// </summary>
public class DynamicPortsOnReloadTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Carga de un flujo guardado
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LoadingAWorkflow_ShouldMaterializeTheDeclaredPortsOfAScript_BeforeMatchingItsEdges()
    {
        var graph = new WorkflowGraph { Name = "Script con puertos propios" };
        graph.Nodes.Add(Source("src", x: 0));
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "script",
            NodeTypeName = typeof(CustomScriptNode).FullName!,
            X = 200,
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Language"] = "CSharp",
                ["InputPorts"] = "In",
                ["OutputPorts"] = "Out,Errores"
            }
        });
        graph.Nodes.Add(Sink("sink", x: 400));
        graph.Edges.Add(Edge("src", "Out", "script", "In"));
        graph.Edges.Add(Edge("script", "Errores", "sink", "In"));

        var editor = new EditorViewModel(CreateLoader());

        editor.LoadFromGraphModel(AsSavedFile(graph));

        var scriptVm = editor.Nodes.Single(node => node.Id == "script");
        // Los puertos declarados en el perfil existen antes de emparejar las aristas.
        scriptVm.OutputPorts.Select(port => port.Name).Should().Equal("Out", "Errores");
        editor.Connections.Should().HaveCount(2);
        editor.Connections.Should().Contain(connection =>
            connection.Source.Name == "Errores" && connection.Target.NodeOwner.Id == "sink");
    }

    [Fact]
    public void LoadingAWorkflow_ShouldMaterializeTheBoundaryOfASubflowContainer_BeforeMatchingItsEdges()
    {
        var graph = new WorkflowGraph { Name = "Contenedor con frontera propia" };
        graph.Nodes.Add(Source("src", x: 0));
        graph.Nodes.Add(Container("container", x: 200));
        graph.Nodes.Add(Sink("sink", x: 400));
        graph.Edges.Add(Edge("src", "Out", "container", "Alternate"));
        graph.Edges.Add(Edge("container", "Errores", "sink", "In"));

        var editor = new EditorViewModel(CreateLoader());

        editor.LoadFromGraphModel(AsSavedFile(graph));

        var containerVm = editor.Nodes.Single(node => node.Id == "container");
        containerVm.InputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");
        containerVm.OutputPorts.Select(port => port.Name).Should().Equal("Out", "Errores");

        editor.Connections.Should().HaveCount(2,
            "los cables del subflujo apuntan a puertos que ya existen cuando se emparejan");
        editor.Connections.Should().Contain(connection =>
            connection.Source.Name == "Errores" && connection.Target.NodeOwner.Id == "sink");
        editor.Connections.Should().Contain(connection =>
            connection.Source.NodeOwner.Id == "src" && connection.Target.Name == "Alternate");
    }

    [Fact]
    public void LoadingAWorkflow_ShouldNotLoseEdgesOfAFixedPortNode()
    {
        var graph = new WorkflowGraph { Name = "Puertos fijos" };
        graph.Nodes.Add(Source("src", x: 0));
        graph.Nodes.Add(Sink("sink", x: 200));
        graph.Edges.Add(Edge("src", "Out", "sink", "In"));

        var editor = new EditorViewModel(CreateLoader());

        editor.LoadFromGraphModel(AsSavedFile(graph));

        editor.Connections.Should().ContainSingle("la materialización no altera a los nodos de puertos fijos");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Pegado de nodos: el mismo camino, la misma pérdida
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PastingCopiedNodes_ShouldMaterializeTheBoundaryOfTheSubflowContainer_BeforeMatchingItsEdges()
    {
        var loader = CreateLoader();
        var origin = new EditorViewModel(loader);
        var clipboard = new NodeClipboardService(loader);

        var subflow = ContainerInstance();
        SubflowPortResolver.Materialize(subflow);

        var sourceVm = EditorFixtures.AddNode(origin, new FolderSourceNode());
        var containerVm = EditorFixtures.AddNode(origin, subflow, x: 200);
        origin.Connections.Add(new ConnectionViewModel(
            sourceVm.OutputPorts.Single(),
            containerVm.InputPorts.Single(port => port.Name == "Alternate")));

        clipboard.Copy([sourceVm, containerVm], origin.Connections);

        var target = new EditorViewModel(loader);

        var pasted = clipboard.Paste(target);

        pasted.Should().HaveCount(2);
        var pastedContainer = pasted.Single(node => node.IsSubflowNode);
        pastedContainer.InputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");
        target.Connections.Should().ContainSingle("la arista pegada conserva su puerto de destino");
        target.Connections[0].Target.Name.Should().Be("Alternate");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El inspector: mismo descubrimiento, fuera del alcance de la ejecución
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EditingTheSubflowDefinitionInTheInspector_ShouldExposeItsFrontierPorts()
    {
        var editor = new EditorViewModel(CreateLoader());
        var containerVm = EditorFixtures.AddNode(editor, new SubflowNode());

        containerVm.InputPorts.Select(port => port.Name).Should().Equal("In");

        containerVm.OnParameterValueChanged("SubflowDefinitionJson", SubflowDefinitionJson());
        containerVm.OnParameterValueChanged("EmbedDefinition", true);

        containerVm.InputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");
        containerVm.OutputPorts.Select(port => port.Name).Should().Equal("Out", "Errores");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El resolutor, que es la única regla que decide qué puertos expone un subflujo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolvingASubflowFromAFile_ShouldDiscoverTheBoundaryPortsItDeclares()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fileflow-subflow-{Guid.NewGuid():N}.flow");
        File.WriteAllText(path, SubflowDefinitionJson());

        try
        {
            var subflowNode = new SubflowNode { EmbedDefinition = false, SubflowPath = path };

            var (inputs, outputs) = SubflowPortResolver.Discover(subflowNode);

            inputs.Should().Equal("In", "Alternate");
            outputs.Should().Equal("Out", "Errores");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ResolvingASubflowWithoutDefinition_ShouldFallBackToTheGenericPorts()
    {
        var subflowNode = new SubflowNode { SubflowPath = $"no-existe-{Guid.NewGuid():N}.flow" };

        var (inputs, outputs) = SubflowPortResolver.Discover(subflowNode);

        inputs.Should().Equal("In");
        outputs.Should().Equal("Out");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(CustomScriptNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);
        return loader;
    }

    /// <summary>Ida y vuelta por JSON, para que el grafo llegue como llega desde un archivo guardado.</summary>
    private static WorkflowGraph AsSavedFile(WorkflowGraph graph) => WorkflowGraph.FromJson(graph.ToJson());

    private static WorkflowNode Source(string id, double x) => new()
    {
        Id = id,
        NodeTypeName = typeof(FolderSourceNode).FullName!,
        X = x
    };

    private static WorkflowNode Sink(string id, double x) => new()
    {
        Id = id,
        NodeTypeName = typeof(DestinationSinkNode).FullName!,
        X = x
    };

    /// <summary>Contenedor de subflujo tal como lo deja el archivo: definición incrustada y su nombre.</summary>
    private static WorkflowNode Container(string id, double x) => new()
    {
        Id = id,
        NodeTypeName = typeof(SubflowNode).FullName!,
        CustomTitle = "Contenedor",
        X = x,
        Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EmbedDefinition"] = true,
            ["SubflowDefinitionJson"] = SubflowDefinitionJson(),
            ["SubflowName"] = "Contenedor"
        }
    };

    private static SubflowNode ContainerInstance()
    {
        var node = new SubflowNode
        {
            EmbedDefinition = true,
            SubflowDefinitionJson = SubflowDefinitionJson(),
            SubflowName = "Contenedor"
        };
        return node;
    }

    /// <summary>Subgrafo con frontera propia: dos entradas y dos salidas declaradas.</summary>
    private static string SubflowDefinitionJson() =>
        SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores");

    private static WorkflowEdge Edge(string sourceId, string sourcePort, string targetId, string targetPort) => new()
    {
        SourceNodeId = sourceId,
        SourcePortName = sourcePort,
        TargetNodeId = targetId,
        TargetPortName = targetPort
    };
}
