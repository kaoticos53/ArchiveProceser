namespace FileFlow.Sdk.Diagnostics;

/// <summary>
/// Ejecutor de <b>tareas descartadas seguras</b>: envuelve un <c>_ = AlgoAsync()</c> de forma que la tarea
/// nunca quede fallida sin observar.
///
/// <para><b>Motivo</b>: una tarea descartada que lanza (<c>_ = AlgoAsync()</c> sin await) no propaga la
/// excepción a nadie. El runtime la entrega después al finalizador y termina en
/// <c>TaskScheduler.UnobservedTaskException</c>, que la aplicación registra en el log de incidentes. Un sondeo
/// que falla en bucle (un servidor local apagado) se convierte así en miles de entradas escritas a disco.</para>
///
/// <para><b>Contrato</b>: la acción se ejecuta y cualquier excepción —incluidas las de la propia condición de
/// error— queda contenida aquí y se entrega al llamador por <paramref name="onError"/> para que la refleje como
/// estado. Nada se filtra al finalizador.</para>
/// </summary>
public static class SafeTaskRunner
{
    /// <summary>Ejecuta <paramref name="action"/> sin que pueda escapar ninguna excepción.</summary>
    /// <param name="action">Acción asíncrona a ejecutar en segundo plano.</param>
    /// <param name="onError">Receptor opcional del error (por ejemplo, para reflejarlo en el estado del ViewModel).</param>
    public static void Run(Func<Task> action, Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        _ = RunCoreAsync(action, onError);
    }

    private static async Task RunCoreAsync(Func<Task> action, Action<Exception>? onError)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (onError is null)
            {
                return;
            }

            try
            {
                onError(ex);
            }
            catch
            {
                // Un manejador de error que falla tampoco puede convertirse en excepción no observada.
            }
        }
    }
}
