using System.Windows;

namespace FileFlow.App.Services;

/// <summary>
/// Adaptador de infraestructura WPF para <see cref="IDialogService"/> utilizando <see cref="MessageBox"/>.
/// </summary>
public class WpfDialogService : IDialogService
{
    private static readonly Lazy<WpfDialogService> _instance = new(() => new WpfDialogService());
    public static WpfDialogService Instance => _instance.Value;

    public void ShowInformation(string message, string title = "FileFlow Studio")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string title = "FileFlow Studio")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(string message, string title = "Error")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool ShowConfirmation(string message, string title = "FileFlow Studio")
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public DialogResult ShowYesNoCancel(string message, string title = "FileFlow Studio")
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return result switch
        {
            MessageBoxResult.Yes => DialogResult.Yes,
            MessageBoxResult.No => DialogResult.No,
            _ => DialogResult.Cancel
        };
    }
}
