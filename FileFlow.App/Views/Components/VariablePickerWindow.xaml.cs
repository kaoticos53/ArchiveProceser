using System.Windows;
using System.Windows.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.Views.Components;

public partial class VariablePickerWindow : Window
{
    private readonly VariablePickerViewModel _viewModel;

    public string? SelectedToken => _viewModel.SelectedToken;
    public VariablePickerViewModel ViewModel => _viewModel;

    public VariablePickerWindow(
        IEnumerable<VariableGroupItem> groups,
        NodeViewModel? targetNode = null,
        FileItemContext? previewContext = null,
        ILocalizationService? localizationService = null)
        : this(new VariablePickerViewModel(groups, targetNode, previewContext, localizationService))
    {
    }

    public VariablePickerWindow(VariablePickerViewModel viewModel)
    {
        InitializeComponent();
        WindowThemeHelper.ApplyThemeToWindow(this);

        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += (_, _) =>
        {
            TxtSearch.Focus();
        };
    }

    private void ListVariables_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedVariable is VariableItem item)
        {
            ConfirmSelection(item.Token);
        }
    }

    private void ListVariables_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.SelectedVariable is VariableItem item)
        {
            ConfirmSelection(item.Token);
            e.Handled = true;
        }
    }

    private void BtnInsert_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedVariable is VariableItem item)
        {
            ConfirmSelection(item.Token);
        }
    }

    private void ConfirmSelection(string token)
    {
        _viewModel.SelectedToken = token;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
