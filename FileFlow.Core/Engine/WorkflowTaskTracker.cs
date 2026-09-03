namespace FileFlow.Core.Engine;

/// <summary>
/// Gestor thread-safe para el seguimiento y drenaje determinista de tareas asíncronas de ejecución de nodos.
/// </summary>
public sealed class WorkflowTaskTracker
{
    private readonly Lock _tasksLock = new();
    private readonly List<Task> _activeTasks = [];

    /// <summary>
    /// Registra una tarea asíncrona en el monitor de ejecución.
    /// </summary>
    public void TrackTask(Task task)
    {
        lock (_tasksLock)
        {
            _activeTasks.Add(task);
        }
    }

    /// <summary>
    /// Limpia todas las tareas registradas.
    /// </summary>
    public void Clear()
    {
        lock (_tasksLock)
        {
            _activeTasks.Clear();
        }
    }

    /// <summary>
    /// Espera de forma determinista hasta que todas las tareas activas hayan finalizado,
    /// recolectando cualquier excepción no cancelada en la lista de errores.
    /// </summary>
    public async Task DrainActiveTasksAsync(List<Exception> executionErrors)
    {
        while (true)
        {
            Task[] pending;
            lock (_tasksLock)
            {
                _activeTasks.RemoveAll(t => t.IsCompleted);
                if (_activeTasks.Count == 0) break;
                pending = [.. _activeTasks];
            }

            try
            {
                await Task.WhenAll(pending).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                {
                    executionErrors.Add(ex);
                }
            }
        }
    }
}
