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

public partial class ToolboxViewModel : ObservableObject
{
    private readonly PluginLoader _pluginLoader;

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
        LocalizationManager.Instance.LanguageChanged += (_, _) => RefreshToolbox();
        UserPreferencesService.Instance.PreferencesChanged += () => RefreshToolbox();
        RefreshToolbox();
    }

    public void RefreshToolbox()
    {
        CategoryGroups.Clear();
        IsCompactMode = UserPreferencesService.Instance.Preferences.IsCompactToolbox;

        var prefs = UserPreferencesService.Instance;
        var allItems = new List<NodeToolboxItem>();

        foreach (var (typeName, type) in _pluginLoader.DiscoveredNodeTypes)
        {
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

            bool isFavorite = prefs.IsFavorite(typeName);
            int usageCount = prefs.GetUsageCount(typeName);
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

        // Special Filter Chips: "Favoritos" and "Frecuentes"
        if (SelectedCategoryFilter.Equals("Favoritos", StringComparison.OrdinalIgnoreCase))
        {
            var favGroup = new ToolboxCategoryGroup("⭐ Favoritos");
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

        if (SelectedCategoryFilter.Equals("Frecuentes", StringComparison.OrdinalIgnoreCase))
        {
            var freqGroup = new ToolboxCategoryGroup("🔥 Más Usados");
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
        if (SelectedCategoryFilter.Equals("Todas", StringComparison.OrdinalIgnoreCase))
        {
            var favItems = allItems.Where(i => i.IsFavorite).ToList();
            if (favItems.Count > 0)
            {
                var favGroup = new ToolboxCategoryGroup("⭐ Favoritos");
                foreach (var f in favItems) favGroup.Items.Add(f);
                CategoryGroups.Add(favGroup);
            }

            var freqItems = allItems.Where(i => i.UsageCount > 0).OrderByDescending(i => i.UsageCount).Take(10).ToList();
            if (freqItems.Count > 0)
            {
                var freqGroup = new ToolboxCategoryGroup("🔥 Más Usados");
                foreach (var f in freqItems) freqGroup.Items.Add(f);
                CategoryGroups.Add(freqGroup);
            }
        }

        // 2. Add Category Groups
        foreach (var item in allItems)
        {
            if (!SelectedCategoryFilter.Equals("Todas", StringComparison.OrdinalIgnoreCase) &&
                !item.Category.Equals(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!groupDict.TryGetValue(item.Category, out var group))
            {
                group = new ToolboxCategoryGroup(item.Category);
                groupDict[item.Category] = group;
                CategoryGroups.Add(group);
            }

            group.Items.Add(item);
        }
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
        if (typeName.Contains("MediaTranscoder", StringComparison.OrdinalIgnoreCase)) return "🎬";
        if (typeName.Contains("DocumentProcessor", StringComparison.OrdinalIgnoreCase)) return "📄";
        if (typeName.Contains("VariableInjector", StringComparison.OrdinalIgnoreCase)) return "🏷️";
        if (typeName.Contains("DeduplicationFilter", StringComparison.OrdinalIgnoreCase)) return "👯";
        if (typeName.Contains("ExpressionFilter", StringComparison.OrdinalIgnoreCase)) return "⚡";
        if (typeName.Contains("SwitchCase", StringComparison.OrdinalIgnoreCase)) return "🔀";
        if (typeName.Contains("BatchBuffer", StringComparison.OrdinalIgnoreCase)) return "📊";
        if (typeName.Contains("DestinationSink", StringComparison.OrdinalIgnoreCase)) return "💾";
        if (typeName.Contains("OriginalFileAction", StringComparison.OrdinalIgnoreCase)) return "🗑️";
        return "🧩";
    }
}
