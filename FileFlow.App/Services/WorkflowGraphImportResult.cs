namespace FileFlow.App.Services;

/// <summary>
/// Lo que quedó por reconstruir al abrir un flujo.
///
/// Las conexiones de un flujo se reconstruyen emparejando <b>nombres de puerto</b>, así que una arista cuyo
/// puerto ya no existe —o cuyo nodo no llegó a crearse, porque su plugin no está registrado— no encuentra
/// dónde conectarse. Hasta ahora se descartaba sin decírselo a nadie: el flujo reabierto parecía completo y
/// ya no lo estaba, y el usuario lo descubría al ejecutarlo, o no lo descubría. Este resultado es lo que
/// permite contarlo, con quién era cada extremo y por qué no se pudo reconstruir.
///
/// No todos los cables que se descartan son un problema del archivo: la migración de un formato anterior
/// recupera los puertos que sí se pueden recuperar, así que un flujo antiguo reparado llega aquí completo.
/// Si aun así falta un cable, es que no había de dónde sacarlo.
/// </summary>
public sealed class WorkflowGraphImportResult
{
    /// <summary>Un flujo que se reconstruyó entero.</summary>
    public static WorkflowGraphImportResult Complete { get; } = new([]);

    internal WorkflowGraphImportResult(IReadOnlyList<DroppedConnection> droppedConnections)
    {
        DroppedConnections = droppedConnections;
    }

    /// <summary>
    /// Conexiones que el archivo declaraba y no se pudieron reconstruir, en el orden en que aparecen en él.
    /// </summary>
    public IReadOnlyList<DroppedConnection> DroppedConnections { get; }

    /// <summary>Si el flujo volvió entero al lienzo.</summary>
    public bool IsComplete => DroppedConnections.Count == 0;
}

/// <summary>Una conexión del archivo que no se pudo reconstruir, con sus dos extremos.</summary>
public sealed record DroppedConnection(DroppedConnectionEnd Source, DroppedConnectionEnd Target)
{
    /// <summary>
    /// Extremos que impidieron reconstruirla: normalmente uno —el puerto que ya no se expone, o el nodo que
    /// no está— y los dos sólo cuando la arista nombra dos nodos que no se pudieron crear. Nunca está vacío:
    /// un cable que se descarta siempre tiene un extremo que no existe.
    /// </summary>
    public IReadOnlyList<DroppedConnectionEnd> Impediments =>
        [.. new[] { Source, Target }.Where(end => end.Problem != DroppedConnectionEndProblem.None)];
}

/// <summary>
/// Un extremo de una conexión: el nodo y el puerto que el archivo nombraba, y el diagnóstico de por qué no
/// se pudo reconstruir por su culpa.
/// </summary>
/// <param name="NodeId">Identificador del nodo en el archivo, que es lo único que siempre está.</param>
/// <param name="NodeName">
/// Nombre con el que reconocerlo: el título que el usuario le puso, o el tipo del nodo —que es lo que hay
/// que buscar en la caja de herramientas—, o su identificador si el archivo ni eso dice.
/// </param>
/// <param name="PortName">Puerto que el archivo conectaba en ese extremo.</param>
/// <param name="Problem">Qué falló en este extremo, si es que falló algo.</param>
public sealed record DroppedConnectionEnd(
    string NodeId,
    string NodeName,
    string PortName,
    DroppedConnectionEndProblem Problem);

/// <summary>Qué le pasa a un extremo de una conexión que no se pudo reconstruir.</summary>
public enum DroppedConnectionEndProblem
{
    /// <summary>El nodo existe y expone el puerto: este extremo no impidió nada.</summary>
    None,

    /// <summary>
    /// El nodo no llegó a crearse: su tipo no está registrado —falta el plugin que lo aporta— o ya no existe
    /// en esta versión.
    /// </summary>
    MissingNode,

    /// <summary>El nodo existe, pero no expone ese puerto: se renombró, o su definición cambió.</summary>
    MissingPort
}
