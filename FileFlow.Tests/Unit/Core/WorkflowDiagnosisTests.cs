using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Scripting;
using FileFlow.Plugin.Subflows;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// El diagnóstico de un flujo antes de lanzarlo: si puede ejecutarse, qué arranca, qué recibe un elemento
/// vacío y si el resultado llega a algún sitio.
///
/// Hasta aquí los dos puntos de entrada sólo sabían decir «sin nodos» —cada uno con su comprobación propia,
/// una en el CLI y otra en la interfaz— y el resto de la forma del flujo se descubría al ejecutarlo, o no se
/// descubría. Estas pruebas fijan la tabla grafos → hallazgos, que es la única regla que los dos consumen.
///
/// Lo que <b>no</b> dicen los hallazgos también se fija aquí: un grafo sano no tiene ninguno, un subflujo
/// —que se alimenta de fuera por diseño— no es un flujo sin origen, y un aviso que saldría en todos los
/// flujos de la aplicación no es un aviso.
/// </summary>
public class WorkflowDiagnosisTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Lo que impide ejecutar
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AGraphWithoutNodes_ShouldBeAnErrorThatSaysThereIsNothingToRun()
    {
        var diagnosis = Analyze(new WorkflowGraph { Name = "Vacío" });

        diagnosis.CanRun.Should().BeFalse("un flujo sin nodos no tiene nada que hacer");
        diagnosis.Errors.Should().ContainSingle().Which.Kind.Should().Be(DiagnosisKind.NoNodes);
        diagnosis.ErrorSummary.Should().Contain("ningún nodo",
            "es la frase que los dos puntos de entrada ya le decían al usuario por su cuenta");
        diagnosis.NodeCount.Should().Be(0);
    }

    [Fact]
    public void ANodeTypeTheLoaderDoesNotKnow_ShouldBeAnErrorWithTheValidatorsOwnWords()
    {
        var graph = TwoNodesWithAnEdge();
        graph.Nodes.Add(new WorkflowNode { Id = "fantasma", NodeTypeName = "FileFlow.Plugin.Que.No.Existe.Nodo" });

        var diagnosis = Analyze(graph);

        diagnosis.CanRun.Should().BeFalse();
        diagnosis.Errors.Should().ContainSingle().Which.Kind.Should().Be(DiagnosisKind.InvalidGraph);
        diagnosis.ErrorSummary.Should().Contain("FileFlow.Plugin.Que.No.Existe.Nodo",
            "el mensaje es el del validador del motor, que es quien decide los tipos: el panel previo y el fallo de la ejecución dicen lo mismo");
    }

    [Fact]
    public void AnEdgeThatNamesAPortNobodyExposes_ShouldBeAnError()
    {
        var graph = TwoNodesWithAnEdge();
        graph.Edges[0].TargetPortName = "UnPuertoQueNoExiste";

        var diagnosis = Analyze(graph);

        diagnosis.CanRun.Should().BeFalse();
        diagnosis.Errors.Should().ContainSingle().Which.Kind.Should().Be(DiagnosisKind.InvalidGraph);
        diagnosis.ErrorSummary.Should().Contain("UnPuertoQueNoExiste");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El flujo sano, que no tiene nada que contar
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AFlowFromASourceToASink_ShouldHaveNothingToReport()
    {
        var diagnosis = Analyze(TwoNodesWithAnEdge());

        diagnosis.Findings.Should().BeEmpty("hay origen, hay destino y los cables llegan: no hay nada que decir");
        diagnosis.CanRun.Should().BeTrue();
        diagnosis.NodeCount.Should().Be(2);
        diagnosis.ConnectionCount.Should().Be(1);
        diagnosis.StartingNodeCount.Should().Be(1, "el motor arranca el nodo que no recibe ninguna arista");
        diagnosis.ClosingNodeCount.Should().Be(1);
        diagnosis.Summary.Should().Contain("2").And.Contain("1");
    }

    [Fact]
    public void ASubflowDefinition_ShouldNotBeToldItHasNoSourceOrNoDestination()
    {
        // Un subflujo no se alimenta de sí mismo: se alimenta de fuera, y el motor lo sabe —trata sus nodos
        // frontera como el arranque y el cierre por diseño—. Un diagnóstico que le exigiera un origen sería un
        // aviso en todos los subflujos de la aplicación.
        var graph = new WorkflowGraph { Name = "Subflujo" };
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "entrada",
            NodeTypeName = typeof(SubflowInputNode).FullName!,
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "In" }
        });
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "salida",
            NodeTypeName = typeof(SubflowOutputNode).FullName!,
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "Out" }
        });
        graph.Edges.Add(Edge("entrada", "In", "salida", "Out"));

        var diagnosis = Analyze(graph);

        diagnosis.Findings.Should().BeEmpty();
        diagnosis.StartingNodeCount.Should().Be(1);
        diagnosis.ClosingNodeCount.Should().Be(1, "la frontera de salida es el cierre del subflujo");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La forma: quién arranca con las manos vacías y quién cierra
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ANodeLeftUnconnected_ShouldBeNamedAsOneThatStartsWithAnEmptyItem()
    {
        var graph = TwoNodesWithAnEdge();
        graph.Nodes.Add(new WorkflowNode { Id = "suelto", NodeTypeName = typeof(CustomScriptNode).FullName! });

        var diagnosis = Analyze(graph);

        var finding = diagnosis.Findings.Should().ContainSingle().Subject;
        finding.Kind.Should().Be(DiagnosisKind.StartsEmpty);
        finding.Severity.Should().Be(DiagnosisSeverity.Warning, "el flujo se puede ejecutar: esto es algo que hay que saber");
        finding.NodeIds.Should().Equal(["suelto"], "se nombra el nodo que quedó sin conectar, no el flujo entero");
        finding.Message.Should().Contain("Script", "el nombre con el que el usuario reconoce al nodo");
        diagnosis.CanRun.Should().BeTrue();
    }

    [Fact]
    public void AFlowWhereNoNodeIsASource_ShouldWarnThatNothingIngestsData()
    {
        var graph = new WorkflowGraph { Name = "Sin origen" };
        graph.Nodes.Add(new WorkflowNode { Id = "script", NodeTypeName = typeof(CustomScriptNode).FullName! });
        graph.Nodes.Add(new WorkflowNode { Id = "destino", NodeTypeName = typeof(DestinationSinkNode).FullName! });
        graph.Edges.Add(Edge("script", "Out", "destino", "In"));

        var diagnosis = Analyze(graph);

        var finding = diagnosis.Findings.Should().ContainSingle().Subject;
        finding.Kind.Should().Be(DiagnosisKind.NoSource);
        finding.Severity.Should().Be(DiagnosisSeverity.Warning);
        finding.NodeIds.Should().HaveCount(2, "el hallazgo habla del flujo: ninguno de sus nodos es un origen");
        diagnosis.CanRun.Should().BeTrue("nada ingiere datos, pero el motor puede arrancarlo igualmente");
    }

    [Fact]
    public void AFlowThatWritesNowhere_ShouldWarnThatTheResultReachesNoDestination()
    {
        var graph = new WorkflowGraph { Name = "Sin destino" };
        graph.Nodes.Add(new WorkflowNode { Id = "origen", NodeTypeName = typeof(FolderSourceNode).FullName! });
        graph.Nodes.Add(new WorkflowNode { Id = "script", NodeTypeName = typeof(CustomScriptNode).FullName! });
        graph.Edges.Add(Edge("origen", "Out", "script", "In"));

        var diagnosis = Analyze(graph);

        diagnosis.Findings.Should().ContainSingle().Which.Kind.Should().Be(DiagnosisKind.DeadEnd);
        diagnosis.ClosingNodeCount.Should().Be(0);
        diagnosis.CanRun.Should().BeTrue("un flujo que no escribe en ninguna parte se ejecuta: lo que no hace es escribir");
    }

    /// <summary>
    /// La mitad que hace útil el aviso: <b>ningún</b> flujo del catálogo se queda sin puertos de salida —hasta
    /// el sumidero tiene sus salidas <c>Done</c> y <c>Error</c>—, así que un diagnóstico que buscara el cierre
    /// en los puertos avisaría en todos los flujos. El cierre se busca en el rol que cada nodo declara.
    /// </summary>
    [Fact]
    public void TheClosingNode_ShouldBeFoundByItsDeclaredRoleAndNotByItsPorts()
    {
        var sink = new DestinationSinkNode();

        sink.Outputs.Should().NotBeEmpty("el sumidero del catálogo declara salidas: buscarlo por puertos no lo encontraría nunca");

        var diagnosis = Analyze(TwoNodesWithAnEdge());

        diagnosis.ClosingNodeCount.Should().Be(1, "y sin embargo es el cierre del flujo, porque lo declara su rol");
        diagnosis.Findings.Should().NotContain(finding => finding.Kind == DiagnosisKind.DeadEnd);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Origen hasta destino, con sus puertos y su cable: el flujo mínimo que se ejecuta y hace algo.</summary>
    private static WorkflowGraph TwoNodesWithAnEdge()
    {
        var graph = new WorkflowGraph { Name = "Origen a destino" };
        graph.Nodes.Add(new WorkflowNode { Id = "origen", NodeTypeName = typeof(FolderSourceNode).FullName! });
        graph.Nodes.Add(new WorkflowNode { Id = "destino", NodeTypeName = typeof(DestinationSinkNode).FullName! });
        graph.Edges.Add(Edge("origen", "Out", "destino", "In"));
        return graph;
    }

    private static WorkflowEdge Edge(string sourceId, string sourcePort, string targetId, string targetPort) => new()
    {
        SourceNodeId = sourceId,
        SourcePortName = sourcePort,
        TargetNodeId = targetId,
        TargetPortName = targetPort
    };

    /// <summary>
    /// El diagnóstico, con el cargador que conoce los nodos que usan estas pruebas: el mismo camino que
    /// recorren el CLI y la interfaz, sin nada específico para probar.
    /// </summary>
    private static WorkflowDiagnosis Analyze(WorkflowGraph graph)
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(CustomScriptNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowInputNode).Assembly);

        return WorkflowDiagnosis.Analyze(graph, loader);
    }
}
