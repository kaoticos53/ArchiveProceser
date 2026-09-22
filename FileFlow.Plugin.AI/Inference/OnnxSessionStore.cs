using System.Collections.Concurrent;
using System.Threading;
using Microsoft.ML.OnnxRuntime;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Almacén de sesiones ONNX con ciclo de vida propio: caché diferida y thread-safe, serialización de
/// la inferencia, consulta de estado, descarga granular y notificación de cada cambio.
///
/// <para>
/// Antes este mismo patrón existía tres veces con nombres distintos: el gestor genérico de
/// visión/texto, la caché privada del motor de audio y un diccionario sin evento dentro de
/// <c>SemanticEmbeddingEngine</c>. Sólo el primero emitía evento y sólo dos admitían descarga, así que
/// el estado de un modelo de embeddings era invisible para la UI. Esta clase es ahora la única
/// implementación; cada motor aporta únicamente su factoría de sesiones y, si procede, su política de
/// aceleración y su fallback.
/// </para>
/// </summary>
public sealed class OnnxSessionStore
{
    private readonly ConcurrentDictionary<string, Lazy<InferenceSession>> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _inferenceLock = new();
    private readonly Func<string, InferenceSession> _createSession;
    private readonly Func<string, InferenceSession>? _cpuFallbackFactory;
    private readonly Func<string, bool>? _accelerationProbe;
    private readonly Action? _releaseRetainedResources;

    public OnnxSessionStore(
        string name,
        Func<string, InferenceSession> createSession,
        Func<string, bool>? accelerationProbe = null,
        Func<string, InferenceSession>? cpuFallbackFactory = null,
        Action? releaseRetainedResources = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(createSession);

        Name = name;
        _createSession = createSession;
        _accelerationProbe = accelerationProbe;
        _cpuFallbackFactory = cpuFallbackFactory;
        _releaseRetainedResources = releaseRetainedResources;
    }

    /// <summary>Nombre del motor propietario, útil para diagnóstico y registro.</summary>
    public string Name { get; }

    /// <summary>
    /// Se dispara cuando una sesión de este almacén se materializa, se sustituye o se libera. El
    /// <see cref="OnnxSessionRegistry"/> lo reexpide como el evento único del plugin.
    /// </summary>
    public event Action? SessionStateChanged;

    /// <summary>Exclusión mutua para las inferencias que no pasan por <see cref="RunInference"/>.</summary>
    public Lock InferenceLock => _inferenceLock;

    /// <summary>Indica si el modelo aprovecha aceleración por hardware según la política del motor.</summary>
    public bool IsHardwareAccelerated(string modelPath)
        => !string.IsNullOrWhiteSpace(modelPath) && (_accelerationProbe?.Invoke(modelPath) ?? false);

    /// <summary>Indica si la sesión del modelo está materializada en memoria.</summary>
    public bool IsSessionLoaded(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath)) return false;
        return _sessions.TryGetValue(modelPath, out var lazy) && lazy.IsValueCreated;
    }

    /// <summary>Número de sesiones materializadas en este almacén.</summary>
    public int GetLoadedSessionCount()
        => _sessions.Values.Count(lazy => lazy.IsValueCreated);

    /// <summary>Rutas de los modelos actualmente materializados en este almacén.</summary>
    public IReadOnlyList<string> GetLoadedModelPaths()
        => _sessions.Where(kv => kv.Value.IsValueCreated).Select(kv => kv.Key).ToList();

    /// <summary>Descarga y libera deterministamente la sesión asociada al modelo.</summary>
    public bool UnloadSession(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath)) return false;

        if (_sessions.TryRemove(modelPath, out var lazy))
        {
            if (lazy.IsValueCreated)
            {
                try { lazy.Value.Dispose(); } catch { }
            }

            ReleaseRetainedResources();
            SessionStateChanged?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Obtiene o materializa de forma diferida y thread-safe la sesión del modelo. Si la creación
    /// falla, la entrada se descarta para permitir reintentos sin reiniciar la aplicación.
    /// </summary>
    public InferenceSession GetOrCreateSession(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        var lazy = _sessions.GetOrAdd(modelPath, path => new Lazy<InferenceSession>(() =>
        {
            var session = _createSession(path);

            // El evento se emite dentro de la factoría: se ejecuta exactamente una vez por sesión
            // creada, incluso si varias hebras compiten por el mismo modelo.
            SessionStateChanged?.Invoke();
            return session;
        }));

        try
        {
            return lazy.Value;
        }
        catch
        {
            _sessions.TryRemove(modelPath, out _);
            throw;
        }
    }

    /// <summary>
    /// Ejecuta la inferencia de forma serializada. Si el almacén declara un fallback de CPU y el
    /// proveedor acelerado falla en tiempo de ejecución, la sesión se sustituye por una de CPU y la
    /// inferencia se reintenta sin propagar el error.
    /// </summary>
    public IDisposableReadOnlyCollection<DisposableNamedOnnxValue> RunInference(
        string modelPath,
        IReadOnlyList<NamedOnnxValue> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var session = GetOrCreateSession(modelPath);

        lock (_inferenceLock)
        {
            try
            {
                return session.Run(inputs);
            }
            catch (Exception ex) when (_cpuFallbackFactory is not null && IsAcceleratedProviderError(ex))
            {
                var cpuSession = _cpuFallbackFactory(modelPath);
                _sessions[modelPath] = new Lazy<InferenceSession>(() => cpuSession);

                try { session.Dispose(); } catch { }

                SessionStateChanged?.Invoke();
                return cpuSession.Run(inputs);
            }
        }
    }

    /// <summary>Libera todas las sesiones del almacén.</summary>
    public void Clear()
    {
        foreach (var lazy in _sessions.Values)
        {
            if (lazy.IsValueCreated)
            {
                try { lazy.Value.Dispose(); } catch { }
            }
        }

        _sessions.Clear();
        ReleaseRetainedResources();
        SessionStateChanged?.Invoke();
    }

    private void ReleaseRetainedResources()
    {
        try { _releaseRetainedResources?.Invoke(); } catch { }
    }

    private static bool IsAcceleratedProviderError(Exception ex)
    {
        string message = ex.ToString();

        return message.Contains("DmlExecutionProvider", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("MLOperatorAuthorImpl", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("80070057", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("DirectML", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("node_Shape", StringComparison.OrdinalIgnoreCase) ||
               ex is OnnxRuntimeException;
    }
}
