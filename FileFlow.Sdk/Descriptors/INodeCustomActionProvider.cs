namespace FileFlow.Sdk;

/// <summary>
/// Interfaz opcional implementada por nodos que proporcionan una o más acciones o diálogos de configuración visual personalizados.
/// </summary>
public interface INodeCustomActionProvider
{
    /// <summary>
    /// Ejecuta una acción personalizada identificada por <paramref name="actionId"/> proporcionando un contexto opcional.
    /// </summary>
    void ExecuteCustomAction(string actionId, object? context = null);
}
