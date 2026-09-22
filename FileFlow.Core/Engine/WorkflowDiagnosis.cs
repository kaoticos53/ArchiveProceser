using System.Reflection;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Core.Engine;

/// <summary>
/// Gravedad de un hallazgo del diagnóstico.
/// </summary>
public enum DiagnosisSeverity
{
    /// <summary>El flujo puede ejecutarse, pero hay algo que conviene saber <b>antes</b>.</summary>
    Warning,

    /// <summary>El flujo no puede ejecutarse: se corta aquí y no se toca el entorno.</summary>
    Error
}

/// <summary>Qué se encontró. El código es lo que permite contar el hallazgo sin depender del idioma.</summary>
public enum DiagnosisKind
{
    /// <summary>El grafo no tiene ningún nodo: no hay nada que ejecutar.</summary>
    NoNodes,

    /// <summary>Ningún nodo es un origen: no hay quién empiece con trabajo real.</summary>
    NoSource,

    /// <summary>Nodos que el motor arrancará con un elemento vacío porque declaran entradas y nada se las da.</summary>
    StartsEmpty,

    /// <summary>Ningún nodo cierra el flujo: el resultado no llega a ningún destino.</summary>
    DeadEnd,

    /// <summary>Lo que el validador del motor ya rechaza: tipos sin registrar, puertos inexistentes, ciclos.</summary>
    InvalidGraph
}

/// <summary>
/// Un hallazgo del diagnóstico, con su gravedad, su código, la frase ya escrita y los nodos a los que se
/// refiere (que es lo que permitirá señalar el nodo cuando alguien lo cuente, no sólo leerlo).
/// </summary>
public sealed record DiagnosisFinding(
    DiagnosisSeverity Severity,
    DiagnosisKind Kind,
    string Message,
    IReadOnlyList<string> NodeIds);

/// <summary>
/// Qué va a hacer un flujo antes de lanzarlo: si puede ejecutarse, qué arranca, qué recibe un elemento vacío
/// y si el resultado llega a algún sitio.
///
/// <para>
/// Existe porque los dos puntos de entrada respondían lo mismo que un guardia de puerta: «sin nodos, no
/// paso». Las fases 2E-P11 y 2E-P12 cerraron el peor caso —un archivo sin nodos terminaba en verde sin hacer
/// nada— cada una con su comprobación propia, y el resto de la forma del flujo no se miraba en ninguno de los
/// dos: un grafo cuyos nodos no arrancan, o cuyo resultado no llega a ninguna parte, se descubría al
/// ejecutarlo, o no se descubría. Aquí hay <b>una sola</b> regla, en Core, y los dos puntos de entrada la
/// consumen.
/// </para>
///
/// <para>
/// Lo que decide <b>otro</b> no se repite: los tipos sin registrar, los puertos que no existen y los ciclos ya
/// los rechaza <see cref="GraphValidator"/>, y son sus errores los que viajan aquí —con sus palabras— en lugar
/// de una segunda versión de la misma sospecha. Esta clase añade la <b>forma</b>: quién arranca con trabajo y
/// quién con las manos vacías, y si hay alguien al final del camino.
/// </para>
///
/// <para>
/// Y todo lo que no sea «no se puede ejecutar» es un aviso que <b>no bloquea</b>: un diagnóstico que avisa de
/// más es un diagnóstico que se aprende a ignorar.
/// </para>
/// </summary>
public sealed class WorkflowDiagnosis
{
    private WorkflowDiagnosis(
        IReadOnlyList<DiagnosisFinding> findings,
        int nodeCount,
        int connectionCount,
        int startingNodeCount,
        int closingNodeCount)
    {
        Findings = findings;
        Errors = [.. findings.Where(finding => finding.Severity == DiagnosisSeverity.Error)];
        Warnings = [.. findings.Where(finding => finding.Severity == DiagnosisSeverity.Warning)];

        NodeCount = nodeCount;
        ConnectionCount = connectionCount;
        StartingNodeCount = startingNodeCount;
        ClosingNodeCount = closingNodeCount;

        Summary = LocalizationManager.Instance.GetFormattedString(
            "Diagnosis_Summary",
            "🔎 {0} nodo(s) · {1} conexión(es) · {2} nodo(s) de arranque · {3} nodo(s) que cierran el flujo",
            nodeCount,
            connectionCount,
            startingNodeCount,
            closingNodeCount);
    }

