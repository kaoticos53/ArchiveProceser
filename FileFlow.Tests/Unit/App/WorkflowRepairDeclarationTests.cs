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
/// Un archivo reparado <b>converge</b>: en cuanto se le aplica la reparación, guardarlo deja de declararse
/// anterior, así que la próxima apertura no vuelve a repararlo y el archivo queda en el formato que se escribe
/// hoy —y entra, por tanto, en la protección que impide sobrescribir uno posterior—.
///
/// <para>
/// Son dos mitades que sólo funcionan juntas. El que <b>escribe</b> no declara el formato actual por escribir:
/// mientras nadie repare, un archivo anterior se guarda como lo que era, porque declararlo actual enterraría
/// su reparación —el que lo abra después ya no la haría—. Y el que <b>repara</b> tiene que dejar en el grafo lo
/// que recuperó, no sólo en la pantalla: un grafo declarado actual sin la reparación dentro pierde los cables
/// que esa reparación recuperaba, que es el defecto que estas pruebas impiden que vuelva.
/// </para>
/// </summary>
public class WorkflowRepairDeclarationTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // De qué versión viene un grafo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AFreshGraph_ShouldNotDeclareAnySourceVersion()
    {
        new WorkflowGraph().Schema.Should().BeNull(
            "un grafo construido en memoria no viene de ningún archivo, y eso es lo único que significa no traer versión");
    }

    [Fact]
    public async Task AGraphReadFromAFileWithoutAVersion_ShouldRememberTheFormatItWasWrittenIn()
    {
        string file = SubflowFixtures.TempFile();

        try
        {
            var graph = new WorkflowGraph { Name = "Sin versión" };
            graph.Nodes.Add(Source("src"));
            File.WriteAllText(file, WorkflowFileFixtures.WithoutVersionDeclaration(graph));

            var byTheApp = await new WorkflowStorageService().LoadWorkflowAsync(file);
            var byTheCore = WorkflowGraph.FromJson(File.ReadAllText(file));
            var byTheAppInMemory = new WorkflowStorageService().DeserializeGraph(File.ReadAllText(file));

            foreach (var read in new[] { byTheApp, byTheCore, byTheAppInMemory })
            {
                WorkflowFormat.VersionOf(read).Should().Be(WorkflowFormat.UndeclaredVersion);
                read.Schema.Should().Be(WorkflowFormat.UndeclaredSchema,
                    "el grafo tiene que recordar con qué formato se escribió el archivo, para que «sin declarar» " +
                    "no signifique a la vez «viene de un archivo anterior» y «no viene de ningún archivo»");
            }
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void AGraphReadFromANewerFile_ShouldKeepTheVersionItDeclared()
    {
        var read = WorkflowGraph.FromJson(new WorkflowGraph { Name = "Posterior", Schema = "FileFlow.Workflow.v3" }.ToJson());

        WorkflowFormat.VersionOf(read).Should().Be(3,
            "anotar de dónde viene un grafo no puede inventarle una versión: la que el archivo declara es suya");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Guardar sin reparar
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SavingAGraphThatCameFromAnOlderFileWithoutRepairingIt_ShouldNotDeclareTheCurrentFormat()
    {
        var written = new WorkflowStorageService().SerializeGraph(OldFileWithCables());

        WorkflowFormat.VersionOf(written).Should().Be(WorkflowFormat.UndeclaredVersion,
            "escribir un archivo anterior no lo repara: declararlo actual dejaría enterrada su reparación, y el " +
            "próximo que lo abra ya no la haría");
        written.Should().Contain(WorkflowFormat.UndeclaredSchema,
            "dice lo que es —el formato anterior al versionado— en vez de lo que no es");
    }

    [Fact]
    public void SavingAGraphFromAnOlderFileWithoutRepairingIt_ShouldKeepItsRepairPending()
    {
        string first = new WorkflowStorageService().SerializeGraph(OldFileWithCables());

        // La reparación se calcula sobre el archivo, así que sigue siendo posible para el que lo abra: eso es
        // exactamente lo que se perdería si el archivo se hubiera declarado actual.
        var again = WorkflowGraph.FromJson(first);
        var recovered = WorkflowFormat.Plan(again).PortsNamedByEdges("container");

        recovered.Inputs.Should().Equal("Alternate");
        recovered.Outputs.Should().Equal("Errores");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Reparar: la reparación se declara y el archivo converge
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RepairingAGraphFromAnOlderFile_ShouldDeclareTheGraphRepaired()
    {
        var graph = OldFileWithCables();

        var editor = new EditorViewModel(CreateLoader());
        editor.LoadFromGraphModel(graph);

        WorkflowFormat.VersionOf(graph).Should().Be(WorkflowFormat.CurrentVersion,
            "la reparación ya se aplicó, así que guardar este grafo deja de decir que es anterior");

        var containerDto = graph.Nodes.Single(node => node.Id == "container");
        containerDto.Parameters[ISubflowNode.RememberedInputPortsKey].Should().Be("Alternate",
            "lo que se recuperó tiene que quedar en el grafo y no sólo en el lienzo, o guardar el grafo que se " +
            "leyó escribiría un formato actual sin los puertos dentro");
        containerDto.Parameters[ISubflowNode.RememberedOutputPortsKey].Should().Be("Errores");
    }

    [Fact]
    public async Task ASavedRepairedFile_ShouldStopDeclaringItselfOlder_AndStopBeingRepaired()
    {
        string file = SubflowFixtures.TempFile();

        try
        {
            var graph = OldFileWithCables();
            var storage = new WorkflowStorageService();

            var editor = new EditorViewModel(CreateLoader());
            editor.LoadFromGraphModel(graph);
            editor.Connections.Should().HaveCount(2, "el archivo anterior todavía conserva sus cables en sus aristas");

            await storage.SaveWorkflowAsync(file, graph);

            var reopened = await storage.LoadWorkflowAsync(file);
            WorkflowFormat.VersionOf(reopened).Should().Be(WorkflowFormat.CurrentVersion,
                "el archivo reparado converge en vez de repararse en cada apertura");

            var reopenedEditor = new EditorViewModel(CreateLoader());
            reopenedEditor.LoadFromGraphModel(reopened);

            reopenedEditor.Connections.Should().HaveCount(2, "lo que se recuperó viajó en el archivo, no se perdió");
            reopenedEditor.Nodes.Single(node => node.Id == "container")
                .InputPorts.Select(port => port.Name).Should().Equal("Alternate");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ASavedRepairedFile_ShouldBeUnchangedByASecondSave()
    {
        string file = SubflowFixtures.TempFile();

        try
        {
            var graph = OldFileWithCables();
            var storage = new WorkflowStorageService();

            new EditorViewModel(CreateLoader()).LoadFromGraphModel(graph);
            await storage.SaveWorkflowAsync(file, graph);
            string repaired = File.ReadAllText(file);

            var reopened = await storage.LoadWorkflowAsync(file);

            storage.SerializeGraph(reopened).Should().Be(repaired,
                "un archivo que ya convergió no cambia al volver a abrirlo y guardarlo: si cambiara, seguiría " +
                "reparándose en cada apertura");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void DeclaringARepair_ShouldNotLowerTheVersionOfANewerFile()
    {
        var newer = new WorkflowGraph { Name = "Posterior", Schema = "FileFlow.Workflow.v3" };

        WorkflowFormat.DeclareRepaired(newer);

        newer.Schema.Should().Be("FileFlow.Workflow.v3",
            "esta versión no repara un archivo posterior, así que no puede declararlo como el suyo");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El archivo anterior tal y como llega de un archivo: un contenedor de subflujo cuya definición ya no
    /// resuelve —así que sus puertos sólo pueden salir de las aristas— y dos cables que los nombran.
    /// </summary>
    private static WorkflowGraph OldFileWithCables()
    {
        var file = new WorkflowGraph { Name = "Antiguo" };
        file.Nodes.Add(Source("src"));
        file.Nodes.Add(UnresolvedContainer("container"));
        file.Nodes.Add(Sink("sink"));
        file.Edges.Add(Edge("src", "Out", "container", "Alternate"));
        file.Edges.Add(Edge("container", "Errores", "sink", "In"));

        return WorkflowFileFixtures.FileFromOlderFormat(file);
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
