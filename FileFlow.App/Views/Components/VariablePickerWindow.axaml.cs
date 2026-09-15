using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FileFlow.App.Models;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.Views.Components;

public partial class VariablePickerWindow : Window
{
    public VariablePickerViewModel? ViewModel => DataContext as VariablePickerViewModel;

    public string? SelectedToken { get; private set; }

    public VariablePickerWindow()
    {
        InitializeComponent();
    }

    public VariablePickerWindow(
        IEnumerable<VariableGroupItem> groups,
        NodeViewModel? targetNode = null,
        FileItemContext? previewContext = null,
        ILocalizationService? localizationService = null) : this()
    {
        var vm = new VariablePickerViewModel(groups, targetNode, previewContext, localizationService);
        DataContext = vm;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
