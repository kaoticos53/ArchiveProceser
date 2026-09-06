using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.App.Views.Components;

public partial class TextEditorDialogWindow : Window
{
    private readonly NodeParameterViewModel? _parameter;
    private readonly List<VariableItem> _allVariables = [];
    private readonly IVariableDiscoveryService _variableDiscoveryService;
    private readonly ILocalizationService _loc;

    public string ResultText { get; private set; } = string.Empty;

    public TextEditorDialogWindow(NodeParameterViewModel parameter)
        : this(parameter.DisplayName, parameter.Value?.ToString() ?? string.Empty, parameter)
    {
    }

    public TextEditorDialogWindow(
        string title,
        string initialText,
        NodeParameterViewModel? parameter = null,
        IVariableDiscoveryService? variableDiscoveryService = null,
        ILocalizationService? localizationService = null)
    {
        InitializeComponent();
        WindowThemeHelper.ApplyThemeToWindow(this);

        _parameter = parameter;
        _variableDiscoveryService = variableDiscoveryService ?? VariableDiscoveryService.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;

        TxtTitle.Text = string.IsNullOrWhiteSpace(title) ? "Editor de Texto / Prompt" : title;
        if (_parameter != null)
        {
            string nodeTitle = _parameter.NodeOwner?.Title ?? "Personalizado";
            TxtSubtitle.Text = $"Parámetro: {_parameter.Key}  •  Nodo: {nodeTitle}";
        }
        else
        {
            TxtSubtitle.Text = "Edición de texto libre con soporte para variables";
        }

        TxtEditor.Text = initialText ?? string.Empty;
        TxtEditor.Focus();
        TxtEditor.CaretIndex = TxtEditor.Text.Length;

        LoadVariables();

        UpdateStats();
        UpdateLivePreview();
    }

    private void LoadVariables()
    {
        var groups = _parameter?.AvailableVariables;
        if ((groups == null || groups.Count == 0) && _parameter?.NodeOwner != null)
        {
            if (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm)
            {
                _parameter.RefreshAvailableVariables(mainVm.Editor);
                groups = _parameter.AvailableVariables;
            }
        }

        if (groups == null || groups.Count == 0)
        {
            // Fallback a variables descubiertas para el nodo si existen o sistema
            if (_parameter?.NodeOwner != null)
            {
                var editor = (Application.Current?.MainWindow?.DataContext as MainViewModel)?.Editor;
                if (editor != null)
                {
                    groups = _variableDiscoveryService.GetAvailableVariables(_parameter.NodeOwner, editor.Connections);
                }
            }
        }

        var seenTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (groups != null)
        {
            foreach (var g in groups)
            {
                foreach (var v in g.Variables)
                {
                    if (seenTokens.Add(v.Token))
                    {
                        _allVariables.Add(v);
                    }
                }
            }
        }

        // Si aún está vacío, añadir variables de sistema esenciales
        if (_allVariables.Count == 0)
        {
            _allVariables.AddRange(
            [
                new("GlobalOutputDir", "{GlobalOutputDir}", "Carpeta de salida global", "Sistema", "C:\\Output"),
                new("FileName", "{FileName}", "Nombre del archivo", "Sistema", "documento.pdf"),
                new("FileNameWithoutExtension", "{FileNameWithoutExtension}", "Nombre sin extensión", "Sistema", "documento"),
                new("Extension", "{Extension}", "Extensión del archivo", "Sistema", ".pdf"),
                new("Date", "{Date}", "Fecha actual (yyyyMMdd)", "Fechas", DateTime.Now.ToString("yyyyMMdd")),
                new("Year", "{Year}", "Año actual (yyyy)", "Fechas", DateTime.Now.ToString("yyyy")),
                new("Month", "{Month}", "Mes actual (MM)", "Fechas", DateTime.Now.ToString("MM")),
                new("Day", "{Day}", "Día actual (dd)", "Fechas", DateTime.Now.ToString("dd")),
                new("Guid", "{Guid}", "Identificador GUID único", "Sistema", Guid.NewGuid().ToString("N")),
                new("TempDir", "{TempDir}", "Directorio temporal de trabajo", "Sistema", "C:\\Temp\\Flow_a1b2c3d4"),
                new("RandomId", "{RandomId}", "Identificador aleatorio seguro", "Sistema", "x8f2q1"),
                new("Random", "{Random:4}", "Dígitos aleatorios", "Funciones", "4829")
            ]);
        }

        FilterSideVariables();
    }

