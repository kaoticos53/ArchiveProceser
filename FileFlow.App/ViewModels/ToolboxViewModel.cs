using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Material.Icons;

namespace FileFlow.App.ViewModels;

public enum ToolboxPerspective
{
    ByCategory,
    ByPipelineRole
}

public partial class ToolboxCategoryFilterItem : ObservableObject
{
    public string Key { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private MaterialIconKind _icon = MaterialIconKind.Folder;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;

    public ToolboxCategoryFilterItem(string key, string displayName, MaterialIconKind icon, int count = 0, bool isSelected = false)
    {
        Key = key;
        _displayName = displayName;
        _icon = icon;
        _count = count;
        _isSelected = isSelected;
    }
}

public partial class ToolboxCategoryGroup : ObservableObject
{
    private readonly Action<ToolboxCategoryGroup>? _onExpanded;

    [ObservableProperty]
    private string _categoryName = string.Empty;

    [ObservableProperty]
    private MaterialIconKind _icon = MaterialIconKind.Folder;

    [ObservableProperty]
    private bool _isExpanded;

    public string CategoryKey { get; }

    public ObservableCollection<NodeToolboxItem> Items { get; } = [];

    public ToolboxCategoryGroup(
        string categoryName, 
        string categoryKey = "", 
        bool isExpanded = false, 
        Action<ToolboxCategoryGroup>? onExpanded = null,
        MaterialIconKind? icon = null)
    {
        _categoryName = categoryName;
        CategoryKey = string.IsNullOrWhiteSpace(categoryKey) ? categoryName : categoryKey;
        _isExpanded = isExpanded;
        _onExpanded = onExpanded;
        _icon = icon ?? NodeIconResolver.GetIconForCategory(CategoryKey);
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value)
        {
            _onExpanded?.Invoke(this);
        }
    }
}

public partial class ToolboxViewModel : ObservableObject, IDisposable
{
    private readonly PluginLoader _pluginLoader;
    private readonly Lock _lock = new();
    private readonly Action _preferencesChangedHandler;
    private readonly EventHandler<System.Globalization.CultureInfo> _languageChangedHandler;
    private bool _disposed;
    private bool _isRefreshing;

