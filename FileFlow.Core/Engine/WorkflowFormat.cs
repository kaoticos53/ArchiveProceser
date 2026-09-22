using System.Text.Json;

namespace FileFlow.Core.Engine;

/// <summary>
/// Versión del formato del archivo de flujo —el campo <c>schema</c>, como el paquete del portapapeles— y
/// las reparaciones que necesita un archivo guardado con una versión anterior.
///
/// <para>
/// Un formato que sólo crece en claves se puede leer sin versionarlo, pero <b>no se puede reparar</b>. Hasta
/// esta versión, el guardado escribía únicamente lo que el inspector muestra, y el nodo lleva además estado
/// de diseño que no es un parámetro de usuario —la definición incrustada de un subflujo, los casos de un
/// switch, los puertos que expone un contenedor—, así que un archivo antiguo llega con huecos que nadie
/// puede rellenar. Lo que sí se puede es aprovechar lo que el archivo todavía dice: las aristas nombran los
/// puertos que el contenedor exponía. Eso es lo que distingue a un archivo antiguo de uno actual, y sin
/// versión no habría forma de saber cuál es cuál: los dos se leen igual, y reparar el que no lo necesita es
/// inventar puertos que la definición no declara.
/// </para>
/// </summary>
public static class WorkflowFormat
{
    /// <summary>
    /// La versión que se escribe al guardar. Guarda el estado de diseño del nodo, así que un archivo actual
    /// no necesita reparaciones: lo que dice es lo que hay.
    /// </summary>
    public const string CurrentSchema = "FileFlow.Workflow.v2";

    /// <summary>Número de <see cref="CurrentSchema"/>; los dos tienen que decir lo mismo.</summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// Versión de un archivo que no la declara: todo guardado anterior a que el formato se versionara. Es
    /// un valor distinto de <c>0</c> a propósito, porque es la versión que hay que reparar.
    /// </summary>
    public const int UndeclaredVersion = 1;

    /// <summary>
    /// Declara la versión que este escritor escribe, si el grafo no trae ya una. Es la última cosa que pasa
    /// antes de escribir y la hacen los <b>dos</b> escritores por el mismo sitio: escribir el campo es parte
    /// del formato, no un detalle de quien escribe, y una versión que se olvide en uno de los caminos convierte
    /// un archivo actual en uno al que se le aplican reparaciones pensadas para archivos que ya no se producen.
    ///
    /// No pisa una versión declarada: un grafo leído de un archivo anterior se guarda como lo que era hasta que
    /// alguien lo repare, que es lo que hace que la reparación no se pierda por el camino.
    /// </summary>
    public static void DeclareCurrent(WorkflowGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        graph.Schema ??= CurrentSchema;
    }

    /// <summary>Versión con la que está escrito un grafo.</summary>
    public static int VersionOf(WorkflowGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return VersionOf(graph.Schema);
    }

    /// <summary>
    /// Versión declarada en un valor de <c>schema</c> (<c>FileFlow.Workflow.v2</c> → <c>2</c>). Un valor
    /// ausente o ilegible se interpreta como <see cref="UndeclaredVersion"/>: a un archivo que no dice su
    /// versión no se le puede inventar una, y el formato sin versionar es el único que pudo escribirlo.
    /// </summary>
    public static int VersionOf(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return UndeclaredVersion;
        }

        int lastDot = schema.LastIndexOf('.');
        string tail = lastDot >= 0 ? schema[(lastDot + 1)..] : schema;

