using System.Windows;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class AiModelDownloadDialog : Window
{
    public AiModelManagerViewModel ViewModel { get; }

    public AiModelDownloadDialog()
    {
        InitializeComponent();
        WindowThemeHelper.ApplyThemeToWindow(this);

        ViewModel = new AiModelManagerViewModel();
        DataContext = ViewModel;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
