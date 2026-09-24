using System.Collections.Generic;
using System.Linq;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.Logic;
using FileFlow.Plugin.Subflows;
using FileFlow.Sdk;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// El validador previo a la ejecución tiene que ver los puertos <b>calculados</b> de un flujo, no los de fábrica.
///
/// <para>Los puertos que no salen del constructor —los casos de un switch, los nombres configurados de un nodo
/// frontera, la frontera de un contenedor de subflujo— sólo existen después de volcar la configuración en la
/// instancia y pedirle su topología. El validador instancia los nodos por su cuenta, así que si no da ese paso
/// compara las aristas contra los puertos genéricos y <b>rechaza un flujo que el lienzo dibujó bien</b>: el
/// usuario ve «Source node 'X' does not have output port 'Case 1'» sobre un cable que él mismo conectó, y el flujo
/// no arranca. Es el cuarto sitio que hace la misma pregunta —el cargador de un flujo, el portapapeles y el
/// diagnóstico previo son los otros tres— y los cuatro tienen que contestarla igual.</para>
/// </summary>
public class GraphValidatorDynamicPortTests
{
    [Fact]
    public void AFlowWiredToASwitchCase_ShouldValidate()
    {
        Validate(CasePortGraph()).IsValid.Should().BeTrue(
            "el puerto 'Case 1' existe en cuanto el nodo lee su `CasesJson`, que el grafo trae configurado");
    }

    [Fact]
    public void AFlowWiredToAConfiguredSubflowPort_ShouldValidate()
    {
        Validate(ConfiguredBoundaryGraph()).IsValid.Should().BeTrue(
            "los nombres de puerto de un nodo frontera salen de su parámetro `PortNames`");
    }

    [Fact]
    public void AFlowWiredToTheBoundaryOfASubflowContainer_ShouldValidate()
    {
        // Éste es el caso que destapó el defecto: la frontera de un contenedor no se lee de un parámetro sino de
        // la definición del subgrafo, y hasta que el validador no materializa el nodo el contenedor expone los
        // genéricos `In`/`Out` y la arista hacia 'Done' se considera un puerto inexistente.
        var result = Validate(ContainerBoundaryGraph());

        result.IsValid.Should().BeTrue(
            "el contenedor declara los puertos de su frontera y la arista del usuario apunta a uno de ellos. " +
            "Errores: " + string.Join(" | ", result.Errors));
    }

    // ─────────────────────────────────────────────────────────────────────────────

    private static ValidationResult Validate(WorkflowGraph graph)
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(SwitchCaseNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);
        loader.RegisterNodeType<ProbeSourceNode>();
        loader.RegisterNodeType<ProbeSinkNode>();

        return new GraphValidator().Validate(graph, loader);
    }

    private static WorkflowGraph TwoEnds() => new()
    {
        Name = "Validación de puertos calculados",
        Nodes =
        {
            new WorkflowNode { Id = "origen", NodeTypeName = nameof(ProbeSourceNode) },
            new WorkflowNode { Id = "espia", NodeTypeName = nameof(ProbeSinkNode) }
        }
    };

    private static WorkflowGraph CasePortGraph()
    {
        var graph = TwoEnds();
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "nodo",
            NodeTypeName = nameof(SwitchCaseNode),
            Parameters = new Dictionary<string, object?>
            {
                ["CasesJson"] = """[{"Name":"Case 1","Pattern":"txt"}]"""
            }
        });

        return Wire(graph, "Case 1");
    }

    private static WorkflowGraph ConfiguredBoundaryGraph()
    {
        var graph = TwoEnds();
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "nodo",
            NodeTypeName = nameof(SubflowInputNode),
            Parameters = new Dictionary<string, object?> { ["PortNames"] = "Entrada" }
        });

        return Wire(graph, "Entrada");
    }

    private static WorkflowGraph ContainerBoundaryGraph()
    {
        var graph = TwoEnds();
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "nodo",
            NodeTypeName = nameof(SubflowNode),
            Parameters = new Dictionary<string, object?>
            {
                ["EmbedDefinition"] = true,
                ["SubflowDefinitionJson"] = DynamicPortResolver.DefinitionJson("In", "Done")
            }
        });

        return Wire(graph, "Done");
    }

    /// <summary>Camino feliz por el puerto calculado: <c>origen → nodo:In → nodo:puertoCalculado → espía</c>.</summary>
    private static WorkflowGraph Wire(WorkflowGraph graph, string computedPort)
    {
        graph.Edges.Add(new WorkflowEdge { SourceNodeId = "origen", SourcePortName = "Out", TargetNodeId = "nodo", TargetPortName = "In" });
        graph.Edges.Add(new WorkflowEdge { SourceNodeId = "nodo", SourcePortName = computedPort, TargetNodeId = "espia", TargetPortName = "In" });

        return graph;
    }

    /// <summary>Origen mínimo: declara una salida y no ejecuta nada.</summary>
    private sealed class ProbeSourceNode : FlowNodeBase
    {
        public override string Name => "Origen de la validación";
        public override string Category => "Testing";
        public override string Description => "Nodo de prueba sin lógica.";

        public ProbeSourceNode() => Outputs = [new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")];

        public override Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, System.Threading.CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>Espía mínimo: declara la entrada por la que recibe el puerto calculado.</summary>
    private sealed class ProbeSinkNode : FlowNodeBase
    {
        public override string Name => "Espía de la validación";
        public override string Category => "Testing";
        public override string Description => "Nodo de prueba sin lógica.";

        public ProbeSinkNode() => Inputs = [new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")];

        public override Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, System.Threading.CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
