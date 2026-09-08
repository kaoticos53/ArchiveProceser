using System.Resources;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Plugins;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Inicializador del plugin de IA y Visión por Computador (FileFlow.Plugin.AI).
/// Registra de forma autónoma los diccionarios de recursos multilingües (.resx) en LocalizationManager.
/// </summary>
public sealed class AiPluginInitializer : IPluginInitializer
{
    private static readonly Lock _lock = new();
    private static bool _isRegistered;

    static AiPluginInitializer()
    {
        Register();
    }

    public void Initialize()
    {
        Register();
    }

    public static void Register()
    {
        if (_isRegistered) return;
        lock (_lock)
        {
            if (_isRegistered) return;
            try
            {
                var rm = new ResourceManager("FileFlow.Plugin.AI.Resources.Strings", typeof(AiPluginInitializer).Assembly);
                LocalizationManager.Instance.RegisterResourceManager(rm);

                FileFlow.Sdk.ModelSessionRegistry.RegisterProvider(
                    () => Inference.OnnxSessionManager.GetLoadedSessionCount() + AudioInferenceEngine.GetLoadedSessionCount(),
                    ClearAllSessions
                );
                Inference.OnnxSessionManager.SessionStateChanged += FileFlow.Sdk.ModelSessionRegistry.NotifySessionStateChanged;
                AudioInferenceEngine.SessionStateChanged += FileFlow.Sdk.ModelSessionRegistry.NotifySessionStateChanged;

                _isRegistered = true;
            }
            catch
            {
                // Ignorar si el cargador dinámico ya lo registró
            }
        }
    }

    /// <summary>
    /// Libera deterministamente todas las sesiones ONNX y tensores en memoria de los motores de IA.
    /// Previene fugas de memoria nativa y permite recargar modelos sin reiniciar la aplicación.
    /// </summary>
    public static void ClearAllSessions()
    {
        OnnxInferenceEngine.ClearSessionCache();
        AudioInferenceEngine.ClearSessionCache();
        SemanticEmbeddingEngine.ClearSessionCache();
        LanguageInferenceEngine.ClearSessionCache();
        try
        {
            SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
        }
        catch { }
    }
}