    private void FilterSideVariables()
    {
        string query = TxtSideSearch?.Text?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(query)
            ? _allVariables
            : _allVariables.Where(v =>
                v.Token.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                v.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                v.Category.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (ListSideVariables != null)
        {
            ListSideVariables.ItemsSource = filtered;
        }
    }

    private void TxtSideSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterSideVariables();
    }

    private void BtnToggleSidePanel_Click(object sender, RoutedEventArgs e)
    {
        bool isCurrentlyVisible = BorderSidePanel.Visibility == Visibility.Visible;
        BorderSidePanel.Visibility = isCurrentlyVisible ? Visibility.Collapsed : Visibility.Visible;
        ColSidePanel.Width = isCurrentlyVisible ? new GridLength(0) : new GridLength(260);
    }

    private void BtnCloseSidePanel_Click(object sender, RoutedEventArgs e)
    {
        BorderSidePanel.Visibility = Visibility.Collapsed;
        ColSidePanel.Width = new GridLength(0);
        TxtEditor.Focus();
    }

    private void ListSideVariables_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListSideVariables.SelectedItem is VariableItem selected)
        {
            InsertToken(selected.Token);
        }
    }

    private void TxtEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateStats();
        UpdateLivePreview();
        CheckIntelliSenseTrigger();
    }

    private void CheckIntelliSenseTrigger()
    {
        int caret = TxtEditor.CaretIndex;
        string text = TxtEditor.Text ?? string.Empty;

        if (caret > 0 && caret <= text.Length)
        {
            int lastOpenBrace = text.LastIndexOf('{', caret - 1);
            if (lastOpenBrace >= 0)
            {
                int closeBrace = text.IndexOf('}', lastOpenBrace);
                // Si la llave de cierre no existe o está después del cursor
                if (closeBrace < 0 || closeBrace >= caret)
                {
                    string query = text.Substring(lastOpenBrace + 1, caret - (lastOpenBrace + 1));
                    // Si el query contiene saltos de línea o espacios largos, cerrar popup
                    if (query.Contains('\n') || query.Contains('\r'))
                    {
                        PopupIntelliSense.IsOpen = false;
                        return;
                    }

                    var matches = _allVariables.Where(v =>
                    {
                        string tokenInner = v.Token.Trim('{', '}');
                        return tokenInner.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               v.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
                    }).Take(12).ToList();

                    if (matches.Count > 0)
                    {
                        ListIntelliSense.ItemsSource = matches;
                        ListIntelliSense.SelectedIndex = 0;

                        try
                        {
                            var rect = TxtEditor.GetRectFromCharacterIndex(lastOpenBrace);
                            PopupIntelliSense.HorizontalOffset = Math.Max(0, rect.Left);
                            PopupIntelliSense.VerticalOffset = rect.Bottom + 4;
                            PopupIntelliSense.IsOpen = true;
                            return;
                        }
                        catch
                        {
                            // En caso de fallo de medida de fuente, mantener posición por defecto
                            PopupIntelliSense.IsOpen = true;
                            return;
                        }
                    }
                }
            }
        }

        PopupIntelliSense.IsOpen = false;
    }

    private void TxtEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (PopupIntelliSense.IsOpen)
        {
            if (e.Key == Key.Down)
            {
                int nextIndex = Math.Min(ListIntelliSense.SelectedIndex + 1, ListIntelliSense.Items.Count - 1);
                ListIntelliSense.SelectedIndex = nextIndex;
                ListIntelliSense.ScrollIntoView(ListIntelliSense.SelectedItem);
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Up)
            {
                int prevIndex = Math.Max(ListIntelliSense.SelectedIndex - 1, 0);
                ListIntelliSense.SelectedIndex = prevIndex;
                ListIntelliSense.ScrollIntoView(ListIntelliSense.SelectedItem);
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                InsertIntelliSenseSelection();
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Escape)
            {
                PopupIntelliSense.IsOpen = false;
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SaveAndClose();
            e.Handled = true;
        }
    }

    private void ListIntelliSense_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        InsertIntelliSenseSelection();
    }

    private void InsertIntelliSenseSelection()
    {
        if (ListIntelliSense.SelectedItem is not VariableItem selected) return;

        int caret = TxtEditor.CaretIndex;
        string text = TxtEditor.Text ?? string.Empty;
        int openBrace = (caret > 0 && caret <= text.Length) ? text.LastIndexOf('{', caret - 1) : -1;

        if (openBrace >= 0)
        {
            int replaceLen = caret - openBrace;
            if (caret < text.Length && text[caret] == '}')
            {
                replaceLen++;
            }
            string newText = text.Remove(openBrace, replaceLen).Insert(openBrace, selected.Token);
            TxtEditor.Text = newText;
            TxtEditor.CaretIndex = openBrace + selected.Token.Length;
        }
        else
        {
            InsertToken(selected.Token);
        }

        PopupIntelliSense.IsOpen = false;
        TxtEditor.Focus();
    }

    private void UpdateStats()
    {
        string text = TxtEditor.Text ?? string.Empty;
        int chars = text.Length;
        int words = string.IsNullOrWhiteSpace(text) ? 0 : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        int lines = TxtEditor.LineCount > 0 ? TxtEditor.LineCount : (string.IsNullOrWhiteSpace(text) ? 1 : text.Split('\n').Length);

        TxtCharCount.Text = $"🔤 {chars} car.";
        TxtWordCount.Text = $"📝 {words} pal.";
        TxtLineCount.Text = $"📄 {lines} lín.";
    }

    private void UpdateLivePreview()
    {
        string text = TxtEditor.Text ?? string.Empty;
        bool hasVars = (text.Contains('{') && text.Contains('}')) || (text.Contains('<') && text.Contains('>'));

        if (!hasVars || string.IsNullOrWhiteSpace(text))
        {
            BorderLivePreview.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            var previewContext = _variableDiscoveryService.CreatePreviewItem(_parameter?.NodeOwner);
            string resolved = VariableTemplateResolver.Resolve(text, previewContext);
            TxtLivePreview.Text = resolved;
            BorderLivePreview.Visibility = Visibility.Visible;
        }
        catch
        {
            BorderLivePreview.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnInsertVar_Click(object sender, RoutedEventArgs e)
    {
        var previewContext = _variableDiscoveryService.CreatePreviewItem(_parameter?.NodeOwner);
        var groups = _parameter?.AvailableVariables ?? [];

        var cm = new ContextMenu
        {
            MaxHeight = 480
        };
        ScrollViewer.SetVerticalScrollBarVisibility(cm, ScrollBarVisibility.Auto);

        // 1. Abrir catálogo completo
        var miFullCatalog = new MenuItem
        {
            Header = _loc.GetString("VarPicker_OpenFullCatalog", "🔍 Abrir Catálogo Completo de Variables..."),
            FontWeight = FontWeights.Bold
        };
        miFullCatalog.Click += (_, _) =>
        {
            var dialog = new VariablePickerWindow(groups, _parameter?.NodeOwner, previewContext, _loc)
            {
                Owner = this
            };
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedToken))
            {
                InsertToken(dialog.SelectedToken);
            }
        };
        cm.Items.Add(miFullCatalog);
        cm.Items.Add(new Separator());

        // 2. Submenús agrupados
        foreach (var group in groups)
        {
            if (group.Variables.Count == 0) continue;

            var subMenu = new MenuItem
            {
                Header = $"{group.GroupName} ({group.Variables.Count})",
                FontWeight = group.IsUpstream ? FontWeights.SemiBold : FontWeights.Normal,
                MaxHeight = 350
            };
            ScrollViewer.SetVerticalScrollBarVisibility(subMenu, ScrollBarVisibility.Auto);

            foreach (var v in group.Variables)
            {
                var mi = new MenuItem
                {
                    Header = $"{v.Token}  —  {v.Description}",
                    Tag = v.Token,
                    ToolTip = !string.IsNullOrEmpty(v.SampleValue) ? $"Ejemplo: {v.SampleValue}" : null
                };
                mi.Click += (s, _) =>
                {
                    if (s is MenuItem clicked && clicked.Tag is string token)
                    {
                        InsertToken(token);
                    }
                };
                subMenu.Items.Add(mi);
            }
            cm.Items.Add(subMenu);
        }

        cm.PlacementTarget = BtnInsertVar;
        cm.IsOpen = true;
    }

    private void InsertToken(string token)
    {
        int caret = TxtEditor.CaretIndex;
        string text = TxtEditor.Text ?? string.Empty;
        TxtEditor.Text = text.Insert(caret, token);
        TxtEditor.CaretIndex = caret + token.Length;
        TxtEditor.Focus();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtEditor.Text))
        {
            LogViewModel.SafeSetClipboardText(TxtEditor.Text);
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        TxtEditor.Text = string.Empty;
        TxtEditor.Focus();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveAndClose();
    }

    private void SaveAndClose()
    {
        ResultText = TxtEditor.Text ?? string.Empty;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
