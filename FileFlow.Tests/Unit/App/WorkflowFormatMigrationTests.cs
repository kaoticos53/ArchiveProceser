using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Subflows;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// El archivo de flujo declara su versión y se repara al leerlo cuando es más antiguo que el formato
/// actual. Sin versión, un archivo al que le faltan datos y uno completo se leen igual, así que no se puede
/// saber cuál de los dos necesita reparación —y reparar el que no la necesita es inventar puertos que la
/// definición del subflujo no declara—. La versión es esa frontera.
///
/// Estas pruebas fijan las dos mitades: que lo que se escribe la declara, y que un archivo que no la
/// declara —es decir, todos los guardados antes de que el formato se versionara— recupera de sus propias
/// aristas los puertos que el contenedor exponía, que es lo único que conserva de ellos.
/// </summary>
public class WorkflowFormatMigrationTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // La versión
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, WorkflowFormat.UndeclaredVersion)]
    [InlineData("", WorkflowFormat.UndeclaredVersion)]
    [InlineData("   ", WorkflowFormat.UndeclaredVersion)]
    [InlineData("FileFlow.Workflow", WorkflowFormat.UndeclaredVersion)]
    [InlineData("no-es-un-schema", WorkflowFormat.UndeclaredVersion)]
    [InlineData("FileFlow.Workflow.v", WorkflowFormat.UndeclaredVersion)]
    [InlineData("FileFlow.Workflow.v0", WorkflowFormat.UndeclaredVersion)]
    [InlineData("FileFlow.Workflow.v1", 1)]
    [InlineData("FileFlow.Workflow.v2", 2)]
    [InlineData("FileFlow.Workflow.v3", 3)]
    [InlineData("FileFlow.Workflow.V7", 7)]
    public void VersionOf_ShouldReadTheDeclaredVersion_AndTreatAnythingUnreadableAsUndeclared(
        string? schema, int expected)
    {
        WorkflowFormat.VersionOf(schema).Should().Be(expected);
    }

    [Fact]
    public void CurrentSchema_ShouldDeclareTheCurrentVersion()
    {
        WorkflowFormat.VersionOf(WorkflowFormat.CurrentSchema).Should().Be(WorkflowFormat.CurrentVersion,
            "el esquema que se escribe y el número con el que se compara son el mismo dato escrito dos veces");
    }

    [Fact]
    public async Task SavingAWorkflow_ShouldDeclareTheFormatVersionInTheFile()
    {
        string file = SubflowFixtures.TempFile();

        try
        {
            var graph = new WorkflowGraph { Name = "Con versión" };
            graph.Nodes.Add(Source("src"));
            await new WorkflowStorageService().SaveWorkflowAsync(file, graph);

            File.ReadAllText(file).Should().Contain(WorkflowFormat.CurrentSchema,
                "el archivo tiene que decir con qué formato está escrito, o al leerlo parecerá anterior");

            var reopened = await new WorkflowStorageService().LoadWorkflowAsync(file);
            WorkflowFormat.VersionOf(reopened).Should().Be(WorkflowFormat.CurrentVersion);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La migración de un archivo que no la declara
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AWorkflowSavedWithoutAVersion_ShouldRecoverTheContainerPortsOnlyItsEdgesName()
    {
        // El contenedor apunta a un subflujo que ya no está: es el caso del flujo compartido sin su
        // definición, y también el de los guardados antiguos, que no escribían la definición incrustada.
        var file = new WorkflowGraph { Name = "Antiguo" };
        file.Nodes.Add(Source("src"));
        file.Nodes.Add(UnresolvedContainer("container"));
        file.Nodes.Add(Sink("sink"));
        file.Edges.Add(Edge("src", "Out", "container", "Alternate"));
        file.Edges.Add(Edge("container", "Errores", "sink", "In"));

        var editor = new EditorViewModel(CreateLoader());

        editor.LoadFromGraphModel(WorkflowFileFixtures.FileFromOlderFormat(file));

        var containerVm = editor.Nodes.Single(node => node.Id == "container");
        containerVm.InputPorts.Select(port => port.Name).Should().Equal(
            new[] { "Alternate" },
            "las aristas son lo único que el archivo conserva de los puertos que el contenedor exponía");
        containerVm.OutputPorts.Select(port => port.Name).Should().Equal("Errores");
        editor.Connections.Should().HaveCount(2,
            "los dos cables apuntan a puertos que acaban de recuperarse de esas mismas aristas");
    }

    [Fact]
    public void AWorkflowSavedWithoutAVersion_ShouldKeepTheGenericPortsOfAContainerWithNothingToRecover()
    {
        // Sin aristas no hay nada que recuperar, y el contenedor no puede quedarse sin puertos: un nodo sin
        // puertos no es conectable. Su respaldo son los genéricos, como en cualquier archivo sin memoria.
        var file = new WorkflowGraph { Name = "Antiguo" };
        file.Nodes.Add(UnresolvedContainer("container"));

        var editor = new EditorViewModel(CreateLoader());

        editor.LoadFromGraphModel(WorkflowFileFixtures.FileFromOlderFormat(file));

        var containerVm = editor.Nodes.Single(node => node.Id == "container");
        containerVm.InputPorts.Select(port => port.Name).Should().Equal("In");
        containerVm.OutputPorts.Select(port => port.Name).Should().Equal("Out");
    }

    [Theory]
    [InlineData(WorkflowFormat.CurrentSchema)]
    [InlineData("FileFlow.Workflow.v3")]
    public void AWorkflowThatDeclaresItsOwnVersion_ShouldNotRecoverPortsFromItsEdges(string schema)
    {
        // Un archivo del formato actual sí guarda los puertos que el contenedor exponía, y uno posterior a
        // este formato sabe aún más: en los dos casos lo que dicen sus aristas puede estar desfasado —un
        // cable viejo apuntando a un puerto que el contenedor ya no expone— y recuperarlo sería resucitar un
        // puerto que su definición no declara.
        var file = new WorkflowGraph { Name = "Con su propia versión", Schema = schema };
        file.Nodes.Add(Source("src"));
        file.Nodes.Add(new WorkflowNode
        {
            Id = "container",
            NodeTypeName = typeof(SubflowNode).FullName!,
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubflowPath"] = SubflowFixtures.TempFile(),
                ["SubflowName"] = "Contenedor",
                ["RememberedInputPorts"] = "In",
                ["RememberedOutputPorts"] = "Out"
            }
        });
        file.Edges.Add(Edge("src", "Out", "container", "In"));
        file.Edges.Add(Edge("src", "Out", "container", "Fantasma"));

        var graph = WorkflowFileFixtures.SavedFile(file);
        WorkflowFormat.VersionOf(graph).Should().Be(WorkflowFormat.VersionOf(schema),
            "la versión declarada es la que llega al plan de migración");

        var editor = new EditorViewModel(CreateLoader());

        editor.LoadFromGraphModel(graph);

        var containerVm = editor.Nodes.Single(node => node.Id == "container");
        containerVm.InputPorts.Select(port => port.Name).Should().Equal(
            new[] { "In" },
            "lo que el archivo guardó manda sobre lo que sus aristas sugieren");
        editor.Connections.Should().ContainSingle(
            "el cable que apunta al puerto desfasado no encuentra dónde conectarse y se descarta");
        editor.Connections[0].Target.Name.Should().Be("In");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

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

    /// <summary>Contenedor de subflujo cuya definición ya no resuelve: puertos perdidos, si no se recuperan.</summary>
    private static WorkflowNode UnresolvedContainer(string id) => new()
    {
        Id = id,
        NodeTypeName = typeof(SubflowNode).FullName!,
        Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["SubflowPath"] = SubflowFixtures.TempFile(),
            ["SubflowName"] = "Contenedor"
        }
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
