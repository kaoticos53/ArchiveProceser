using System.Runtime.InteropServices;
using FileFlow.Sdk.Platform;

namespace FileFlow.Core.Platform;

/// <summary>
/// Factoría de detección y provisión del adaptador de sistema operativo adecuado para el entorno de ejecución.
/// </summary>
public static class OsPlatformServiceFactory
{
    private static IOsPlatformService? _customInstance;
    private static readonly Lock _lock = new();

    /// <summary>
    /// Instancia activa del servicio de plataforma detectada para el sistema operativo en ejecución.
    /// </summary>
    public static IOsPlatformService Instance
    {
        get
        {
            lock (_lock)
            {
                if (_customInstance != null) return _customInstance;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return WindowsPlatformService.Instance;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return LinuxPlatformService.Instance;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return MacPlatformService.Instance;
                }

                return NullOsPlatformService.Instance;
            }
        }
    }

    /// <summary>
    /// Permite inyectar una instancia simulada o personalizada de <see cref="IOsPlatformService"/> (útil para pruebas unitarias).
    /// </summary>
    public static void SetCustomInstance(IOsPlatformService? service)
    {
        lock (_lock)
        {
            _customInstance = service;
        }
    }

    /// <summary>
    /// Crea o resuelve un adaptador para una plataforma de sistema operativo explícita.
    /// </summary>
    public static IOsPlatformService GetServiceFor(OSPlatform platform)
    {
        if (platform == OSPlatform.Windows) return WindowsPlatformService.Instance;
        if (platform == OSPlatform.Linux) return LinuxPlatformService.Instance;
        if (platform == OSPlatform.OSX) return MacPlatformService.Instance;
        return NullOsPlatformService.Instance;
    }
}
