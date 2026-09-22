using System.Runtime.CompilerServices;
using FileFlow.Sdk;
using FileFlow.Sdk.Common;

namespace FileFlow.Core.Engine;

/// <summary>
/// Resuelve la definición de un subflujo —incrustada en el nodo o en un archivo— y los puertos frontera
/// que expone (<c>SubflowInputNode</c>/<c>SubflowOutputNode</c> y sus <c>PortNames</c>).
///
/// <para>
/// Vive en Core, y no dentro del servicio de ejecución, porque la pregunta «¿qué puertos expone este
/// subflujo?» no es exclusiva del motor: el editor la necesita en <b>tiempo de diseño</b> para que los
/// puertos del contenedor existan antes de reconstruir las conexiones. Cuando el descubrimiento vivía
/// sólo detrás de <see cref="FileFlow.Sdk.Services.ISubflowExecutionService"/>, el editor lo consultaba a
/// través del servicio global, que en el editor es el doble nulo (<c>In</c>/<c>Out</c>) hasta que alguien
/// ejecuta un flujo: los puertos configurados del contenedor no se materializaban y los cables guardados
/// contra ellos se perdían al reabrir.
/// </para>
///
/// <para>
/// El descubrimiento se memoriza por nodo y por <b>huella de su origen</b>: el inspector escribe un
/// parámetro por pulsación de tecla y cada escritura pide reevaluar la topología, así que sin memoria el
/// mismo archivo se leería y se analizaría decenas de veces para responder siempre lo mismo. La huella no
/// exige leer el contenido —lo identifica por su fecha y su tamaño—, de modo que comprobarla es lo que
/// permite saltarse la lectura y el análisis.
/// </para>
/// </summary>
public static class SubflowPortResolver
{
    /// <summary>
    /// Última definición resuelta de cada nodo. La clave es la <b>instancia</b> y la tabla es débil, así
    /// que un nodo descartado —al cerrar un flujo, al deshacer, al reabrir— no deja su caché atrás.
    /// </summary>
    private static readonly ConditionalWeakTable<ISubflowNode, DiscoveryCache> Discoveries = new();

    /// <summary>Puertos genéricos que expone un subflujo sin frontera declarada.</summary>
    private static (List<string> Inputs, List<string> Outputs) GenericPorts() =>
        ([WellKnownPorts.In], [WellKnownPorts.Out]);

    /// <summary>
    /// Resuelve el subgrafo referenciado o incrustado en el nodo, o <c>null</c> cuando la definición no
    /// está disponible (ni incrustada ni en un archivo localizable). No toma ni escribe la caché: es la vía
    /// del motor, que quiere la definición de verdad y no una respuesta ya dada.
    ///
    /// Un JSON incrustado ilegible <b>sí</b> puede lanzar: es la definición que el usuario declaró como
    /// incrustada y el motor prefiere fallar a voces antes que ejecutar otro subgrafo. Quien pregunte por
    /// puertos en tiempo de diseño usa <see cref="Discover(ISubflowNode)"/>, que no lanza.
    /// </summary>
    public static WorkflowGraph? ResolveGraph(ISubflowNode subflowNode)
    {
        ArgumentNullException.ThrowIfNull(subflowNode);

        if (subflowNode.EmbedDefinition && !string.IsNullOrWhiteSpace(subflowNode.SubflowDefinitionJson))
        {
            return WorkflowGraph.FromJson(subflowNode.SubflowDefinitionJson);
        }

        if (!string.IsNullOrWhiteSpace(subflowNode.SubflowDefinitionJson))
        {
            try
            {
                return WorkflowGraph.FromJson(subflowNode.SubflowDefinitionJson);
            }
            catch
            {
                // La definición incrustada está corrupta: se intenta la ruta del archivo, si la hay.
            }
        }

        string? file = ResolveExistingFile(subflowNode);
        if (file != null)
        {
            return WorkflowGraph.FromJson(File.ReadAllText(file));
        }

        return null;
    }

    /// <summary>
    /// ¿El origen del que salen los puertos del nodo es otro del que se descubrió la última vez?
    ///
    /// <para>
    /// Es la mitad barata de <see cref="Discover"/>: compara la huella del origen contra la memorizada y no
    /// lee ni analiza nada, que es justo lo que <see cref="Discover"/> evita pagar cuando se le repite la
    /// misma pregunta. Existe para que quien vigile un contenedor en tiempo de diseño pueda preguntarlo a
    /// menudo —una vez por segundo, por ejemplo— y sólo entonces materialice lo que cambió.
    /// </para>
    ///
    /// <para>
    /// Un nodo del que nunca se descubrieron puertos responde <c>false</c>: no hay nada que refrescar, y lo
    /// que falte lo materializa quien carga el flujo. Un origen que dejó de resolver —el archivo se
    /// movió— <b>sí</b> cuenta como cambio: el contenedor debe pasar a sus puertos recordados en vez de
    /// seguir exponiendo los del archivo que ya no está.
    /// </para>
    /// </summary>
    public static bool HasSourceChanged(ISubflowNode subflowNode)
    {
        ArgumentNullException.ThrowIfNull(subflowNode);

        if (!Discoveries.TryGetValue(subflowNode, out var cache) || cache.Snapshot is not { } known)
        {
            return false;
        }

        return known.Source != SourceIdentity.Of(subflowNode);
    }

