using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

/// <summary>
/// Gestor del ciclo de vida y persistencia de checkpoints para reanudación de flujos interrumpidos.
///
/// <para><b>El defecto, medido.</b> Cada archivo completado persistía el conjunto <b>entero</b> de claves
/// —serializado con sangría y escrito a disco de una vez— bajo el candado del punto de control. Con N archivos
/// eso son N escrituras de un conjunto que crece hasta N claves: O(N²) en bytes escritos y un <b>punto de
/// serialización por ítem</b>, justo en el camino que reparte el trabajo entre hilos (medido con 2 000
/// archivos: ver <c>CheckpointWriteCostTests</c>).</para>
///
/// <para><b>La cura.</b> El ítem completado sólo anota su clave; el conjunto se persiste cuando se han
/// acumulado <see cref="FilesPerCheckpointWrite"/> claves nuevas, y la escritura se hace <b>fuera del
/// candado</b> sobre una copia tomada dentro de él. Lo que se pierde en una caída seca son, como mucho, esos
/// archivos —que la reanudación vuelve a procesar—; lo que se gana es que el coste de escribir el punto de
/// control deja de crecer con el cuadrado de los archivos.</para>
///
/// <para>La escritura es <b>sincrónica</b> a propósito, aunque sea cada muchos ítems: escrituras en segundo
/// plano pueden llegar después de que el flujo termine y <b>resucitar</b> el fichero que el final acaba de
/// borrar.</para>
/// </summary>
public sealed class WorkflowCheckpointHandler
{
    private readonly Lock _checkpointLock = new();

    /// <summary>Archivos completados desde la última vez que se persistió el conjunto.</summary>
    private int _filesSinceLastSave;

    public WorkflowCheckpointData? Checkpoint { get; set; }
    public bool EnableCheckpointing { get; set; } = true;

    /// <summary>
    /// Cuántos archivos completados se acumulan antes de volver a persistir el conjunto completo. <c>1</c> es el
    /// comportamiento anterior —una escritura por archivo, que es el defecto que esto arregla— y se conserva
    /// para poder medirlo contra el arreglo.
    /// </summary>
    public int FilesPerCheckpointWrite { get; set; } = DefaultFilesPerCheckpointWrite;

    /// <summary>
    /// Cuántas claves nuevas se acumulan por defecto antes de reescribir el punto de control. Es un compromiso:
    /// bajo (más escrituras, menos trabajo repetido tras una caída) contra alto (menos escrituras, más trabajo
    /// repetido). Con 256, un flujo de 100 000 archivos escribe unas 390 veces en lugar de 100 000.
    /// </summary>
    public const int DefaultFilesPerCheckpointWrite = 256;

    /// <summary>
    /// Dónde se persiste. Se puede sustituir para escribir en otro sitio (las pruebas y las mediciones usan un
    /// directorio temporal en lugar del perfil real del usuario).
    /// </summary>
    public WorkflowCheckpointManager Manager { get; set; } = WorkflowCheckpointManager.Instance;

    /// <summary>
    /// Inicializa o recupera un checkpoint previo para el flujo de trabajo si está habilitado.
    /// </summary>
    public void InitializeCheckpoint(string workflowName, string executionId, bool isDryRun, Action<string, LogLevel> notifyLog)
    {
        if (!EnableCheckpointing || isDryRun || string.IsNullOrWhiteSpace(workflowName))
        {
            return;
        }

        if (Checkpoint == null)
        {
            if (Manager.HasPendingCheckpoint(workflowName, out var savedCp) && savedCp != null)
            {
                Checkpoint = savedCp;
                notifyLog($"[Checkpoint] Reanudando ejecución previa para '{workflowName}' ({Checkpoint.CompletedFileKeys.Count} archivos ya completados).", LogLevel.Information);
            }
            else
            {
                Checkpoint = new WorkflowCheckpointData
                {
                    WorkflowName = workflowName,
                    ExecutionId = executionId
                };
            }
        }
    }

    /// <summary>
    /// Comprueba si un archivo ya fue completado en una ejecución anterior.
    /// </summary>
    public bool IsFileAlreadyCompleted(string? originalPath)
    {
        if (Checkpoint == null || string.IsNullOrWhiteSpace(originalPath))
        {
            return false;
        }

        lock (_checkpointLock)
        {
            return Checkpoint.CompletedFileKeys.Contains(originalPath);
        }
    }

