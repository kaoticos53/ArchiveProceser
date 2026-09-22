using System.Collections.Concurrent;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Registro de los almacenes de sesiones ONNX del plugin y punto único de observación y consulta.
///
/// <para>
/// Los tres motores (visión/texto, audio y embeddings) tienen su propio
/// <see cref="OnnxSessionStore"/> —cada uno con su política de sesión— pero todos publican sus cambios
/// aquí: un nodo que observa el estado de modelos sólo necesita escuchar
/// <see cref="SessionStateChanged"/>, y preguntar por cualquier modelo se resuelve contra todos los
/// almacenes sin saber cuál lo posee.
/// </para>
///
/// <para>
/// Cada almacén se registra en su propio constructor estático, es decir, antes de que pueda contener
/// ninguna sesión. Un almacén no registrado está por definición vacío, así que el conteo y la
/// liberación globales nunca dejan memoria viva sin contabilizar.
/// </para>
/// </summary>
public static class OnnxSessionRegistry
{
    private static readonly ConcurrentDictionary<OnnxSessionStore, byte> _stores = new();

    /// <summary>
    /// Evento único de estado de sesiones del plugin: cualquier carga, sustitución o descarga en
    /// cualquiera de los almacenes lo dispara.
    /// </summary>
    public static event Action? SessionStateChanged;

    /// <summary>Almacenes registrados. Instantánea, segura de enumerar mientras se registran otros.</summary>
    public static IReadOnlyCollection<OnnxSessionStore> Stores => _stores.Keys.ToList();

    internal static void Register(OnnxSessionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (_stores.TryAdd(store, 0))
        {
            store.SessionStateChanged += RaiseSessionStateChanged;
        }
    }

    /// <summary>Número total de sesiones materializadas entre todos los almacenes.</summary>
    public static int GetLoadedSessionCount()
        => _stores.Keys.Sum(store => store.GetLoadedSessionCount());

    /// <summary>Rutas de todos los modelos materializados, sin duplicados entre almacenes.</summary>
    public static IReadOnlyList<string> GetLoadedModelPaths()
        => _stores.Keys
            .SelectMany(store => store.GetLoadedModelPaths())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Indica si el modelo está cargado en cualquiera de los almacenes.</summary>
    public static bool IsSessionLoaded(string modelPath)
        => !string.IsNullOrWhiteSpace(modelPath) && _stores.Keys.Any(store => store.IsSessionLoaded(modelPath));

    /// <summary>Indica si el modelo aprovecha aceleración por hardware según su almacén propietario.</summary>
    public static bool IsHardwareAccelerated(string modelPath)
        => !string.IsNullOrWhiteSpace(modelPath) && _stores.Keys.Any(store => store.IsHardwareAccelerated(modelPath));

    /// <summary>Descarga el modelo de todos los almacenes que lo tengan materializado.</summary>
    public static bool UnloadSession(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath)) return false;

        bool unloaded = false;
        foreach (var store in _stores.Keys)
        {
            unloaded |= store.UnloadSession(modelPath);
        }

        return unloaded;
    }

    /// <summary>Libera todas las sesiones de todos los almacenes.</summary>
    public static void ClearAll()
    {
        foreach (var store in _stores.Keys)
        {
            store.Clear();
        }
    }

    private static void RaiseSessionStateChanged() => SessionStateChanged?.Invoke();
}
