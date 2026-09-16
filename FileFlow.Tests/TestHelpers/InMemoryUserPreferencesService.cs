using System;
using System.Collections.Generic;
using System.Linq;
using FileFlow.App.Services;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Preferencias de usuario en memoria, aisladas del perfil del desarrollador.
///
/// Las capturas y el resto de pruebas de UI que montan view models completos dependen del estado de las
/// preferencias: qué nodos son favoritos, qué contadores de uso hay, si el toolbox está compacto, el tema o
/// el idioma activos. Leyendo el fichero real del usuario, la línea base de una captura dependería de la
/// máquina —el mismo test pasaría en un equipo y fallaría en otro— y además cada ejecución de la suite
/// escribiría en las preferencias de quien la lanza. Este doble fija ese estado en los valores por defecto
/// y lo deja inspeccionable.
/// </summary>
public sealed class InMemoryUserPreferencesService : IUserPreferencesService
{
    private readonly HashSet<string> _favorites;
    private readonly Dictionary<string, int> _usage;

    public InMemoryUserPreferencesService(UserPreferencesData? data = null)
    {
        Preferences = data ?? new UserPreferencesData();
        _favorites = new HashSet<string>(Preferences.FavoriteNodeTypes, StringComparer.OrdinalIgnoreCase);
        _usage = new Dictionary<string, int>(Preferences.NodeUsageCounts, StringComparer.OrdinalIgnoreCase);
    }

    public UserPreferencesData Preferences { get; }

    public event Action? PreferencesChanged;

    public int SaveCount { get; private set; }

    public void Load()
    {
        // Nada que cargar: el estado es el que ya tiene el modelo en memoria.
    }

    public void Save()
    {
        SaveCount++;
        Preferences.FavoriteNodeTypes = new HashSet<string>(_favorites, StringComparer.OrdinalIgnoreCase);
        Preferences.NodeUsageCounts = new Dictionary<string, int>(_usage, StringComparer.OrdinalIgnoreCase);
        PreferencesChanged?.Invoke();
    }

    public void UpdatePreferences(Action<UserPreferencesData> updateAction)
    {
        ArgumentNullException.ThrowIfNull(updateAction);

        updateAction(Preferences);
        Save();
    }

    public bool IsFavorite(string typeName) => _favorites.Contains(typeName);

    public bool ToggleFavorite(string typeName)
    {
        bool isFavorite = _favorites.Contains(typeName);

        if (isFavorite)
        {
            _favorites.Remove(typeName);
        }
        else
        {
            _favorites.Add(typeName);
        }

        Save();
        return !isFavorite;
    }

    public void IncrementNodeUsage(string typeName)
    {
        _usage[typeName] = GetUsageCount(typeName) + 1;
    }

    public int GetUsageCount(string typeName) => _usage.TryGetValue(typeName, out int count) ? count : 0;

    public List<string> GetFavoriteNodeTypes() => _favorites.ToList();

    public List<(string TypeName, int Count)> GetTopUsedNodeTypes(int limit = 5) =>
        _usage
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(limit)
            .Select(pair => (pair.Key, pair.Value))
            .ToList();
}