    /// <summary>
    /// Registra un archivo completado. La clave se anota siempre; el conjunto se persiste cada
    /// <see cref="FilesPerCheckpointWrite"/> archivos, no una vez por archivo.
    /// </summary>
    public void RecordCompletedFile(string? originalPath, long doneFilesCount)
    {
        if (Checkpoint == null || string.IsNullOrWhiteSpace(originalPath))
        {
            return;
        }

        WorkflowCheckpointData? snapshot = null;
        lock (_checkpointLock)
        {
            Checkpoint.CompletedFileKeys.Add(originalPath);
            Checkpoint.ProcessedItemsCount = doneFilesCount;

            if (++_filesSinceLastSave >= Math.Max(1, FilesPerCheckpointWrite))
            {
                _filesSinceLastSave = 0;
                snapshot = CreateSnapshot();
            }
        }

        // Fuera del candado: mientras un hilo serializa y escribe, los demás ítems siguen anotando el suyo. El
        // punto de serialización por ítem era la mitad del defecto.
        if (snapshot != null)
        {
            Manager.SaveCheckpoint(snapshot);
        }
    }

    /// <summary>
    /// Persiste ya lo que quede pendiente. Lo llama el motor al cerrar la ejecución —así una ejecución
    /// interrumpida deja en disco lo completado hasta el último archivo— y quien necesite preguntar por el
    /// estado en disco.
    /// </summary>
    public void FlushPendingSaves()
    {
        WorkflowCheckpointData? snapshot;
        lock (_checkpointLock)
        {
            if (Checkpoint == null || _filesSinceLastSave == 0)
            {
                return;
            }

            _filesSinceLastSave = 0;
            snapshot = CreateSnapshot();
        }

        Manager.SaveCheckpoint(snapshot);
    }

    /// <summary>
    /// Limpia el checkpoint al finalizar exitosamente la ejecución.
    ///
    /// <para><b>Limpia las dos mitades.</b> Borra el fichero <b>y olvida el estado en memoria</b>. Olvidar sólo
    /// el fichero es el defecto que esto arregla: el objeto sobrevivía a la ejecución, y como
    /// <see cref="InitializeCheckpoint"/> sólo crea uno nuevo si no hay ninguno, la <b>siguiente ejecución del
    /// mismo motor</b> —el botón Ejecutar pulsado por segunda vez— se encontraba con las claves de la anterior,
    /// daba cada archivo por ya completado y terminaba <b>en verde sin hacer nada</b>.</para>
    ///
    /// <para>También olvida lo que quedaba pendiente de persistir: sin eso, el volcado de cierre volvería a
    /// escribir el fichero que se acaba de borrar.</para>
    ///
    /// <para>Una ejecución que <b>no</b> termina bien no pasa por aquí, así que su estado sobrevive y la
    /// siguiente ejecución reanuda donde se quedó: eso es lo que el punto de control existe para hacer.</para>
    /// </summary>
    public void ClearCheckpoint(string workflowName, bool isDryRun)
    {
        lock (_checkpointLock)
        {
            Checkpoint = null;
            _filesSinceLastSave = 0;
        }

        if (EnableCheckpointing && !isDryRun && !string.IsNullOrWhiteSpace(workflowName))
        {
            Manager.ClearCheckpoint(workflowName);
        }
    }

    /// <summary>
    /// Copia del estado, para escribirla sin tener que sostener el candado durante la serialización. Copiar las
    /// claves es barato (referencias); serializarlas y escribirlas no.
    /// </summary>
    private WorkflowCheckpointData CreateSnapshot() => new()
    {
        ExecutionId = Checkpoint!.ExecutionId,
        WorkflowName = Checkpoint.WorkflowName,
        Timestamp = Checkpoint.Timestamp,
        ProcessedItemsCount = Checkpoint.ProcessedItemsCount,
        CompletedFileKeys = new HashSet<string>(Checkpoint.CompletedFileKeys, StringComparer.OrdinalIgnoreCase)
    };
}
