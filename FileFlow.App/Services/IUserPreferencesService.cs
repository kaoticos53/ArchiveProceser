namespace FileFlow.App.Services;

/// <summary>
/// Contrato de puerto para la persistencia, consulta y gestión reactiva de preferencias de usuario y métricas de uso de la UI.
/// </summary>
public interface IUserPreferencesService
{
    /// <summary>
    /// Modelo de datos de preferencias activas.
    /// </summary>
    UserPreferencesData Preferences { get; }

    /// <summary>
    /// Evento emitido cuando se modifican y guardan las preferencias.
    /// </summary>
    event Action? PreferencesChanged;

    /// <summary>
    /// Carga las preferencias desde el almacenamiento persistente.
    /// </summary>
    void Load();

    /// <summary>
    /// Guarda las preferencias actuales en el almacenamiento persistente.
    /// </summary>
    void Save();

    /// <summary>
    /// Aplica una mutación transaccional sobre las preferencias y las persiste.
    /// </summary>
    void UpdatePreferences(Action<UserPreferencesData> updateAction);

    /// <summary>
    /// Comprueba si un nodo está marcado como favorito.
    /// </summary>
    bool IsFavorite(string typeName);

    /// <summary>
    /// Alterna el estado de favorito de un tipo de nodo.
    /// </summary>
    bool ToggleFavorite(string typeName);

    /// <summary>
    /// Incrementa el contador de uso para un tipo de nodo.
    /// </summary>
    void IncrementNodeUsage(string typeName);

    /// <summary>
    /// Obtiene el número de veces que se ha insertado un nodo.
    /// </summary>
    int GetUsageCount(string typeName);

    /// <summary>
    /// Obtiene la lista de identificadores de tipos de nodo favoritos.
    /// </summary>
    List<string> GetFavoriteNodeTypes();

    /// <summary>
    /// Obtiene los tipos de nodo más utilizados ordenados por frecuencia.
    /// </summary>
    List<(string TypeName, int Count)> GetTopUsedNodeTypes(int limit = 5);
}
