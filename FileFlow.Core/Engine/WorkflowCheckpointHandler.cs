using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

/// <summary>
/// Gestor del ciclo de vida y persistencia de checkpoints para reanudación de flujos interrumpidos.
/// </summary>
public sealed class WorkflowCheckpointHandler
{
    private readonly Lock _checkpointLock = new();

    public WorkflowCheckpointData? Checkpoint { get; set; }
    public bool EnableCheckpointing { get; set; } = true;

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
            if (WorkflowCheckpointManager.Instance.HasPendingCheckpoint(workflowName, out var savedCp) && savedCp != null)
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
    /// Registra un archivo completado en el checkpoint y lo persiste atómicamente en disco.
    /// </summary>
    public void RecordCompletedFile(string? originalPath, long doneFilesCount)
    {
        if (Checkpoint == null || string.IsNullOrWhiteSpace(originalPath))
        {
            return;
        }

        lock (_checkpointLock)
        {
            Checkpoint.CompletedFileKeys.Add(originalPath);
            Checkpoint.ProcessedItemsCount = doneFilesCount;
            WorkflowCheckpointManager.Instance.SaveCheckpoint(Checkpoint);
        }
    }

    /// <summary>
    /// Limpia el checkpoint al finalizar exitosamente la ejecución.
    /// </summary>
    public void ClearCheckpoint(string workflowName, bool isDryRun)
    {
        if (EnableCheckpointing && !isDryRun && !string.IsNullOrWhiteSpace(workflowName))
        {
            WorkflowCheckpointManager.Instance.ClearCheckpoint(workflowName);
        }
    }
}
