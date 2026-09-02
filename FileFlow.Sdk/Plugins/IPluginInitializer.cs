namespace FileFlow.Sdk.Plugins;

/// <summary>
/// Interfaz opcional que los plugins pueden implementar para ejecutar
/// rutinas de inicialización personalizada al cargarse en el motor.
/// </summary>
public interface IPluginInitializer
{
    /// <summary>
    /// Se invoca de forma determinista tras cargar el ensamblado del plugin.
    /// </summary>
    void Initialize();
}