    /// <summary>Todo lo que se encontró, errores primero.</summary>
    public IReadOnlyList<DiagnosisFinding> Findings { get; }

    /// <summary>Lo que impide ejecutar.</summary>
    public IReadOnlyList<DiagnosisFinding> Errors { get; }

    /// <summary>Lo que conviene saber antes de ejecutar, sin impedirlo.</summary>
    public IReadOnlyList<DiagnosisFinding> Warnings { get; }

    /// <summary>Si el flujo puede lanzarse.</summary>
    public bool CanRun => Errors.Count == 0;

    /// <summary>
    /// Qué se va a ejecutar, en una línea: nodos, conexiones, cuántos arrancan y cuántos cierran el flujo. Es
    /// la mitad afirmativa del diagnóstico —la que dice qué va a hacer el flujo, no qué le falta—, y sale de
    /// los recuentos de abajo en lugar de contarse aparte.
    /// </summary>
    public string Summary { get; }

    /// <summary>Nodos que el cargador pudo instanciar, que son los que se pudieron mirar.</summary>
    public int NodeCount { get; }

    /// <summary>Conexiones declaradas.</summary>
    public int ConnectionCount { get; }

    /// <summary>Nodos con los que el motor empieza, según la regla del motor.</summary>
    public int StartingNodeCount { get; }

    /// <summary>Nodos que cierran el flujo: los que declaran rol de destino, o la frontera de salida de un subflujo.</summary>
    public int ClosingNodeCount { get; }

    /// <summary>Los errores en una frase, que es lo que los dos puntos de entrada devuelven como motivo.</summary>
    public string ErrorSummary => string.Join(" ", Errors.Select(error => error.Message));

    /// <summary>
    /// Diagnostica un grafo con los nodos que el cargador conoce. El cargador es obligatorio: sin él no se
    /// pueden leer los puertos de los nodos, y los puertos son la mitad de la forma del flujo.
    /// </summary>
    public static WorkflowDiagnosis Analyze(WorkflowGraph graph, PluginLoader loader)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(loader);

        var findings = new List<DiagnosisFinding>();

        // Sin nodos no hay nada que ejecutar, y es un error y no un aviso por la razón que ya se pagó dos
        // veces: terminar en verde sin haber hecho nada es la peor forma de fallar.
        if (graph.Nodes is null || graph.Nodes.Count == 0)
        {
            findings.Add(new DiagnosisFinding(
                DiagnosisSeverity.Error,
                DiagnosisKind.NoNodes,
                LocalizationManager.Instance.GetFormattedString(
                    "Diagnosis_NoNodes",
                    "El flujo no contiene ningún nodo, así que no hay nada que ejecutar."),
                []));

            return new WorkflowDiagnosis(findings, nodeCount: 0, connectionCount: graph.Edges?.Count ?? 0, startingNodeCount: 0, closingNodeCount: 0);
        }

        findings.AddRange(ValidatorErrors(graph, loader));

        var instances = ConfiguredInstances(graph, loader);

        if (instances.Count > 0)
        {
            findings.AddRange(ShapeFindings(graph, instances));
        }

