using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.App.ViewModels;

/// <summary>
/// ViewModel desacoplado para el catálogo visual y selector de variables dinámicas del pipeline.
/// </summary>
public partial class VariablePickerViewModel : ObservableObject
{
    private readonly List<VariableItem> _allVariables = [];
    private readonly FileItemContext? _previewContext;
    private readonly ILocalizationService _loc;

    [ObservableProperty]
    private string _targetNodeTitle = "Personalizado";

    [ObservableProperty]
    private int _upstreamCount;

    [ObservableProperty]
    private string _upstreamCountText = string.Empty;

    [ObservableProperty]
    private bool _hasUpstreamNodes;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _currentCategory = "ALL";

    [ObservableProperty]
    private VariableItem? _selectedVariable;

    [ObservableProperty]
    private bool _hasSelectedVariable;

    [ObservableProperty]
    private string _selectedToken = string.Empty;

    [ObservableProperty]
    private string _detailToken = string.Empty;

    [ObservableProperty]
    private string _detailDescription = string.Empty;

    [ObservableProperty]
    private string _detailCategory = string.Empty;

    [ObservableProperty]
    private string _detailSource = string.Empty;

    [ObservableProperty]
    private string _detailEvaluated = string.Empty;

    [ObservableProperty]
    private string _countStatusText = string.Empty;

    [ObservableProperty]
    private bool _hasNoResults;

    public ObservableCollection<VariableItem> FilteredVariables { get; } = [];

    public IReadOnlyList<VariableItem> AllVariables => _allVariables;

    public VariablePickerViewModel(
        IEnumerable<VariableGroupItem> groups,
        NodeViewModel? targetNode = null,
        FileItemContext? previewContext = null,
        ILocalizationService? localizationService = null)
    {
        _loc = localizationService ?? LocalizationManager.Instance;
        _previewContext = previewContext;

        TargetNodeTitle = targetNode?.Title ?? "Personalizado";

        // Aplanar variables deduplicando por Token
        var seenTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var upstreamNodeTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            foreach (var variable in group.Variables)
            {
                if (seenTokens.Add(variable.Token))
                {
                    _allVariables.Add(variable);
                    if (variable.IsUpstream && !string.IsNullOrWhiteSpace(variable.SourceNodeTitle))
                    {
                        upstreamNodeTitles.Add(variable.SourceNodeTitle);
                    }
                }
            }
        }

        UpstreamCount = upstreamNodeTitles.Count;
        HasUpstreamNodes = UpstreamCount > 0;
        if (HasUpstreamNodes)
        {
            UpstreamCountText = string.Format(_loc.GetString("VarPicker_UpstreamCountBadge", "🔗 {0} nodo(s) anterior(es)"), UpstreamCount);
        }

        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnCurrentCategoryChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedVariableChanged(VariableItem? value)
    {
        if (value != null)
        {
            ShowDetail(value);
        }
        else
        {
            ClearDetail();
        }
    }

    [RelayCommand]
    public void SetCategory(string category)
    {
        CurrentCategory = category;
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    public void ApplyFilter()
    {
        string query = SearchText?.Trim() ?? string.Empty;

        var filtered = _allVariables.Where(v =>
        {
            // Filtro por categoría seleccionada
            if (CurrentCategory == "UPSTREAM" && !v.IsUpstream)
            {
                return false;
            }
            else if (CurrentCategory == "SYSTEM" &&
                     !v.Category.Contains("Sistema", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("System", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Archivo", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("File", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else if (CurrentCategory == "DATES" &&
                     !v.Category.Contains("Fecha", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Date", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Tiempo", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else if (CurrentCategory == "SIZES" &&
                     !v.Category.Contains("Tamaño", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Size", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Métrica", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else if (CurrentCategory == "FUNCTIONS" &&
                     !v.Category.Contains("Función", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Function", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Texto", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Filtro por texto de búsqueda
            if (string.IsNullOrEmpty(query)) return true;

            return v.Token.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   v.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   v.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrEmpty(v.SourceNodeTitle) && v.SourceNodeTitle.Contains(query, StringComparison.OrdinalIgnoreCase));
        }).ToList();

        FilteredVariables.Clear();
        foreach (var item in filtered)
        {
            FilteredVariables.Add(item);
        }

        HasNoResults = FilteredVariables.Count == 0;
        string countFormat = _loc.GetString("VarPicker_CountFormat", "Mostrando {0} de {1} variables");
        CountStatusText = string.Format(countFormat, FilteredVariables.Count, _allVariables.Count);

        if (FilteredVariables.Count > 0)
        {
            SelectedVariable = FilteredVariables[0];
        }
        else
        {
            ClearDetail();
        }
    }

    private void ShowDetail(VariableItem item)
    {
        HasSelectedVariable = true;
        DetailToken = item.Token;
        DetailDescription = item.Description;
        DetailCategory = item.Category;

        if (item.IsUpstream && !string.IsNullOrWhiteSpace(item.SourceNodeTitle))
        {
            DetailSource = $"🔗 {item.SourceNodeTitle}";
        }
        else
        {
            DetailSource = _loc.GetString("VarPicker_SystemBadge", "🌐 Global / Sistema");
        }

        try
        {
            var ctx = _previewContext ?? new FileItemContext();
            string resolved = VariableTemplateResolver.Resolve(item.Token, ctx);

            if (string.Equals(resolved, item.Token, StringComparison.Ordinal) && !string.IsNullOrEmpty(item.SampleValue))
            {
                DetailEvaluated = item.SampleValue;
            }
            else
            {
                DetailEvaluated = string.IsNullOrEmpty(resolved) ? (item.SampleValue ?? "-") : resolved;
            }
        }
        catch
        {
            DetailEvaluated = item.SampleValue ?? item.Token;
        }
    }

    private void ClearDetail()
    {
        HasSelectedVariable = false;
        DetailToken = string.Empty;
        DetailDescription = string.Empty;
        DetailCategory = string.Empty;
        DetailSource = string.Empty;
        DetailEvaluated = string.Empty;
    }

    [RelayCommand]
    public void CopyToken()
    {
        if (!string.IsNullOrEmpty(DetailToken))
        {
            LogViewModel.SafeSetClipboardText(DetailToken);
        }
    }
}
