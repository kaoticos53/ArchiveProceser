using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.Views.Components;

public partial class TextEditorDialogWindow : Window
{
    private readonly NodeParameterViewModel? _parameter;
    private readonly IVariableDiscoveryService _variableDiscoveryService;
    private readonly ILocalizationService _loc;

    public TextEditorDialogViewModel ViewModel { get; }

    public string ResultText => ViewModel.ResultText;

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

        ViewModel = new TextEditorDialogViewModel(title, initialText, parameter, _variableDiscoveryService, _loc);
        DataContext = ViewModel;

        TxtEditor.Focus();
        TxtEditor.CaretIndex = TxtEditor.Text.Length;
    }

    private void BtnVersionChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string token && !string.IsNullOrEmpty(token))
        {
            var (_, newCaret) = ViewModel.SelectVersionToken(TxtEditor.CaretIndex, token);
            TxtEditor.CaretIndex = Math.Min(newCaret, TxtEditor.Text.Length);
            TxtEditor.Focus();
        }
    }

    public void InsertVariableAtCaret(string token)
    {
        var (_, newCaret) = ViewModel.InsertTokenAt(TxtEditor.CaretIndex, token);
        TxtEditor.CaretIndex = Math.Min(newCaret, TxtEditor.Text.Length);
        TxtEditor.Focus();
    }

    private void BtnToggleSidePanel_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleSidePanel();
    }

    private void BtnCloseSidePanel_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseSidePanel();
        TxtEditor.Focus();
    }

    private void ListSideVariables_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListSideVariables.SelectedItem is VariableItem selected)
        {
            InsertVariableAtCaret(selected.Token);
        }
    }

    private void TxtEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel == null) return;
        ViewModel.UpdateStats(TxtEditor.LineCount);
        CheckIntelliSenseTrigger();
    }

    private void CheckIntelliSenseTrigger()
    {
        int openBrace = ViewModel.EvaluateIntelliSense(TxtEditor.CaretIndex);
        if (openBrace >= 0)
        {
            try
            {
                var rect = TxtEditor.GetRectFromCharacterIndex(openBrace);
                PopupIntelliSense.HorizontalOffset = Math.Max(0, rect.Left);
                PopupIntelliSense.VerticalOffset = rect.Bottom + 4;
            }
            catch
            {
                // En caso de fallo de medida de fuente, mantener posición por defecto
            }
        }
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
                ViewModel.IsIntelliSenseOpen = false;
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

        var (_, newCaret) = ViewModel.ApplyIntelliSenseSelection(TxtEditor.CaretIndex, selected);
        TxtEditor.CaretIndex = Math.Min(newCaret, TxtEditor.Text.Length);
        TxtEditor.Focus();
    }

    private void BtnInsertVar_Click(object sender, RoutedEventArgs e)
    {
        var previewContext = _variableDiscoveryService.CreatePreviewItem(_parameter?.NodeOwner);
        var groups = ViewModel.VariableGroups;

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
                InsertVariableAtCaret(dialog.SelectedToken);
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
                        InsertVariableAtCaret(token);
                    }
                };
                subMenu.Items.Add(mi);
            }
            cm.Items.Add(subMenu);
        }

        cm.PlacementTarget = BtnInsertVar;
        cm.IsOpen = true;
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CopyText();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearText();
        TxtEditor.Focus();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveAndClose();
    }

    private void SaveAndClose()
    {
        ViewModel.SaveResult();
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
