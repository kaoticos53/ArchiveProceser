using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.App.Views.Components;

public partial class VariablePickerWindow : Window
{
    private readonly List<VariableItem> _allVariables = [];
    private readonly FileItemContext? _previewContext;
    private readonly ILocalizationService _loc;
    private string _currentCategory = "ALL";

    public string? SelectedToken { get; private set; }

    public VariablePickerWindow(
        IEnumerable<VariableGroupItem> groups,
        NodeViewModel? targetNode = null,
        FileItemContext? previewContext = null,
        ILocalizationService? localizationService = null)
    {
        InitializeComponent();
        WindowThemeHelper.ApplyThemeToWindow(this);

        _loc = localizationService ?? LocalizationManager.Instance;
        _previewContext = previewContext;

        // Configuración de contexto de nodo
        string nodeTitle = targetNode?.Title ?? "Personalizado";
        TxtTargetNode.Text = $"Nodo: {nodeTitle}";

        // Aplanar variables de todos los grupos preservando metadata y deduplicando por Token
        var seenTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int upstreamNodeCount = 0;
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

        upstreamNodeCount = upstreamNodeTitles.Count;
        if (upstreamNodeCount > 0)
        {
            BadgeUpstreamCount.Visibility = Visibility.Visible;
            TxtUpstreamCount.Text = string.Format(_loc.GetString("VarPicker_UpstreamCountBadge", "🔗 {0} nodo(s) anterior(es)"), upstreamNodeCount);
        }

        // Aplicar filtros iniciales
        ApplyFilter();

        Loaded += (_, _) =>
        {
            TxtSearch.Focus();
        };
    }

    private void ApplyFilter()
    {
        string query = TxtSearch.Text?.Trim() ?? string.Empty;
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
        BtnClearSearch.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;

        var filtered = _allVariables.Where(v =>
        {
            // Filtro por categoría seleccionada
            if (_currentCategory == "UPSTREAM" && !v.IsUpstream)
            {
                return false;
            }
            else if (_currentCategory == "SYSTEM" &&
                     !v.Category.Contains("Sistema", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("System", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Archivo", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("File", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else if (_currentCategory == "DATES" &&
                     !v.Category.Contains("Fecha", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Date", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Tiempo", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else if (_currentCategory == "SIZES" &&
                     !v.Category.Contains("Tamaño", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Size", StringComparison.OrdinalIgnoreCase) &&
                     !v.Category.Contains("Métrica", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else if (_currentCategory == "FUNCTIONS" &&
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

        ListVariables.ItemsSource = filtered;
        TxtNoResults.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        string countFormat = _loc.GetString("VarPicker_CountFormat", "Mostrando {0} de {1} variables");
        TxtCountStatus.Text = string.Format(countFormat, filtered.Count, _allVariables.Count);

        if (filtered.Count > 0)
        {
            ListVariables.SelectedIndex = 0;
        }
        else
        {
            ClearDetail();
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        TxtSearch.Text = string.Empty;
        TxtSearch.Focus();
    }

    private void CategoryFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;

        if (rb == RadioCatAll) _currentCategory = "ALL";
        else if (rb == RadioCatUpstream) _currentCategory = "UPSTREAM";
        else if (rb == RadioCatSystem) _currentCategory = "SYSTEM";
        else if (rb == RadioCatDates) _currentCategory = "DATES";
        else if (rb == RadioCatSizes) _currentCategory = "SIZES";
        else if (rb == RadioCatFunctions) _currentCategory = "FUNCTIONS";

        ApplyFilter();
    }

    private void ListVariables_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListVariables.SelectedItem is VariableItem item)
        {
            ShowDetail(item);
        }
        else
        {
            ClearDetail();
        }
    }

    private void ShowDetail(VariableItem item)
    {
        PanelDetailContent.Visibility = Visibility.Visible;
        TxtDetailPlaceholder.Visibility = Visibility.Collapsed;
        BtnInsert.IsEnabled = true;

        TxtDetailToken.Text = item.Token;
        TxtDetailDescription.Text = item.Description;
        TxtDetailCategory.Text = item.Category;

        if (item.IsUpstream && !string.IsNullOrWhiteSpace(item.SourceNodeTitle))
        {
            LabelDetailSource.Visibility = Visibility.Visible;
            TxtDetailSource.Visibility = Visibility.Visible;
            TxtDetailSource.Text = $"🔗 {item.SourceNodeTitle}";
        }
        else
        {
            LabelDetailSource.Visibility = Visibility.Visible;
            TxtDetailSource.Visibility = Visibility.Visible;
            TxtDetailSource.Text = _loc.GetString("VarPicker_SystemBadge", "🌐 Global / Sistema");
        }

        // Vista previa resuelta con el contexto proporcionado
        try
        {
            var ctx = _previewContext ?? new FileItemContext();
            string resolved = VariableTemplateResolver.Resolve(item.Token, ctx);

            // Si no se reemplazó nada o devuelve lo mismo, mostramos el SampleValue
            if (string.Equals(resolved, item.Token, StringComparison.Ordinal) && !string.IsNullOrEmpty(item.SampleValue))
            {
                TxtDetailEvaluated.Text = item.SampleValue;
            }
            else
            {
                TxtDetailEvaluated.Text = string.IsNullOrEmpty(resolved) ? (item.SampleValue ?? "-") : resolved;
            }
        }
        catch
        {
            TxtDetailEvaluated.Text = item.SampleValue ?? item.Token;
        }
    }

    private void ClearDetail()
    {
        PanelDetailContent.Visibility = Visibility.Collapsed;
        TxtDetailPlaceholder.Visibility = Visibility.Visible;
        BtnInsert.IsEnabled = false;
    }

    private void ListVariables_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListVariables.SelectedItem is VariableItem item)
        {
            ConfirmSelection(item.Token);
        }
    }

    private void ListVariables_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ListVariables.SelectedItem is VariableItem item)
        {
            ConfirmSelection(item.Token);
            e.Handled = true;
        }
    }

    private void BtnInsert_Click(object sender, RoutedEventArgs e)
    {
        if (ListVariables.SelectedItem is VariableItem item)
        {
            ConfirmSelection(item.Token);
        }
    }

    private void ConfirmSelection(string token)
    {
        SelectedToken = token;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnCopyToken_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtDetailToken.Text))
        {
            LogViewModel.SafeSetClipboardText(TxtDetailToken.Text);
        }
    }
}
