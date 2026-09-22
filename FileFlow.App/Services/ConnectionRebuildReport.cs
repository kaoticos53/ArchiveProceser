namespace FileFlow.App.Services;

/// <summary>
/// Lo que quedó por reconstruir al volver a armar las conexiones de un grafo.
///
/// Hay <b>dos</b> caminos que reconstruyen conexiones —abrir un flujo guardado y pegar o duplicar nodos— y
/// los dos las emparejan igual, por <b>nombre de puerto</b> (ver <see cref="ConnectionReconstructor"/>), así
/// que los dos tienen el mismo agujero: una arista cuyo puerto ya no existe —o cuyo nodo no llegó a crearse,
/// porque su plugin no está registrado— no encuentra dónde conectarse. Hasta 2E-P9 se descartaba sin
/// decírselo a nadie: el grafo parecía completo y ya no lo estaba, y el usuario lo descubría al ejecutarlo,
/// o no lo descubría. Este informe es lo que permite contarlo, con quién era cada extremo y por qué no se
/// pudo reconstruir.
///
/// Que lo produzcan <b>los dos</b> caminos es deliberado: es la misma pérdida, y merece la misma cuenta
/// aunque se cuente en sitios distintos —abrir un archivo se cuenta en la consola, pegar se cuenta donde
/// está la acción, el lienzo—.
///
/// No todos los cables que se descartan son un problema del archivo: la migración de un formato anterior
/// recupera los puertos que sí se pueden recuperar, así que un flujo antiguo reparado llega aquí completo.
/// Si aun así falta un cable, es que no había de dónde sacarlo.
/// </summary>
public sealed class ConnectionRebuildReport
{
    /// <summary>Un grafo que se reconstruyó entero.</summary>
    public static ConnectionRebuildReport Complete { get; } = new([]);

    internal ConnectionRebuildReport(IReadOnlyList<DroppedConnection> droppedConnections)
    {
        DroppedConnections = droppedConnections;
    }

    /// <summary>
    /// Conexiones que el origen declaraba y no se pudieron reconstruir, en el orden en que aparecen en él.
    /// </summary>
    public IReadOnlyList<DroppedConnection> DroppedConnections { get; }

    /// <summary>Si el grafo volvió entero al lienzo.</summary>
    public bool IsComplete => DroppedConnections.Count == 0;
}

/// <summary>Una conexión que no se pudo reconstruir, con sus dos extremos.</summary>
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
/// Un extremo de una conexión: el nodo y el puerto que el origen nombraba, y el diagnóstico de por qué no
/// se pudo reconstruir por su culpa.
/// </summary>
/// <param name="NodeId">Identificador del nodo en el origen, que es lo único que siempre está.</param>
/// <param name="NodeName">
/// Nombre con el que reconocerlo: el título que el usuario le puso, o el tipo del nodo —que es lo que hay
/// que buscar en la caja de herramientas—, o su identificador si el origen ni eso dice.
/// </param>
/// <param name="PortName">Puerto que el origen conectaba en ese extremo.</param>
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
