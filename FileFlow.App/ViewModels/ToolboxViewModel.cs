using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFlow.App.Models;
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

    public ToolboxViewModel(PluginLoader pluginLoader)
    {
        _pluginLoader = pluginLoader;
        LocalizationManager.Instance.LanguageChanged += (_, _) => RefreshToolbox();
        RefreshToolbox();
    }

    public void RefreshToolbox()
    {
        CategoryGroups.Clear();

        var groupDict = new Dictionary<string, ToolboxCategoryGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var (typeName, type) in _pluginLoader.DiscoveredNodeTypes)
        {
            var defAttr = type.GetCustomAttribute<NodeDefinitionAttribute>();
            string rawName = defAttr?.Name ?? type.Name;
            string rawDesc = defAttr?.Description ?? string.Empty;

            string name = LocalizationManager.Instance.GetString(type.Name + "_Name", rawName);
            string category = defAttr?.Category ?? "General";
            string description = LocalizationManager.Instance.GetString(type.Name + "_Desc", rawDesc);

            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !category.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!groupDict.TryGetValue(category, out var group))
            {
                group = new ToolboxCategoryGroup(category);
                groupDict[category] = group;
                CategoryGroups.Add(group);
            }

            group.Items.Add(new NodeToolboxItem(name, category, description, typeName));
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshToolbox();
    }
}
