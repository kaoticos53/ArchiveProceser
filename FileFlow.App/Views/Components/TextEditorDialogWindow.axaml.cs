using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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

    public TextEditorDialogWindow() : this("Editor", string.Empty)
    {
    }

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

        Opened += (_, _) =>
        {
            var txtEditor = this.FindControl<TextBox>("TxtEditor");
            if (txtEditor != null)
            {
                txtEditor.Focus();
                txtEditor.CaretIndex = txtEditor.Text?.Length ?? 0;
            }
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void BtnVersionChip_Click(object? sender, RoutedEventArgs e)
    {
        var txtEditor = this.FindControl<TextBox>("TxtEditor");
        if (sender is Button btn && btn.Tag is string token && !string.IsNullOrEmpty(token) && txtEditor != null)
        {
            var (_, newCaret) = ViewModel.SelectVersionToken(txtEditor.CaretIndex, token);
            txtEditor.CaretIndex = Math.Min(newCaret, txtEditor.Text?.Length ?? 0);
            txtEditor.Focus();
        }
    }

    public void InsertVariableAtCaret(string token)
    {
        var txtEditor = this.FindControl<TextBox>("TxtEditor");
        if (txtEditor != null)
        {
            var (_, newCaret) = ViewModel.InsertTokenAt(txtEditor.CaretIndex, token);
            txtEditor.CaretIndex = Math.Min(newCaret, txtEditor.Text?.Length ?? 0);
            txtEditor.Focus();
        }
    }

    private void BtnToggleSidePanel_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.ToggleSidePanel();
    }

    private void BtnCloseSidePanel_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.CloseSidePanel();
        var txtEditor = this.FindControl<TextBox>("TxtEditor");
        txtEditor?.Focus();
    }

    private void ListSideVariables_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var listSideVariables = this.FindControl<ListBox>("ListSideVariables");
        if (listSideVariables?.SelectedItem is VariableItem selected)
        {
            InsertVariableAtCaret(selected.Token);
        }
    }

    private void BtnAccept_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
