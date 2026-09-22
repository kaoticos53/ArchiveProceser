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

                // El registro unificado es la única fuente de verdad de sesiones en memoria: suma los
                // almacenes de visión, audio y embeddings (antes el de embeddings quedaba fuera del
                // conteo de la barra de estado) y publica un solo evento de cambio de estado.
                FileFlow.Sdk.ModelSessionRegistry.RegisterProvider(
                    Inference.OnnxSessionRegistry.GetLoadedSessionCount,
                    ClearAllSessions
                );
                Inference.OnnxSessionRegistry.SessionStateChanged += FileFlow.Sdk.ModelSessionRegistry.NotifySessionStateChanged;

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
    /// Delega en el registro unificado, que recorre los almacenes de visión, audio y embeddings, de modo
    /// que añadir un motor nuevo no obligue a tocar este método. Previene fugas de memoria nativa y
    /// permite recargar modelos sin reiniciar la aplicación.
    /// </summary>
    public static void ClearAllSessions() => Inference.OnnxSessionRegistry.ClearAll();
}
