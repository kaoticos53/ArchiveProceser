using System.Resources;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Plugins;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Inicializador del plugin de IA y Visión por Computador (FileFlow.Plugin.AI).
/// Registra de forma autónoma los diccionarios de recursos multilingües (.resx) en LocalizationManager.
/// </summary>
public class AiPluginInitializer : IPluginInitializer
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
                _isRegistered = true;
            }
            catch
            {
                // Ignorar si el cargador dinámico ya lo registró
            }
        }
    }
}
