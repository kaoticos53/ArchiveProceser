using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

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
    private string _icon = "📁";

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;

    public ToolboxCategoryFilterItem(string key, string displayName, string icon, int count = 0, bool isSelected = false)
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
    [ObservableProperty]
    private string _categoryName = string.Empty;

    public ObservableCollection<NodeToolboxItem> Items { get; } = [];

    public ToolboxCategoryGroup(string categoryName)
    {
        _categoryName = categoryName;
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
    private ToolboxPerspective _currentPerspective = ToolboxPerspective.ByCategory;

    public bool IsPipelineRolePerspective => CurrentPerspective == ToolboxPerspective.ByPipelineRole;

    public string PerspectiveButtonText => CurrentPerspective == ToolboxPerspective.ByCategory
        ? "🔄 " + LocalizationManager.Instance.GetString("Toolbox_Perspective_Pipeline", "Pipeline Stage")
        : "📁 " + LocalizationManager.Instance.GetString("Toolbox_Perspective_Domain", "Domain");

    partial void OnIsCompactModeChanged(bool value)
    {
        if (UserPreferencesService.Instance.Preferences.IsCompactToolbox != value)
        {
            UserPreferencesService.Instance.UpdatePreferences(p => p.IsCompactToolbox = value);
        }
    }

    [ObservableProperty]
    private string _selectedCategoryFilter = "Todas";

    [ObservableProperty]
    private ToolboxCategoryFilterItem? _selectedCategoryItem;

    partial void OnSelectedCategoryItemChanged(ToolboxCategoryFilterItem? value)
    {
        if (_isRefreshing) return;
        if (value != null && !_selectedCategoryFilter.Equals(value.Key, StringComparison.OrdinalIgnoreCase))
        {
            _selectedCategoryFilter = value.Key;
            RefreshToolbox();
        }
    }

    public ToolboxViewModel(PluginLoader pluginLoader)
    {
        _pluginLoader = pluginLoader;
        _isCompactMode = UserPreferencesService.Instance.Preferences.IsCompactToolbox;
        _languageChangedHandler = (_, _) => RefreshToolbox();
        _preferencesChangedHandler = () => RefreshToolbox();

        LocalizationManager.Instance.LanguageChanged += _languageChangedHandler;
        UserPreferencesService.Instance.PreferencesChanged += _preferencesChangedHandler;
        RefreshToolbox();
    }

    public void RefreshToolbox()
    {
        lock (_lock)
        {
            _isRefreshing = true;
            try
            {
                CategoryGroups.Clear();
                IsCompactMode = UserPreferencesService.Instance.Preferences.IsCompactToolbox;

                var prefs = UserPreferencesService.Instance;
                var allItems = new List<NodeToolboxItem>();

                var uniqueTypes = _pluginLoader.DiscoveredNodeTypes.Values.Distinct().ToList();

                foreach (var type in uniqueTypes)
                {
                    string typeName = type.FullName ?? type.Name;
                    IFlowNode? sampleInstance = null;
                    try
                    {
                        sampleInstance = _pluginLoader.CreateNodeInstance(typeName);
                    }
                    catch { }

                    var defAttr = type.GetCustomAttribute<NodeDefinitionAttribute>();
                    string name = LocalizationManager.Instance.GetString(type.Name + "_Name", sampleInstance?.Name ?? defAttr?.Name ?? type.Name);
                    if (name.EndsWith("_Name", StringComparison.OrdinalIgnoreCase) && sampleInstance != null && !string.IsNullOrWhiteSpace(sampleInstance.Name))
                    {
                        name = sampleInstance.Name;
                    }

                    string category = sampleInstance?.Category ?? defAttr?.Category ?? "General";

                    string description = LocalizationManager.Instance.GetString(type.Name + "_Desc", sampleInstance?.Description ?? defAttr?.Description ?? string.Empty);
                    if (description.EndsWith("_Desc", StringComparison.OrdinalIgnoreCase) && sampleInstance != null && !string.IsNullOrWhiteSpace(sampleInstance.Description))
                    {
                        description = sampleInstance.Description;
                    }

                    bool isFavorite = prefs.IsFavorite(typeName) || prefs.IsFavorite(type.Name);
                    int usageCount = Math.Max(prefs.GetUsageCount(typeName), prefs.GetUsageCount(type.Name));
                    string icon = GetIconForNodeType(typeName);

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

                    foreach (var r in roleOrder)
                    {
                        if (roleGroups.TryGetValue(r, out var roleItems) && roleItems.Count > 0)
                        {
                            string roleGroupName = LocalizationManager.Instance.GetString($"Role_{r}", r.ToString());
                            var group = new ToolboxCategoryGroup(roleGroupName);
                            foreach (var it in roleItems)
                            {
                                group.Items.Add(it);
                            }
                            CategoryGroups.Add(group);
                        }
                    }
                    return;
                }

                // 3. Filtro Especial: "Favoritos"
                if (SelectedCategoryFilter.Equals("Favoritos", StringComparison.OrdinalIgnoreCase) ||
                    SelectedCategoryFilter.Equals("Favorites", StringComparison.OrdinalIgnoreCase))
                {
                    var favGroup = new ToolboxCategoryGroup(favGroupName);
                    foreach (var item in allItems.Where(i => i.IsFavorite))
                    {
                        favGroup.Items.Add(item);
                    }
                    if (favGroup.Items.Count > 0)
                    {
                        CategoryGroups.Add(favGroup);
                    }
                    return;
                }

                // 3. Filtro Especial: "Frecuentes"
                if (SelectedCategoryFilter.Equals("Frecuentes", StringComparison.OrdinalIgnoreCase) ||
                    SelectedCategoryFilter.Equals("Frequent", StringComparison.OrdinalIgnoreCase))
                {
                    var freqGroup = new ToolboxCategoryGroup(freqGroupName);
                    foreach (var item in allItems.Where(i => i.UsageCount > 0).OrderByDescending(i => i.UsageCount).Take(10))
                    {
                        freqGroup.Items.Add(item);
                    }
                    if (freqGroup.Items.Count > 0)
                    {
                        CategoryGroups.Add(freqGroup);
                    }
                    return;
                }

                // 4. Agrupación Estándar o "Todas"
                var groupDict = new Dictionary<string, ToolboxCategoryGroup>(StringComparer.OrdinalIgnoreCase);

                if (SelectedCategoryFilter.Equals("Todas", StringComparison.OrdinalIgnoreCase) ||
                    SelectedCategoryFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    var favItems = allItems.Where(i => i.IsFavorite).ToList();
                    if (favItems.Count > 0)
                    {
                        var favGroup = new ToolboxCategoryGroup(favGroupName);
                        foreach (var f in favItems) favGroup.Items.Add(f);
                        CategoryGroups.Add(favGroup);
                    }

                    var freqItems = allItems.Where(i => i.UsageCount > 0).OrderByDescending(i => i.UsageCount).Take(10).ToList();
                    if (freqItems.Count > 0)
                    {
                        var freqGroup = new ToolboxCategoryGroup(freqGroupName);
                        foreach (var f in freqItems) freqGroup.Items.Add(f);
                        CategoryGroups.Add(freqGroup);
                    }
                }

                foreach (var item in allItems)
                {
                    if (!SelectedCategoryFilter.Equals("Todas", StringComparison.OrdinalIgnoreCase) &&
                        !SelectedCategoryFilter.Equals("All", StringComparison.OrdinalIgnoreCase) &&
                        !item.Category.Equals(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string localizedCategoryName = LocalizationManager.Instance.GetString($"Category_{item.Category}", item.Category);

                    if (!groupDict.TryGetValue(item.Category, out var group))
                    {
                        group = new ToolboxCategoryGroup(localizedCategoryName);
                        groupDict[item.Category] = group;
                        CategoryGroups.Add(group);
                    }

                    group.Items.Add(item);
                }
            }
            finally
            {
                _isRefreshing = false;
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
            new("Todas", LocalizationManager.Instance.GetString("Category_All", "Todas"), "🌐", totalCount,
                SelectedCategoryFilter.Equals("Todas", StringComparison.OrdinalIgnoreCase) || SelectedCategoryFilter.Equals("All", StringComparison.OrdinalIgnoreCase)),
            new("Favoritos", LocalizationManager.Instance.GetString("Category_Favorites", "Favoritos"), "⭐", favCount,
                SelectedCategoryFilter.Equals("Favoritos", StringComparison.OrdinalIgnoreCase) || SelectedCategoryFilter.Equals("Favorites", StringComparison.OrdinalIgnoreCase)),
            new("Frecuentes", LocalizationManager.Instance.GetString("Category_Frequent", "Más Usados"), "🔥", freqCount,
                SelectedCategoryFilter.Equals("Frecuentes", StringComparison.OrdinalIgnoreCase) || SelectedCategoryFilter.Equals("Frequent", StringComparison.OrdinalIgnoreCase))
        };

        // Descubrir todas las categorías dinámicas de plugins cargados
        var dynamicCategories = _pluginLoader.DiscoveredNodeTypes.Values
            .Distinct()
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
            string icon = GetIconForCategory(cat);
            bool isSelected = SelectedCategoryFilter.Equals(cat, StringComparison.OrdinalIgnoreCase);

            list.Add(new ToolboxCategoryFilterItem(cat, displayName, icon, count, isSelected));
        }

        // Actualizar la colección observable de forma limpia
        AvailableCategories.Clear();
        ToolboxCategoryFilterItem? matchedItem = null;
        foreach (var item in list)
        {
            AvailableCategories.Add(item);
            if (item.Key.Equals(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                matchedItem = item;
            }
        }

        if (matchedItem != null)
        {
            SelectedCategoryItem = matchedItem;
        }
        else if (AvailableCategories.Count > 0)
        {
            SelectedCategoryFilter = AvailableCategories[0].Key;
            SelectedCategoryItem = AvailableCategories[0];
        }
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

    public static string GetIconForCategory(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "all" or "todas" => "🌐",
            "favorites" or "favoritos" => "⭐",
            "frequent" or "frecuentes" or "más usados" => "🔥",
            "files" or "filesystem" or "archivos y sistema" or "archivos" or "file system" => "📁",
            "imagevision" or "images" or "imágenes" or "imagen y visión ia" or "fotos" => "🖼️",
            "audiovoice" or "audio" or "audio y voz ia" or "voz" => "🎙️",
            "documents" or "documentos" or "documentos y pdf" or "pdf" or "pdfs" => "📄",
            "data" or "data & tables" or "data & databases" or "datos y tablas" or "datos" or "databases" => "📊",
            "languageai" or "lenguaje y llm" or "language & llm" or "llm" => "🧠",
            "security" or "seguridad y rgpd" or "security & privacy" or "hashing" => "🔒",
            "logic" or "lógica y control" or "logic & flow" or "flujo" => "🔀",
            "archives" or "archivos comprimidos" or "compresión" or "compressed archives" => "📦",
            "network" or "red y nube" or "network & cloud" or "network & remote" or "red" => "🌐",
            "integrations" or "integraciones y diagnóstico" or "integrations & diagnostics" or "webhooks" or "cli" => "⚡",
            "scripting" or "scripts" or "c#" or "javascript" => "📜",
            "metadata" or "metadatos" => "🏷️",
            "media & docs" or "mediadocs" => "🎬",
            _ => "🧩"
        };
    }

    public static string GetIconForNodeType(string typeName)
    {
        if (typeName.Contains("FolderSource", StringComparison.OrdinalIgnoreCase)) return "📁";
        if (typeName.Contains("DirectoryInspector", StringComparison.OrdinalIgnoreCase)) return "🕵️";
        if (typeName.Contains("SmartUnpack", StringComparison.OrdinalIgnoreCase)) return "📦";
        if (typeName.Contains("ArchiveCompressor", StringComparison.OrdinalIgnoreCase)) return "🗜️";
        if (typeName.Contains("ArchiveFilter", StringComparison.OrdinalIgnoreCase)) return "🗄️";
        if (typeName.Contains("ImageOptimizer", StringComparison.OrdinalIgnoreCase)) return "🖼️";
        if (typeName.Contains("ExifMetadata", StringComparison.OrdinalIgnoreCase)) return "🏷️";
        if (typeName.Contains("MediaTranscoder", StringComparison.OrdinalIgnoreCase)) return "🎬";
        if (typeName.Contains("LocalOcr", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Ocr", StringComparison.OrdinalIgnoreCase)) return "🔍";
        if (typeName.Contains("SmartImageClassifier", StringComparison.OrdinalIgnoreCase) || typeName.Contains("ImageClassifier", StringComparison.OrdinalIgnoreCase)) return "👁️";
        if (typeName.Contains("FaceDetector", StringComparison.OrdinalIgnoreCase)) return "👤";
        if (typeName.Contains("ObjectDetector", StringComparison.OrdinalIgnoreCase)) return "🎯";
        if (typeName.Contains("Whisper", StringComparison.OrdinalIgnoreCase)) return "🎙️";
        if (typeName.Contains("DocumentProcessor", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Pdf", StringComparison.OrdinalIgnoreCase)) return "📄";
        if (typeName.Contains("FtpDownload", StringComparison.OrdinalIgnoreCase)) return "📥";
        if (typeName.Contains("FtpUpload", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Ftp", StringComparison.OrdinalIgnoreCase)) return "📤";
        if (typeName.Contains("SftpDownload", StringComparison.OrdinalIgnoreCase)) return "📥";
        if (typeName.Contains("SftpUpload", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Sftp", StringComparison.OrdinalIgnoreCase)) return "🔒";
        if (typeName.Contains("Smb", StringComparison.OrdinalIgnoreCase)) return "🖧";
        if (typeName.Contains("WebDav", StringComparison.OrdinalIgnoreCase)) return "☁️";
        if (typeName.Contains("RemoteDownload", StringComparison.OrdinalIgnoreCase)) return "📥";
        if (typeName.Contains("Excel", StringComparison.OrdinalIgnoreCase)) return "📊";
        if (typeName.Contains("Csv", StringComparison.OrdinalIgnoreCase)) return "📑";
        if (typeName.Contains("DataLookup", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Lookup", StringComparison.OrdinalIgnoreCase)) return "🔍";
        if (typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Database", StringComparison.OrdinalIgnoreCase)) return "🗄️";
        if (typeName.Contains("DataFormatConverter", StringComparison.OrdinalIgnoreCase) || typeName.Contains("DataConverter", StringComparison.OrdinalIgnoreCase)) return "🔄";
        if (typeName.Contains("VariableInjector", StringComparison.OrdinalIgnoreCase)) return "🏷️";
        if (typeName.Contains("DeduplicationFilter", StringComparison.OrdinalIgnoreCase)) return "👯";
        if (typeName.Contains("HashCalculator", StringComparison.OrdinalIgnoreCase)) return "🔑";
        if (typeName.Contains("ExpressionFilter", StringComparison.OrdinalIgnoreCase)) return "⚡";
        if (typeName.Contains("SwitchCase", StringComparison.OrdinalIgnoreCase)) return "🔀";
        if (typeName.Contains("ForkJoinBarrier", StringComparison.OrdinalIgnoreCase)) return "🔀";
        if (typeName.Contains("ThrottleDelay", StringComparison.OrdinalIgnoreCase)) return "⏳";
        if (typeName.Contains("BatchBuffer", StringComparison.OrdinalIgnoreCase)) return "📊";
        if (typeName.Contains("DestinationSink", StringComparison.OrdinalIgnoreCase)) return "💾";
        if (typeName.Contains("FileRelocator", StringComparison.OrdinalIgnoreCase)) return "🚚";
        if (typeName.Contains("EmptyDirectoryCleaner", StringComparison.OrdinalIgnoreCase)) return "🧹";
        if (typeName.Contains("SafeRecycleDelete", StringComparison.OrdinalIgnoreCase)) return "♻️";
        if (typeName.Contains("OriginalFileAction", StringComparison.OrdinalIgnoreCase)) return "🛡️";
        if (typeName.Contains("AdvancedRenamer", StringComparison.OrdinalIgnoreCase)) return "✏️";
        if (typeName.Contains("OperationReport", StringComparison.OrdinalIgnoreCase)) return "📋";
        if (typeName.Contains("LogOutput", StringComparison.OrdinalIgnoreCase)) return "📝";
        if (typeName.Contains("WebhookNotification", StringComparison.OrdinalIgnoreCase)) return "🌐";
        if (typeName.Contains("CliExecution", StringComparison.OrdinalIgnoreCase)) return "💻";
        if (typeName.Contains("CustomScript", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Script", StringComparison.OrdinalIgnoreCase)) return "📜";
        return "🧩";
    }
}