    public ObservableCollection<ToolboxCategoryGroup> CategoryGroups { get; } = [];
    public ObservableCollection<ToolboxCategoryFilterItem> AvailableCategories { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private NodeToolboxItem? _selectedItem;

    [ObservableProperty]
    private bool _isCompactMode = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPipelineRolePerspective))]
    [NotifyPropertyChangedFor(nameof(PerspectiveButtonText))]
    private ToolboxPerspective _currentPerspective = ToolboxPerspective.ByCategory;

    public bool IsPipelineRolePerspective => CurrentPerspective == ToolboxPerspective.ByPipelineRole;

    public string PerspectiveButtonText => CurrentPerspective == ToolboxPerspective.ByCategory
        ? "🔄 " + (_loc?.GetString("Toolbox_Perspective_Pipeline", "Pipeline Stage") ?? LocalizationManager.Instance.GetString("Toolbox_Perspective_Pipeline", "Pipeline Stage"))
        : "📁 " + (_loc?.GetString("Toolbox_Perspective_Domain", "Domain") ?? LocalizationManager.Instance.GetString("Toolbox_Perspective_Domain", "Domain"));

    partial void OnIsCompactModeChanged(bool value)
    {
        var prefs = _userPreferencesService ?? UserPreferencesService.Instance;
        if (prefs.Preferences.IsCompactToolbox != value)
        {
            prefs.UpdatePreferences(p => p.IsCompactToolbox = value);
        }
    }

    [ObservableProperty]
    private string _selectedCategoryFilter = "Todas";

    [ObservableProperty]
    private ToolboxCategoryFilterItem? _selectedCategoryItem;

    partial void OnSelectedCategoryItemChanged(ToolboxCategoryFilterItem? value)
    {
        if (_isRefreshing) return;
        if (value != null && !SelectedCategoryFilter.Equals(value.Key, StringComparison.OrdinalIgnoreCase))
        {
            SelectedCategoryFilter = value.Key;
            RefreshToolbox();
        }
    }

    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ILocalizationService _loc;

    public ToolboxViewModel(
        PluginLoader pluginLoader,
        IUserPreferencesService? userPreferencesService = null,
        ILocalizationService? localizationService = null)
    {
        _pluginLoader = pluginLoader;
        _userPreferencesService = userPreferencesService ?? UserPreferencesService.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;
        _isCompactMode = _userPreferencesService.Preferences.IsCompactToolbox;
        _languageChangedHandler = (_, _) => RefreshToolbox();
        _preferencesChangedHandler = () => RefreshToolbox();

        _loc.LanguageChanged += _languageChangedHandler;
        _userPreferencesService.PreferencesChanged += _preferencesChangedHandler;
        RefreshToolbox();
    }

    public void RefreshToolbox()
    {
        lock (_lock)
        {
            _isRefreshing = true;
            try
            {
                OnPropertyChanged(nameof(PerspectiveButtonText));
                IsCompactMode = _userPreferencesService.Preferences.IsCompactToolbox;

                var prefs = _userPreferencesService;
                var allItems = new List<NodeToolboxItem>();
                var seenTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // UniqueNodeTypes ya devuelve una snapshot desduplicada y thread-safe
                var uniqueTypes = _pluginLoader.UniqueNodeTypes;

                foreach (var type in uniqueTypes)
                {
                    string typeName = type.FullName ?? type.Name;
                    if (!seenTypeNames.Add(typeName))
                    {
                        continue;
                    }

                    var defAttr = type.GetCustomAttribute<NodeDefinitionAttribute>();

                    string rawName = defAttr?.Name ?? type.Name;
                    string name = _loc.GetString(type.Name + "_Name", rawName);
                    if (name.EndsWith("_Name", StringComparison.OrdinalIgnoreCase))
                    {
                        name = rawName;
                    }

                    string category = defAttr?.Category ?? "General";

                    string rawDesc = defAttr?.Description ?? string.Empty;
                    string description = _loc.GetString(type.Name + "_Desc", rawDesc);
                    if (description.EndsWith("_Desc", StringComparison.OrdinalIgnoreCase))
                    {
                        description = rawDesc;
                    }

                    bool isFavorite = prefs.IsFavorite(typeName) || prefs.IsFavorite(type.Name);
                    int usageCount = Math.Max(prefs.GetUsageCount(typeName), prefs.GetUsageCount(type.Name));
                    MaterialIconKind icon = GetIconForNodeType(typeName);

                    var role = defAttr?.Role ?? PipelineRole.Transform;
                    var tags = defAttr?.Tags ?? Array.Empty<string>();
                    var subCategory = defAttr?.SubCategory ?? string.Empty;
                    string localizedRole = LocalizationManager.Instance.GetString($"Role_{role}", role.ToString());

                    var item = new NodeToolboxItem(name, category, description, typeName, icon, isFavorite, usageCount, role, tags, subCategory, localizedRole);

                    // Multilingual Search Filter: checks Name, Category, Description, LocalizedRole, Role name, and Tags
                    if (!string.IsNullOrWhiteSpace(SearchText))
                    {
                        bool matches = name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                       category.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                       description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                       localizedRole.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                       role.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                       tags.Any(t => t.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

                        if (!matches)
                        {
                            continue;
                        }
                    }

                    allItems.Add(item);
                }

                // 1. Construir / Actualizar dinámicamente AvailableCategories con contadores en tiempo real
                UpdateAvailableCategories(allItems);

                string favGroupName = LocalizationManager.Instance.GetString("Category_Favorites", "⭐ Favoritos");
                string freqGroupName = LocalizationManager.Instance.GetString("Category_Frequent", "🔥 Más Usados");

                string? previouslyExpandedKey = CategoryGroups.FirstOrDefault(g => g.IsExpanded)?.CategoryKey;
                bool hadExistingGroups = CategoryGroups.Count > 0;
                bool isSpecificFilter = !SelectedCategoryFilter.Equals("Todas", StringComparison.OrdinalIgnoreCase) &&
                                        !SelectedCategoryFilter.Equals("All", StringComparison.OrdinalIgnoreCase);

                bool DetermineInitialExpanded(string groupKey, bool isFirst = false)
                {
                    if (!string.IsNullOrWhiteSpace(SearchText))
                    {
                        return true;
                    }

                    if (isSpecificFilter)
                    {
                        return true;
                    }

                    if (hadExistingGroups && previouslyExpandedKey != null)
                    {
                        return string.Equals(groupKey, previouslyExpandedKey, StringComparison.OrdinalIgnoreCase);
                    }

                    return isFirst;
                }

                var targetGroups = new List<ToolboxCategoryGroup>();

                // 2. Dual Perspective: Group by Pipeline Role if selected
                if (CurrentPerspective == ToolboxPerspective.ByPipelineRole)
                {
                    var roleOrder = new[]
                    {
                        PipelineRole.Source,
                        PipelineRole.Filter,
                        PipelineRole.Transform,
                        PipelineRole.Analyze,
                        PipelineRole.Sink,
                        PipelineRole.Control
                    };

                    IEnumerable<NodeToolboxItem> filteredItems = allItems;
                    if (SelectedCategoryFilter.Equals("Favoritos", StringComparison.OrdinalIgnoreCase) ||
                        SelectedCategoryFilter.Equals("Favorites", StringComparison.OrdinalIgnoreCase))
                    {
                        filteredItems = filteredItems.Where(i => i.IsFavorite);
                    }
                    else if (SelectedCategoryFilter.Equals("Frecuentes", StringComparison.OrdinalIgnoreCase) ||
                             SelectedCategoryFilter.Equals("Frequent", StringComparison.OrdinalIgnoreCase))
                    {
                        filteredItems = filteredItems.Where(i => i.UsageCount > 0).OrderByDescending(i => i.UsageCount).Take(10);
                    }
                    else if (!SelectedCategoryFilter.Equals("Todas", StringComparison.OrdinalIgnoreCase) &&
                             !SelectedCategoryFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
                    {
                        var matching = filteredItems.Where(i => i.Category.Equals(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (matching.Count > 0)
                        {
                            filteredItems = matching;
                        }
                    }

                    var roleGroups = filteredItems.GroupBy(i => i.Role).ToDictionary(g => g.Key, g => g.ToList());

                    bool isFirstRole = true;
                    foreach (var r in roleOrder)
                    {
                        if (roleGroups.TryGetValue(r, out var roleItems) && roleItems.Count > 0)
                        {
                            string roleGroupName = LocalizationManager.Instance.GetString($"Role_{r}", r.ToString());
                            string roleKey = $"Role_{r}";
                            var group = new ToolboxCategoryGroup(
                                roleGroupName, 
                                roleKey, 
                                DetermineInitialExpanded(roleKey, isFirstRole), 
                                HandleGroupExpanded);

                            foreach (var it in roleItems)
                            {
                                group.Items.Add(it);
                            }
                            targetGroups.Add(group);
                            isFirstRole = false;
                        }
                    }

                    CommitGroups(targetGroups);
                    return;
                }

                // 3. Filtro Especial: "Favoritos"
                if (SelectedCategoryFilter.Equals("Favoritos", StringComparison.OrdinalIgnoreCase) ||
                    SelectedCategoryFilter.Equals("Favorites", StringComparison.OrdinalIgnoreCase))
                {
                    var favGroup = new ToolboxCategoryGroup(
                        favGroupName, 
                        "Favorites", 
                        DetermineInitialExpanded("Favorites", true), 
                        HandleGroupExpanded);

                    foreach (var item in allItems.Where(i => i.IsFavorite))
                    {
                        favGroup.Items.Add(item);
                    }
                    if (favGroup.Items.Count > 0)
                    {
                        targetGroups.Add(favGroup);
                    }
                    CommitGroups(targetGroups);
                    return;
                }

                // 3. Filtro Especial: "Frecuentes"
                if (SelectedCategoryFilter.Equals("Frecuentes", StringComparison.OrdinalIgnoreCase) ||
                    SelectedCategoryFilter.Equals("Frequent", StringComparison.OrdinalIgnoreCase))
                {
                    var freqGroup = new ToolboxCategoryGroup(
                        freqGroupName, 
                        "Frequent", 
                        DetermineInitialExpanded("Frequent", true), 
                        HandleGroupExpanded);

                    foreach (var item in allItems.Where(i => i.UsageCount > 0).OrderByDescending(i => i.UsageCount).Take(10))
                    {
                        freqGroup.Items.Add(item);
                    }
                    if (freqGroup.Items.Count > 0)
                    {
                        targetGroups.Add(freqGroup);
                    }
                    CommitGroups(targetGroups);
                    return;
                }

                // 4. Agrupación Estándar o "Todas"
                var itemsByCategory = allItems
                    .GroupBy(i => i.Category, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => LocalizationManager.Instance.GetString($"Category_{g.Key}", g.Key));

                bool isFirstCat = true;
                foreach (var categoryGroup in itemsByCategory)
                {
                    if (!SelectedCategoryFilter.Equals("Todas", StringComparison.OrdinalIgnoreCase) &&
                        !SelectedCategoryFilter.Equals("All", StringComparison.OrdinalIgnoreCase) &&
                        !categoryGroup.Key.Equals(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string localizedCategoryName = LocalizationManager.Instance.GetString($"Category_{categoryGroup.Key}", categoryGroup.Key);

                    var group = new ToolboxCategoryGroup(
                        localizedCategoryName, 
                        categoryGroup.Key, 
                        DetermineInitialExpanded(categoryGroup.Key, isFirstCat), 
                        HandleGroupExpanded);

                    foreach (var it in categoryGroup)
                    {
                        group.Items.Add(it);
                    }

                    targetGroups.Add(group);
                    isFirstCat = false;
                }

                CommitGroups(targetGroups);
            }
            finally
            {
                _isRefreshing = false;
            }
        }
    }

    internal void HandleGroupExpanded(ToolboxCategoryGroup expandedGroup)
    {
        if (_isRefreshing) return;

        foreach (var group in CategoryGroups)
        {
            if (group != expandedGroup && group.IsExpanded)
            {
                group.IsExpanded = false;
            }
        }
    }

    private void CommitGroups(List<ToolboxCategoryGroup> newGroups)
    {
        CategoryGroups.Clear();
        var seenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in newGroups)
        {
            if (seenGroups.Add(group.CategoryKey))
            {
                CategoryGroups.Add(group);
            }
        }
    }

    private void UpdateAvailableCategories(List<NodeToolboxItem> allItems)
    {
        var categoryCounts = allItems
            .GroupBy(i => i.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        int totalCount = allItems.Count;
        int favCount = allItems.Count(i => i.IsFavorite);
        int freqCount = allItems.Count(i => i.UsageCount > 0);

        var list = new List<ToolboxCategoryFilterItem>
        {
            new("Todas", LocalizationManager.Instance.GetString("Category_All", "Todas"), MaterialIconKind.Web, totalCount,
                SelectedCategoryFilter.Equals("Todas", StringComparison.OrdinalIgnoreCase) || SelectedCategoryFilter.Equals("All", StringComparison.OrdinalIgnoreCase)),
            new("Favoritos", LocalizationManager.Instance.GetString("Category_Favorites", "Favoritos"), MaterialIconKind.Star, favCount,
                SelectedCategoryFilter.Equals("Favoritos", StringComparison.OrdinalIgnoreCase) || SelectedCategoryFilter.Equals("Favorites", StringComparison.OrdinalIgnoreCase)),
            new("Frecuentes", LocalizationManager.Instance.GetString("Category_Frequent", "Más Usados"), MaterialIconKind.Fire, freqCount,
                SelectedCategoryFilter.Equals("Frecuentes", StringComparison.OrdinalIgnoreCase) || SelectedCategoryFilter.Equals("Frequent", StringComparison.OrdinalIgnoreCase))
        };

        // Descubrir todas las categorías dinámicas de plugins cargados
        var dynamicCategories = _pluginLoader.UniqueNodeTypes
            .Select(type =>
            {
                var defAttr = type.GetCustomAttribute<NodeDefinitionAttribute>();
                return defAttr?.Category ?? "General";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(cat => LocalizationManager.Instance.GetString($"Category_{cat}", cat))
            .ToList();

        foreach (var cat in dynamicCategories)
        {
            int count = categoryCounts.TryGetValue(cat, out int c) ? c : 0;
            string displayName = LocalizationManager.Instance.GetString($"Category_{cat}", cat);
            MaterialIconKind icon = GetIconForCategory(cat);
            bool isSelected = SelectedCategoryFilter.Equals(cat, StringComparison.OrdinalIgnoreCase);

            list.Add(new ToolboxCategoryFilterItem(cat, displayName, icon, count, isSelected));
        }

        // Actualizar la colección observable de forma limpia e in-place si ya existe la estructura
        bool needsRebuild = AvailableCategories.Count != list.Count;
        if (!needsRebuild)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (!AvailableCategories[i].Key.Equals(list[i].Key, StringComparison.OrdinalIgnoreCase))
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (needsRebuild)
        {
            AvailableCategories.Clear();
            foreach (var item in list)
            {
                AvailableCategories.Add(item);
            }
        }
        else
        {
            for (int i = 0; i < list.Count; i++)
            {
                var existing = AvailableCategories[i];
                var updated = list[i];
                existing.DisplayName = updated.DisplayName;
                existing.Icon = updated.Icon;
                existing.Count = updated.Count;
                existing.IsSelected = updated.IsSelected;
            }
        }

        var matchedItem = AvailableCategories.FirstOrDefault(c => c.Key.Equals(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
                          ?? AvailableCategories.FirstOrDefault();

#pragma warning disable MVVMTK0034
        if (matchedItem != null && !ReferenceEquals(_selectedCategoryItem, matchedItem))
        {
            _selectedCategoryItem = matchedItem;
            OnPropertyChanged(nameof(SelectedCategoryItem));
        }
        else if (AvailableCategories.Count > 0 && _selectedCategoryItem == null)
        {
            _selectedCategoryFilter = AvailableCategories[0].Key;
            _selectedCategoryItem = AvailableCategories[0];
            OnPropertyChanged(nameof(SelectedCategoryItem));
        }
#pragma warning restore MVVMTK0034
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        LocalizationManager.Instance.LanguageChanged -= _languageChangedHandler;
        UserPreferencesService.Instance.PreferencesChanged -= _preferencesChangedHandler;
    }

    [RelayCommand]
    public void ToggleFavorite(NodeToolboxItem item)
    {
        if (item == null) return;
        UserPreferencesService.Instance.ToggleFavorite(item.TypeName);
    }

    [RelayCommand]
    public void ToggleViewMode()
    {
        IsCompactMode = !IsCompactMode;
    }

    [RelayCommand]
    public void TogglePerspective()
    {
        CurrentPerspective = CurrentPerspective == ToolboxPerspective.ByCategory
            ? ToolboxPerspective.ByPipelineRole
            : ToolboxPerspective.ByCategory;
        SelectedCategoryFilter = "Todas";
        OnPropertyChanged(nameof(IsPipelineRolePerspective));
        OnPropertyChanged(nameof(PerspectiveButtonText));
        RefreshToolbox();
    }

    [RelayCommand]
    public void SetCategoryFilter(string category)
    {
        SelectedCategoryFilter = category;
        var found = AvailableCategories.FirstOrDefault(c => c.Key.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            SelectedCategoryItem = found;
        }
        RefreshToolbox();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshToolbox();
    }

    public static MaterialIconKind GetIconForCategory(string category) => NodeIconResolver.GetIconForCategory(category);

    public static MaterialIconKind GetIconForNodeType(string typeName) => NodeIconResolver.GetIconForNodeType(typeName);
}
