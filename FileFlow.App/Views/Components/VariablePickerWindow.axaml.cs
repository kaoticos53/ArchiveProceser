using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileFlow.App.Models;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.Views.Components;

public partial class VariablePickerWindow : Window
{
    public VariablePickerViewModel? ViewModel => DataContext as VariablePickerViewModel;

    public string? SelectedToken => ViewModel?.SelectedToken;

    public VariablePickerWindow() : this(groups: null)
    {
    }

    public VariablePickerWindow(
        IEnumerable<VariableGroupItem>? groups,
        NodeViewModel? targetNode = null,
        FileItemContext? previewContext = null,
        ILocalizationService? localizationService = null)
    {
        InitializeComponent();

        var vm = new VariablePickerViewModel(groups, targetNode, previewContext, localizationService);
        DataContext = vm;

        vm.RequestClose += (s, result) =>
        {
            Close(result);
        };
    }

    private void Row_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel?.SelectedVariable != null)
        {
            ViewModel.InsertSelected();
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