        return tail.Length > 1 && (tail[0] is 'v' or 'V')
            && int.TryParse(tail[1..], out int version) && version > 0
                ? version
                : UndeclaredVersion;
    }

    /// <summary>
    /// ¿Lo escribió una versión del formato <b>posterior</b> a la que ésta entiende?
    ///
    /// No es lo mismo que «no necesita reparaciones»: un archivo posterior se lee y se deja tal cual, porque su
    /// versión sabe más que ésta, pero <b>no se puede volver a escribir</b> sin perder lo que esa versión añadió.
    /// Es la pregunta que separa mirar un archivo de sobrescribirlo.
    /// </summary>
    public static bool IsFromNewerFormat(WorkflowGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return VersionOf(graph) > CurrentVersion;
    }

    /// <summary>
    /// Valor de <c>schema</c> que declara la raíz de un archivo de flujo, sin interpretar el resto: se lee del
    /// JSON crudo y no del modelo porque un archivo de un formato posterior es justo el que puede traer formas
    /// que este modelo no sabe enlazar, y hay que poder reconocerlo igual. Acepta el nombre en cualquier caja,
    /// como los lectores. <c>null</c> si no lo declara.
    /// </summary>
    public static string? DeclaredSchema(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals("schema", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    /// <summary>
    /// Lo que hay que reparar al leer un grafo, según la versión con la que se guardó.
    ///
    /// Un archivo que ya declara el formato actual (o uno <b>posterior</b>, escrito por una versión que
    /// sabía más que ésta) no se repara: guarda su propio estado de diseño, así que lo que dicen sus aristas
    /// puede estar desfasado —un cable viejo a un puerto que el nodo ya no expone— y recuperarlo sería
    /// inventar puertos que su definición no declara. La decisión es <b>una sola</b>, y vive aquí: repartida
    /// entre quien pregunta y quien responde, la mitad que se olvide deja pasar una reparación que no toca.
    /// </summary>
    public static WorkflowMigrationPlan Plan(WorkflowGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        return VersionOf(graph) < CurrentVersion
            ? new WorkflowMigrationPlan(PortsNamedByEdges(graph))
            : WorkflowMigrationPlan.Nothing;
    }

    /// <summary>
    /// Nombres de puerto que las aristas del archivo asocian a cada nodo: los que nombra una arista que
    /// entra en él y los que nombra una que sale. Es el rastro que un contenedor deja de los puertos que
    /// exponía —en su momento se conectaron cables a ellos— cuando esos nombres no se guardaron.
    /// </summary>
    private static Dictionary<string, (List<string> Inputs, List<string> Outputs)> PortsNamedByEdges(WorkflowGraph graph)
    {
        var inputs = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var outputs = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in graph.Edges)
        {
            Add(inputs, edge.TargetNodeId, edge.TargetPortName);
            Add(outputs, edge.SourceNodeId, edge.SourcePortName);
        }

        return inputs.Keys
            .Union(outputs.Keys, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                nodeId => nodeId,
                nodeId => (inputs.GetValueOrDefault(nodeId, []), outputs.GetValueOrDefault(nodeId, [])),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Acumula un nombre de puerto para un nodo, sin repetir y sin aceptar nombres vacíos.</summary>
    private static void Add(Dictionary<string, List<string>> portsByNode, string nodeId, string portName)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(portName))
        {
            return;
        }

        if (!portsByNode.TryGetValue(nodeId, out var ports))
        {
            ports = [];
            portsByNode[nodeId] = ports;
        }

        if (!ports.Contains(portName, StringComparer.OrdinalIgnoreCase))
        {
            ports.Add(portName);
        }
    }
}

/// <summary>
/// Reparaciones que necesita un archivo anterior al formato actual, deducidas <b>sólo del propio archivo</b>:
/// a este nivel no hay tipos de nodo, así que el plan dice qué nombres de puerto aparecen y quién lo use
/// decide a qué nodos corresponden.
///
/// <para>
/// El plan no inventa: lo único que un archivo antiguo conserva de la configuración que no guardaba son los
/// <b>nombres de puerto</b>, porque las aristas los citan. Los casos de un switch no guardados o la
/// definición incrustada que se perdió no se recuperan —no hay de dónde—: son datos que el archivo nunca
/// tuvo.
/// </para>
/// </summary>
public sealed class WorkflowMigrationPlan
{
    private static readonly Dictionary<string, (List<string> Inputs, List<string> Outputs)> None =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, (List<string> Inputs, List<string> Outputs)> _portsNamedByEdges;

    internal WorkflowMigrationPlan(Dictionary<string, (List<string> Inputs, List<string> Outputs)> portsNamedByEdges)
    {
        _portsNamedByEdges = portsNamedByEdges;
    }

    /// <summary>Un archivo que no necesita ninguna reparación: no recupera ningún puerto de sus aristas.</summary>
    internal static WorkflowMigrationPlan Nothing { get; } = new(None);

    /// <summary>
    /// Puertos que las aristas del archivo asocian a ese nodo, es decir, lo que ese archivo todavía dice de
    /// los puertos que el nodo exponía. Un archivo que no necesita reparación devuelve una lista vacía para
    /// todos sus nodos, así que recuperar nada es el caso normal y no una excepción que haya que comprobar
    /// aparte.
    ///
    /// Un contenedor de subflujo guardado sin esos nombres es el caso que importa: son justo los que el
    /// editor necesita para emparejar las aristas al reabrir, y sin ellos sus cables desaparecen en
    /// silencio.
    /// </summary>
    public (List<string> Inputs, List<string> Outputs) PortsNamedByEdges(string nodeId) =>
        _portsNamedByEdges.TryGetValue(nodeId, out var ports)
            ? ([.. ports.Inputs], [.. ports.Outputs])
            : ([], []);
}
