using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileFlow.App.Preview.Core;
using FileFlow.App.Preview.ViewModels;

namespace FileFlow.App.Preview.Views;

public class AddOneConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i) return i + 1;
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public partial class FilePreviewerWindow : Window
{
    public FilePreviewerViewModel ViewModel { get; }

    public FilePreviewerWindow()
    {
        InitializeComponent();
        ViewModel = new FilePreviewerViewModel();
        DataContext = ViewModel;
        KeyDown += Window_KeyDown;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public async Task ShowPreviewAsync(FilePreviewContext context, IEnumerable<FilePreviewContext>? siblings = null, Window? owner = null)
    {
        await ViewModel.LoadContextAsync(context, siblings).ConfigureAwait(true);
        if (owner != null)
        {
            Show(owner);
        }
        else
        {
            Show();
        }
        Activate();
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
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

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