    /// <summary>
    /// Puertos frontera que expone el subflujo del nodo.
    ///
    /// Mientras el origen no cambie no se vuelve a leer ni a analizar: se responde con lo que ya se
    /// descubrió. Y cuando la definición <b>no se puede leer</b> —ni incrustada, ni en un archivo
    /// localizable, ni con un JSON que el analizador acepte— responde con los puertos que el propio
    /// contenedor recuerda haber expuesto, que viajan con él en el archivo del flujo, en lugar de caer a
    /// los genéricos <c>In</c>/<c>Out</c>. Eso importa más de lo que parece: el lienzo revalida las
    /// conexiones contra los puertos vigentes, así que un respaldo genérico no es una degradación
    /// inocente, es borrar los cables que el usuario ya había conectado a los puertos propios del subflujo
    /// —y con ellos el trabajo de configurarlos—. Un contenedor que nunca haya expuesto puertos propios no
    /// tiene nada que recordar y su respaldo son los genéricos.
    ///
    /// Un origen ilegible <b>no</b> se memoriza: el fallo puede ser transitorio (un archivo bloqueado, una
    /// ruta todavía a medio escribir) y el siguiente intento debe reintentarlo.
    /// </summary>
    public static (List<string> Inputs, List<string> Outputs) Discover(ISubflowNode subflowNode)
    {
        ArgumentNullException.ThrowIfNull(subflowNode);

        var source = SourceIdentity.Of(subflowNode);
        var cache = Discoveries.GetValue(subflowNode, static _ => new DiscoveryCache());

        var known = cache.Snapshot;
        if (known != null && known.Source == source)
        {
            return PortsOf(known);
        }

        WorkflowGraph? graph;

        try
        {
            graph = ResolveGraph(subflowNode);
        }
        catch
        {
            // La definición está, pero no se puede leer (un archivo bloqueado, un JSON a medio escribir):
            // el contenedor conserva lo que recuerda y el fallo no se memoriza, porque puede ser
            // transitorio.
            return RememberedPorts(subflowNode);
        }

        if (graph == null)
        {
            // No hay definición que leer: ni incrustada ni en un archivo localizable.
            return RememberedPorts(subflowNode);
        }

        var (inputs, outputs) = Discover(graph);
        var discovery = new Discovery(source, inputs, outputs);
        cache.Snapshot = discovery;

        return (inputs, outputs);
    }

    /// <summary>
    /// Copia de los puertos de un descubrimiento: no son de quien pregunta, y un llamante que los tocara
    /// no puede envenenar la respuesta del siguiente.
    /// </summary>
    private static (List<string> Inputs, List<string> Outputs) PortsOf(Discovery discovery) =>
        ([.. discovery.Inputs], [.. discovery.Outputs]);

    /// <summary>
    /// Puertos con los que responde un contenedor cuya definición no está disponible: los que él mismo
    /// recuerda haber expuesto, o los genéricos si nunca expuso puertos propios. La memoria es suya y viaja
    /// en el archivo del flujo, así que cruzó sesiones; aquí sólo se le da forma de puertos.
    /// </summary>
    private static (List<string> Inputs, List<string> Outputs) RememberedPorts(ISubflowNode subflowNode)
    {
        var (inputs, outputs) = subflowNode.RememberedPorts;

        return inputs.Count > 0 || outputs.Count > 0
            ? ([.. inputs], [.. outputs])
            : GenericPorts();
    }

