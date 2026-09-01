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

    public ObservableCollection<ToolboxCategoryGroup> CategoryGroups { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private NodeToolboxItem? _selectedItem;

    [ObservableProperty]
    private bool _isCompactMode = true;

    [ObservableProperty]
    private string _selectedCategoryFilter = "Todas";

    public ObservableCollection<string> AvailableCategoryFilters { get; } =
        ["Todas", "Favoritos", "Frecuentes", "FileSystem", "Archives", "MediaDocs", "Metadata", "Logic", "Integrations"];

    public ToolboxViewModel(PluginLoader pluginLoader)
    {
        _pluginLoader = pluginLoader;
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

            var item = new NodeToolboxItem(name, category, description, typeName, icon, isFavorite, usageCount);

            // Text Search Filter
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !category.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !description.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            allItems.Add(item);
        }

        string favGroupName = LocalizationManager.Instance.GetString("Filter_Favorites", "⭐ Favoritos");
        string freqGroupName = LocalizationManager.Instance.GetString("Filter_Frequent", "🔥 Más Usados");

        // Special Filter Chips: "Favoritos" and "Frecuentes"
        if (SelectedCategoryFilter.Equals("Favoritos", StringComparison.OrdinalIgnoreCase) || SelectedCategoryFilter.Equals("Favorites", StringComparison.OrdinalIgnoreCase))
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

        if (SelectedCategoryFilter.Equals("Frecuentes", StringComparison.OrdinalIgnoreCase) || SelectedCategoryFilter.Equals("Frequent", StringComparison.OrdinalIgnoreCase))
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

        // Standard Categorized Groups (or "Todas")
        var groupDict = new Dictionary<string, ToolboxCategoryGroup>(StringComparer.OrdinalIgnoreCase);

        // 1. Add "⭐ Favoritos" group on top if "Todas" is selected and favorites exist
        if (SelectedCategoryFilter.Equals("Todas", StringComparison.OrdinalIgnoreCase) || SelectedCategoryFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
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

        // 2. Add Category Groups
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
    public void SetCategoryFilter(string category)
    {
        SelectedCategoryFilter = category;
        RefreshToolbox();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshToolbox();
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
        if (typeName.Contains("DocumentProcessor", StringComparison.OrdinalIgnoreCase)) return "📄";
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
