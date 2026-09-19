using Avalonia.Controls;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class UpdateDialogWindow : Window
{
    public UpdateDialogWindow()
    {
        InitializeComponent();
    }

    public UpdateDialogWindow(UpdateDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += success => Close(success);
    }
}

