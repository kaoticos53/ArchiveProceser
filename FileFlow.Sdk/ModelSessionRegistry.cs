namespace FileFlow.Sdk;

/// <summary>
/// Registro transversal y desacoplado para coordinar el ciclo de vida y métricas de sesiones de modelos pesados (IA, ONNX, Audio).
/// Permite que la capa de UI consulte y libere memoria sin acoplarse directamente a ningún plugin específico.
/// </summary>
public static class ModelSessionRegistry
{
    private static readonly List<Func<int>> _sessionCountProviders = [];
    private static readonly List<Action> _sessionClearActions = [];
    private static readonly Lock _lock = new();

    /// <summary>
    /// Evento disparado cuando el estado de sesiones en memoria de cualquier proveedor cambia.
    /// </summary>
    public static event Action? SessionStateChanged;

    /// <summary>
    /// Registra un proveedor de sesiones de inferencia o modelo en memoria.
    /// </summary>
    public static void RegisterProvider(Func<int> getLoadedCount, Action clearSessions)
    {
        ArgumentNullException.ThrowIfNull(getLoadedCount);
        ArgumentNullException.ThrowIfNull(clearSessions);

        lock (_lock)
        {
            _sessionCountProviders.Add(getLoadedCount);
            _sessionClearActions.Add(clearSessions);
        }
    }

    /// <summary>
    /// Notifica a los observadores (ej. barra de estado de la UI) que el estado de sesiones ha cambiado.
    /// </summary>
    public static void NotifySessionStateChanged()
    {
        SessionStateChanged?.Invoke();
    }

    /// <summary>
    /// Obtiene el total consolidado de modelos o sesiones de inferencia cargadas en memoria entre todos los plugins registrados.
    /// </summary>
    public static int GetTotalLoadedSessions()
    {
        lock (_lock)
        {
            int total = 0;
            foreach (var provider in _sessionCountProviders)
            {
                try
                {
                    total += provider();
                }
                catch { }
            }
            return total;
        }
    }

    /// <summary>
    /// Solicita la descarga y liberación inmediata de todas las sesiones de modelos en memoria a todos los plugins registrados.
    /// </summary>
    public static void ClearAllSessions()
    {
        List<Action> actions;
        lock (_lock)
        {
            actions = [.. _sessionClearActions];
        }

        foreach (var action in actions)
        {
            try
            {
                action();
            }
            catch { }
        }

        NotifySessionStateChanged();
    }
}