    /// <summary>
    /// Puertos frontera de un subgrafo ya resuelto. Un subgrafo sin nodos —o sin frontera declarada— no
    /// expone puertos propios y la respuesta son los genéricos <c>In</c>/<c>Out</c>, nunca una lista vacía:
    /// un contenedor sin puertos no es conectable y el editor no tendría dónde colgar sus cables.
    /// </summary>
    public static (List<string> Inputs, List<string> Outputs) Discover(WorkflowGraph? graph)
    {
        if (graph == null || graph.Nodes.Count == 0)
        {
            return GenericPorts();
        }

        var inputs = new List<string>();
        var outputs = new List<string>();

        foreach (var node in graph.Nodes)
        {
            if (node.NodeTypeName.Contains("SubflowInputNode", StringComparison.OrdinalIgnoreCase))
            {
                inputs.AddRange(ReadPortNames(node, WellKnownPorts.In));
            }
            else if (node.NodeTypeName.Contains("SubflowOutputNode", StringComparison.OrdinalIgnoreCase))
            {
                outputs.AddRange(ReadPortNames(node, WellKnownPorts.Out));
            }
        }

        if (inputs.Count == 0) inputs.Add(WellKnownPorts.In);
        if (outputs.Count == 0) outputs.Add(WellKnownPorts.Out);

        return (inputs.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                outputs.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>
    /// Materializa en el nodo los puertos que expone su subflujo. Es la operación que necesitan tanto el
    /// editor al reabrir un flujo como el inspector al cambiar la definición del subflujo.
    /// </summary>
    public static void Materialize(ISubflowNode subflowNode)
    {
        ArgumentNullException.ThrowIfNull(subflowNode);

        var (inputs, outputs) = Discover(subflowNode);
        subflowNode.RefreshDynamicPorts(inputs, outputs);
    }

    /// <summary>
    /// Archivo que aporta la definición: la ruta declarada si existe, o su versión relativa al directorio
    /// de la aplicación o a su subcarpeta <c>Subflows</c>. <c>null</c> si no hay ninguno.
    /// </summary>
    private static string? ResolveExistingFile(ISubflowNode subflowNode)
    {
        string path = subflowNode.SubflowPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (File.Exists(path))
        {
            return path;
        }

        string candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        string inSubflowsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Subflows", path);
        return File.Exists(inSubflowsFolder) ? inSubflowsFolder : null;
    }

    /// <summary>
    /// Nombres declarados por un nodo frontera; su respaldo es el puerto genérico de su dirección.
    /// </summary>
    private static IEnumerable<string> ReadPortNames(WorkflowNode node, string fallback)
    {
        if (node.Parameters.TryGetValue("PortNames", out var value) && value != null)
        {
            var parts = value.ToString()!.Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 0)
            {
                return parts;
            }
        }

        return [fallback];
    }

    /// <summary>
    /// Marca del archivo que no exige leerlo: cuándo se escribió y cuánto mide. Es el compromiso habitual
    /// de cualquier construcción incremental —dos versiones distintas con la misma fecha y el mismo tamaño
    /// son indistinguibles— y se acepta a cambio de no abrir el archivo en cada pulsación de tecla.
    /// </summary>
    private static string DescribeFile(string filePath)
    {
        var info = new FileInfo(filePath);
        return $"{info.LastWriteTimeUtc.Ticks}:{info.Length}";
    }

    /// <summary>
    /// Identidad observable del origen del que salen los puertos: el JSON de la definición y, cuando la
    /// definición puede venir de un archivo, cuál se resolvió con su fecha y su tamaño.
    ///
    /// La ruta escrita a mano <b>no</b> forma parte de la identidad, y es deliberado: dos rutas distintas
    /// que no resuelven a ningún archivo exponen los mismos puertos genéricos, así que tratar cada pulsación
    /// de una ruta a medio escribir como un origen nuevo sólo serviría para resolverla una y otra vez sin
    /// que el resultado pueda cambiar.
    /// </summary>
    private readonly record struct SourceIdentity(string Definition, string FilePath, string FileStamp)
    {
        public static SourceIdentity Of(ISubflowNode subflowNode)
        {
            string definition = subflowNode.SubflowDefinitionJson ?? string.Empty;

            // Definición incrustada y presente: es la que decide, así que el archivo no se consulta ni se
            // mira su fecha. (Una definición incrustada vacía o ausente sí deja decidir al archivo.)
            if (subflowNode.EmbedDefinition && definition.Length > 0)
            {
                return new SourceIdentity(definition, string.Empty, string.Empty);
            }

            string? file = ResolveExistingFile(subflowNode);
            return file == null
                ? new SourceIdentity(definition, string.Empty, string.Empty)
                : new SourceIdentity(definition, file, DescribeFile(file));
        }
    }

    /// <summary>
    /// Memoria de un nodo por origen: la huella que se resolvió y los puertos que expuso. Sólo se guardan
    /// descubrimientos en los que la definición <b>se pudo leer</b> —un origen que no resolvió no se
    /// memoriza—, así que el campo jamás contiene una respuesta inventada.
    /// </summary>
    private sealed class DiscoveryCache
    {
        /// <summary>
        /// Volátil porque el nodo puede ejecutarse mientras el editor pregunta por sus puertos: la escritura
        /// es de una referencia completa, así que un lector ve la entrada anterior o la nueva, nunca una a
        /// medias.
        /// </summary>
        public volatile Discovery? Snapshot;
    }

    /// <summary>Descubrimiento memorizado, inmutable: se sustituye entero o no se toca.</summary>
    private sealed record Discovery(SourceIdentity Source, IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs);
}