        return new WorkflowDiagnosis(
            findings,
            instances.Count,
            graph.Edges?.Count ?? 0,
            StartNodes(graph, instances).Count,
            instances.Count(instance => IsClosing(instance.Value)));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Lo que decide otro, con sus palabras
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los errores del validador del motor, tal y como los diría una ejecución al fallar. No se traducen ni se
    /// reescriben a propósito: el panel previo y el fallo de la ejecución tienen que decir <b>lo mismo</b>, o
    /// el usuario acaba traduciendo entre dos vocabularios para el mismo problema.
    /// </summary>
    private static IEnumerable<DiagnosisFinding> ValidatorErrors(WorkflowGraph graph, PluginLoader loader) =>
        new GraphValidator()
            .Validate(graph, loader)
            .Errors
            .Select(error => new DiagnosisFinding(DiagnosisSeverity.Error, DiagnosisKind.InvalidGraph, error, []));

    // ─────────────────────────────────────────────────────────────────────────────
    // La forma: quién arranca y quién cierra
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La forma del flujo, contada con el vocabulario que el propio producto ya usa para clasificar sus nodos:
    /// el <see cref="PipelineRole"/> que cada nodo <b>declara</b>. Un origen es un nodo que dice ser un origen
    /// —y los nodos frontera de entrada de un subflujo, que el motor trata como entradas por diseño—; un
    /// cierre es uno que dice ser un destino, o la frontera de salida de un subflujo.
    ///
    /// <para>
    /// Se apoya en el rol y no en los puertos a propósito, y la diferencia se midió: en este catálogo
    /// <b>ningún</b> nodo se queda sin puertos de salida —hasta el sumidero tiene sus salidas <c>Done</c> y
    /// <c>Error</c>—, así que «no hay ningún nodo sin salidas» sería un aviso en <b>todos</b> los flujos de la
    /// aplicación. Un diagnóstico que avisa siempre es un diagnóstico que se ignora.
    /// </para>
    /// </summary>
    private static IEnumerable<DiagnosisFinding> ShapeFindings(
        WorkflowGraph graph,
        IReadOnlyDictionary<string, IFlowNode> instances)
    {
        var findings = new List<DiagnosisFinding>();

        var entries = instances.Where(pair => IsEntry(pair.Value)).Select(pair => pair.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var starts = StartNodes(graph, instances);

        if (entries.Count == 0)
        {
            findings.Add(new DiagnosisFinding(
                DiagnosisSeverity.Warning,
                DiagnosisKind.NoSource,
                LocalizationManager.Instance.GetFormattedString(
                    "Diagnosis_NoSource",
                    "Ninguno de los {0} nodos es un origen, así que nada ingiere datos: el motor arrancará el flujo con un elemento vacío.",
                    instances.Count),
                [.. instances.Keys]));
        }
        else
        {
            // Nodos que el motor va a arrancar con un elemento vacío: no son una entrada y declaran puertos de
            // entrada, así que lo que reciben es un FileItemContext vacío. Es el nodo que quedó sin conectar, y
            // se cuenta <b>sólo</b> cuando el flujo tiene entradas de verdad: si no las tiene, eso ya lo dice el
            // hallazgo de arriba, y repetirlo nodo a nodo sería decir lo mismo dos veces.
            var forgotten = starts
                .Where(pair => !entries.Contains(pair.Key) && pair.Value.Inputs.Count > 0)
                .ToList();

            if (forgotten.Count > 0)
            {
                findings.Add(new DiagnosisFinding(
                    DiagnosisSeverity.Warning,
                    DiagnosisKind.StartsEmpty,
                    LocalizationManager.Instance.GetFormattedString(
                        "Diagnosis_StartsEmpty",
                        "El motor arrancará {0} nodo(s) con un elemento vacío, porque declaran entradas y nada se las da: {1}.",
                        forgotten.Count,
                        NameList(instances, forgotten.Select(pair => pair.Key))),
                    [.. forgotten.Select(pair => pair.Key)]));
            }
        }

        if (!instances.Values.Any(IsClosing))
        {
            findings.Add(new DiagnosisFinding(
                DiagnosisSeverity.Warning,
                DiagnosisKind.DeadEnd,
                LocalizationManager.Instance.GetFormattedString(
                    "Diagnosis_DeadEnd",
                    "Ningún nodo cierra el flujo, así que el resultado de los últimos nodos no llega a ningún destino."),
                []));
        }

        return findings;
    }

    /// <summary>Un nodo que empieza el flujo con trabajo propio: el que declara el rol de origen, o la frontera de entrada de un subflujo.</summary>
    private static bool IsEntry(IFlowNode node) =>
        RoleOf(node) == PipelineRole.Source || IsBoundaryInput(node);

    /// <summary>Un nodo que cierra el flujo: el que declara el rol de destino, o la frontera de salida de un subflujo.</summary>
    private static bool IsClosing(IFlowNode node) =>
        RoleOf(node) == PipelineRole.Sink || IsBoundaryOutput(node);

    /// <summary>
    /// El rol que el nodo declara en sus metadatos, que es como el catálogo ya lo clasifica. Un nodo sin
    /// metadatos —un doble de prueba, por ejemplo— es un transformador, que es el papel por defecto de la casa.
    /// </summary>
    private static PipelineRole RoleOf(IFlowNode node) =>
        node.GetType().GetCustomAttribute<NodeDefinitionAttribute>()?.Role ?? PipelineRole.Transform;

    /// <summary>
    /// Los nodos con los que el motor empieza, según la regla del propio motor: los que no reciben ninguna
    /// arista y, si el grafo es un subflujo, sus nodos frontera de entrada. Repetir esta regla aquí —en vez de
    /// inventar una parecida— es lo que hace que el diagnóstico hable del flujo de verdad.
    /// </summary>
    private static List<KeyValuePair<string, IFlowNode>> StartNodes(
        WorkflowGraph graph,
        IReadOnlyDictionary<string, IFlowNode> instances)
    {
        var withIncomingEdge = (graph.Edges ?? []).Select(edge => edge.TargetNodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var boundaryInputs = instances
            .Where(pair => IsBoundaryInput(instances[pair.Key]))
            .ToList();

        if (boundaryInputs.Count > 0)
        {
            return boundaryInputs;
        }

        return [.. instances.Where(pair => !withIncomingEdge.Contains(pair.Key))];
    }

    private static bool IsBoundaryInput(IFlowNode node) =>
        node.GetType().Name.Contains("SubflowInputNode", StringComparison.OrdinalIgnoreCase);

    private static bool IsBoundaryOutput(IFlowNode node) =>
        node.GetType().Name.Contains("SubflowOutputNode", StringComparison.OrdinalIgnoreCase);

    // ─────────────────────────────────────────────────────────────────────────────
    // Instancias, que es donde viven los puertos
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El nodo tal y como lo vería el motor: instanciado, con sus parámetros puestos y sus puertos
    /// materializados —los de un contenedor de subflujo salen de su definición, no del constructor—. Un tipo
    /// que el cargador no conoce no tiene puertos que leer, y de eso ya avisa el validador.
    /// </summary>
    private static Dictionary<string, IFlowNode> ConfiguredInstances(WorkflowGraph graph, PluginLoader loader)
    {
        var instances = new Dictionary<string, IFlowNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Nodes)
        {
            if (loader.CreateNodeInstance(node.NodeTypeName) is not { } instance)
            {
                continue;
            }

            instance.Id = node.Id;
            instance.Parameters.Clear();
            foreach (var (key, value) in node.Parameters)
            {
                instance.Parameters[key] = value;
            }

            DynamicPortMaterializer.Materialize(instance);

            instances[node.Id] = instance;
        }

        return instances;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Cómo se dice
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los nombres con los que el usuario reconoce a esos nodos: el título que les puso, porque es lo que ve
    /// en el lienzo. Es la misma regla que usa el informe de las conexiones que no se pudieron reconstruir.
    /// </summary>
    private static string NameList(IReadOnlyDictionary<string, IFlowNode> instances, IEnumerable<string> nodeIds) =>
        string.Join(", ", nodeIds.Select(id => instances.TryGetValue(id, out var node) ? node.Name : id));

}
