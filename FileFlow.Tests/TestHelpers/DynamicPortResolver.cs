using System;
using System.Collections.Generic;
using System.Linq;
using FileFlow.Core.Engine;
using FileFlow.Plugin.Logic;
using FileFlow.Plugin.Subflows;
using FileFlow.Sdk;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Resuelve <b>en ejecución</b> los puertos de los nodos que no los declaran en el texto: los que se calculan a
/// partir de su configuración (<c>BuildOutputPorts</c>/<c>BuildInputPorts</c>). Es la pieza que convierte a esos
/// nodos de «no se pueden juzgar leyendo el código» en «se juzgan ejecutándolos».
///
/// <para><b>Por qué hace falta</b>: la auditoría de nombres de puerto compara lo declarado con lo emitido
/// leyendo el código, y en estos nodos lo declarado <i>no está en el código</i>: sale de un parámetro (los casos
/// de un switch, los nombres de puerto de un subflujo) o de la definición de un subgrafo. El analizador los
/// aplaza, y un aplazamiento sin prueba es un punto ciego con buena reputación. Aquí se instancia el nodo, se le
/// vuelca la configuración y se le pide su topología con el <b>mismo</b> materializador que usan el cargador de
/// flujos, el portapapeles y el diagnóstico previo a la ejecución (<see cref="DynamicPortMaterializer"/>), así
/// que lo que devuelve es lo que el motor va a ver, no una aproximación.</para>
///
/// <para>Cada nodo aplazado tiene aquí su <see cref="Shape"/>: una configuración representativa, el motivo por el
/// que leerlo no basta y <b>las pruebas que lo ejecutan</b>. La guardia de cobertura de ramas usa las formas para
/// comprobar que ninguno de estos nodos esconde una rama (<c>Error</c>, <c>Skipped</c>, <c>Failed</c>) detrás de
/// un puerto calculado, y la guardia de puertos comprueba que las pruebas citadas existen.</para>
/// </summary>
public static class DynamicPortResolver
{
    /// <summary>
    /// Una configuración representativa de un nodo de puertos calculados, con el motivo de su aplazamiento y las
    /// pruebas que lo ejecutan de verdad.
    /// </summary>
    /// <param name="NodeClass">Nombre de la clase del nodo, que es como lo identifica el analizador.</param>
    /// <param name="Configuration">Por qué sus puertos no se pueden leer del texto.</param>
    /// <param name="CoveredBy">Métodos de prueba que ejecutan el nodo; la guardia comprueba que existen.</param>
    /// <param name="Create">Crea una instancia del nodo, sin configurar.</param>
    /// <param name="Parameters">Configuración que se vuelca antes de materializar sus puertos.</param>
    public sealed record Shape(
        string NodeClass,
        string Configuration,
        IReadOnlyList<string> CoveredBy,
        Func<IFlowNode> Create,
        IReadOnlyDictionary<string, object?> Parameters);

    /// <summary>
    /// Los nodos del producto cuyos puertos se calculan en ejecución, cada uno con una configuración que
    /// <b>ejercita su forma no trivial</b>: un switch con dos casos (su puerto por caso más <c>Default</c>), una
    /// entrada de subflujo con nombres propios (uno de ellos el que emite) y un contenedor con la frontera
    /// renombrada en su definición incrustada.
    /// </summary>
    public static IReadOnlyList<Shape> Shapes { get; } =
    [
        new(
            "SwitchCaseNode",
            "un puerto por caso de `CasesJson` más `Default`: los nombres salen de la configuración, no del código",
            [
                "TheCaseThatMatches_ShouldLeaveByTheCasePortThatTheNodeDeclares",
                "AValueThatMatchesNoCase_ShouldLeaveByTheDeclaredDefaultPort"
            ],
            () => new SwitchCaseNode(),
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CasesJson"] = """[{"Name":"Case 1","Pattern":"txt"},{"Name":"Case 2","Pattern":"png"}]"""
            }),

        new(
            "SubflowInputNode",
            "los nombres de `PortNames`, que el usuario reescribe en tiempo de diseño",
            ["AConfiguredPort_ShouldBeTheOneTheSubflowInputEmits"],
            () => new SubflowInputNode(),
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["PortNames"] = "Entrada;Alterna"
            }),

        new(
            "SubflowNode",
            "los puertos frontera del subgrafo incrustado, que se descubren al resolver la definición",
            ["AContainerWithARenamedBoundary_ShouldEmitByTheDeclaredBoundaryPort"],
            () => new SubflowNode(),
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["EmbedDefinition"] = true,
                ["SubflowDefinitionJson"] = DefinitionJson("In", "Done")
            })
    ];

    /// <summary>
    /// Instancia un nodo, le vuelca la configuración y le pide la topología vigente: el camino que siguen el
    /// cargador de un flujo y el diagnóstico antes de ejecutar.
    /// </summary>
    public static IFlowNode Materialize(Func<IFlowNode> create, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(create);

        IFlowNode instance = create();
        instance.Id = "nodo-de-puertos-calculados";

        foreach ((string key, object? value) in parameters ?? new Dictionary<string, object?>())
        {
            instance.Parameters[key] = value;
        }

        DynamicPortMaterializer.Materialize(instance);
        return instance;
    }

    /// <summary>Nombres de los puertos de salida que el nodo declara con esa configuración.</summary>
    public static IReadOnlyList<string> DeclaredOutputs(Func<IFlowNode> create, IReadOnlyDictionary<string, object?>? parameters = null) =>
        [.. Materialize(create, parameters).Outputs.Select(port => port.Name)];

    /// <summary>Nombres de los puertos de entrada que el nodo declara con esa configuración.</summary>
    public static IReadOnlyList<string> DeclaredInputs(Func<IFlowNode> create, IReadOnlyDictionary<string, object?>? parameters = null) =>
        [.. Materialize(create, parameters).Inputs.Select(port => port.Name)];

    /// <summary>Puertos de salida de una de las formas declaradas, por nombre de clase del nodo.</summary>
    public static IReadOnlyList<string> DeclaredOutputsOf(string nodeClass)
    {
        Shape shape = Shapes.Single(s => string.Equals(s.NodeClass, nodeClass, StringComparison.Ordinal));
        return DeclaredOutputs(shape.Create, shape.Parameters);
    }

    /// <summary>
    /// Definición de subgrafo con una frontera propia: un nodo de entrada y uno de salida, conectados, con los
    /// nombres de puerto que declaran. Es el subgrafo mínimo que expone puertos propios en el contenedor, y lo
    /// comparten la forma declarada del contenedor y la prueba que lo ejecuta (dos definiciones del mismo
    /// subgrafo serían dos sitios donde equivocarse).
    /// </summary>
    public static string DefinitionJson(string inputPort, string outputPort) => new WorkflowGraph
    {
        Name = "Subflujo de la prueba de puertos",
        Nodes =
        {
            Boundary("in", "SubflowInputNode", inputPort),
            Boundary("out", "SubflowOutputNode", outputPort)
        },
        Edges =
        {
            new WorkflowEdge
            {
                SourceNodeId = "in",
                SourcePortName = inputPort,
                TargetNodeId = "out",
                TargetPortName = outputPort
            }
        }
    }.ToJson();

    private static WorkflowNode Boundary(string id, string nodeTypeName, string portNames) => new()
    {
        Id = id,
        NodeTypeName = $"{typeof(SubflowNode).Namespace}.{nodeTypeName}",
        Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PortNames"] = portNames
        }
    };
}
