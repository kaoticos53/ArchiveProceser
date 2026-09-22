using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Microsoft.ML.OnnxRuntime;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Caché compartida de sesiones ONNX de audio (Silero VAD y Piper TTS), incluyendo
/// la serialización de inferencia para evitar condiciones de carrera sobre los tensores.
/// </summary>
internal static class AudioSessionCache
{
    private static readonly ConcurrentDictionary<string, Lazy<InferenceSession>> _sessionCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock _inferenceLock = new();

    /// <summary>
    /// Se dispara cuando una sesión ONNX de audio se carga o se libera.
    /// </summary>
    public static event Action? SessionStateChanged;

    /// <summary>
    /// Exclusión mutua compartida por las inferencias ONNX de audio.
    /// </summary>
    public static Lock InferenceLock => _inferenceLock;

    public static bool IsSessionLoaded(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath)) return false;
        return _sessionCache.TryGetValue(modelPath, out var lazy) && lazy.IsValueCreated;
    }

    public static int GetLoadedSessionCount()
        => _sessionCache.Values.Count(lazy => lazy.IsValueCreated);

    public static bool UnloadSession(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath)) return false;
        if (_sessionCache.TryRemove(modelPath, out var lazy))
        {
            if (lazy.IsValueCreated)
            {
                try { lazy.Value.Dispose(); } catch { }
            }
            SessionStateChanged?.Invoke();
            return true;
        }
        return false;
    }

    public static InferenceSession GetOrCreateSession(string modelPath)
    {
        bool isNew = !_sessionCache.ContainsKey(modelPath);
        var lazy = _sessionCache.GetOrAdd(modelPath, path => new Lazy<InferenceSession>(() =>
        {
            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = 1,
                IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4)
            };

            var session = new InferenceSession(path, options);
            SessionStateChanged?.Invoke();
            return session;
        }));

        var instance = lazy.Value;
        if (isNew)
        {
            SessionStateChanged?.Invoke();
        }
        return instance;
    }

    /// <summary>
    /// Libera todas las sesiones ONNX de audio en caché.
    /// </summary>
    public static void Clear()
    {
        foreach (var lazy in _sessionCache.Values)
        {
            if (lazy.IsValueCreated)
            {
                try { lazy.Value.Dispose(); } catch { }
            }
        }
        _sessionCache.Clear();
        SessionStateChanged?.Invoke();
    }
}
