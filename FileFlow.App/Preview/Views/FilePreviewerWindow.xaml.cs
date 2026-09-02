using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using FileFlow.App.Preview.Core;
using FileFlow.App.Preview.ViewModels;

namespace FileFlow.App.Preview.Views;

public class AddOneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i) return i + 1;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw exoticException();
    private static NotImplementedException exoticException() => new();
}

public partial class FilePreviewerWindow : Window
{
    public FilePreviewerViewModel ViewModel { get; }

    public FilePreviewerWindow()
    {
        InitializeComponent();
        ViewModel = new FilePreviewerViewModel();
        DataContext = ViewModel;

        if (!Resources.Contains("AddOneConverter"))
        {
            Resources.Add("AddOneConverter", new AddOneConverter());
        }
    }

    public async Task ShowPreviewAsync(FilePreviewContext context, IEnumerable<FilePreviewContext>? siblings = null, Window? owner = null)
    {
        if (owner != null) Owner = owner;
        await ViewModel.LoadContextAsync(context, siblings).ConfigureAwait(true);
        Show();
        Activate();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || e.Key == Key.Space)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Left && ViewModel.CanNavigatePrevious)
        {
            _ = ViewModel.NavigatePreviousAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Right && ViewModel.CanNavigateNext)
        {
            _ = ViewModel.NavigateNextAsync();
            e.Handled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
