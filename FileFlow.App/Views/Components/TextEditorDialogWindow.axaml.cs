using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
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

        _parameter = parameter;
        _variableDiscoveryService = variableDiscoveryService ?? VariableDiscoveryService.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;

        ViewModel = new TextEditorDialogViewModel(title, initialText, parameter, _variableDiscoveryService, _loc);
        DataContext = ViewModel;

        TxtContent.Text = initialText;
        TxtTitle.Text = title;
        TxtCharCount.Text = $"{initialText.Length} caracteres";
        TxtContent.TextChanged += (s, e) =>
        {
            int len = TxtContent.Text?.Length ?? 0;
            TxtCharCount.Text = $"{len} caracteres";
        };
    }

    private void BtnInsertVar_Click(object? sender, RoutedEventArgs e)
    {
        var groups = _parameter?.AvailableVariables;
        if (groups == null || groups.Count == 0)
        {
            var editor = _parameter != null ? _parameter.NodeOwner?.ParentEditor : null;
            if (editor != null && _parameter?.NodeOwner != null)
            {
                groups = _variableDiscoveryService.GetAvailableVariables(_parameter.NodeOwner, editor.Connections);
            }
        }

        var previewContext = _parameter?.NodeOwner != null
            ? _variableDiscoveryService.CreatePreviewItem(_parameter.NodeOwner)
            : null;

        var picker = new VariablePickerWindow(groups ?? [], _parameter?.NodeOwner, previewContext, _loc);
        _ = picker.ShowDialog<bool>(this).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && t.Result && !string.IsNullOrEmpty(picker.SelectedToken))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    int caretIndex = TxtContent.CaretIndex;
                    string currentText = TxtContent.Text ?? string.Empty;
                    if (caretIndex >= 0 && caretIndex <= currentText.Length)
                    {
                        TxtContent.Text = currentText.Insert(caretIndex, picker.SelectedToken);
                        TxtContent.CaretIndex = caretIndex + picker.SelectedToken.Length;
                    }
                    else
                    {
                        TxtContent.Text = currentText + picker.SelectedToken;
                    }
                });
            }
        });
    }

    private async void BtnCopy_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtContent.Text))
        {
            await AvaloniaClipboardService.Instance.SetTextAsync(TxtContent.Text);
        }
    }

    private void BtnClear_Click(object? sender, RoutedEventArgs e)
    {
        TxtContent.Text = string.Empty;
    }

    private void BtnApply_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.Text = TxtContent.Text ?? string.Empty;
        ViewModel.SaveResult();
        Close(true);
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
