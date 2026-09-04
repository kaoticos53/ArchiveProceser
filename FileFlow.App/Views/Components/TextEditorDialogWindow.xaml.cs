using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.App.Views.Components;

public partial class TextEditorDialogWindow : Window
{
    private readonly NodeParameterViewModel? _parameter;

    public string ResultText { get; private set; } = string.Empty;

    public TextEditorDialogWindow(NodeParameterViewModel parameter)
        : this(parameter.DisplayName, parameter.Value?.ToString() ?? string.Empty, parameter)
    {
    }

    public TextEditorDialogWindow(string title, string initialText, NodeParameterViewModel? parameter = null)
    {
        InitializeComponent();
        WindowThemeHelper.ApplyThemeToWindow(this);

        _parameter = parameter;
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

        UpdateStats();
        UpdateLivePreview();
    }

    private void TxtEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateStats();
        UpdateLivePreview();
    }

    private void TxtEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SaveAndClose();
            e.Handled = true;
        }
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
            var ctx = new FileFlow.Sdk.FileItemContext();
            string resolved = VariableTemplateResolver.Resolve(text, ctx);
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
        var vars = _parameter?.AvailableVariables;
        if ((vars == null || vars.Count == 0) && _parameter?.NodeOwner != null)
        {
            if (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm)
            {
                _parameter.RefreshAvailableVariables(mainVm.Editor);
                vars = _parameter.AvailableVariables;
            }
        }

        if (vars == null || vars.Count == 0)
        {
            // Variables de sistema generales si no hay contexto de nodo upstream
            var sysGroup = new Models.VariableGroupItem("Variables de Sistema");
            sysGroup.Variables.AddRange(
            [
                new("GlobalOutputDir", "{GlobalOutputDir}", "Carpeta de salida global"),
                new("FileName", "{FileName}", "Nombre del archivo"),
                new("FileNameWithoutExtension", "{FileNameWithoutExtension}", "Nombre sin extensión"),
                new("Extension", "{Extension}", "Extensión del archivo"),
                new("Date", "{Date}", "Fecha actual (yyyyMMdd)"),
                new("Year", "{Year}", "Año actual (yyyy)"),
                new("Month", "{Month}", "Mes actual (MM)"),
                new("Day", "{Day}", "Día actual (dd)"),
                new("Guid", "{Guid}", "Identificador GUID único"),
                new("Random", "{Random:4}", "Dígitos aleatorios")
            ]);
            vars = [sysGroup];
        }

        var cm = new ContextMenu();
        foreach (var group in vars)
        {
            var groupHeader = new MenuItem
            {
                Header = group.GroupName,
                IsEnabled = false,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.DimGray
            };
            cm.Items.Add(groupHeader);

            foreach (var v in group.Variables)
            {
                var mi = new MenuItem
                {
                    Header = $"{v.Token}  —  {v.Description}",
                    Tag = v.Token
                };
                mi.Click += (s, args) =>
                {
                    if (s is MenuItem clickedItem && clickedItem.Tag is string token)
                    {
                        InsertToken(token);
                    }
                };
                cm.Items.Add(mi);
            }

            cm.Items.Add(new Separator());
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
