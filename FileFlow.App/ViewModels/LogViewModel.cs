using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public partial class LogViewModel : ObservableObject
{
    public ObservableCollection<LogEntry> Logs { get; } = [];

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public LogViewModel()
    {
        _statusMessage = LocalizationManager.Instance["StatusReady"];
        LocalizationManager.Instance.LanguageChanged += (_, _) =>
        {
            if (ProgressPercentage == 0)
            {
                StatusMessage = LocalizationManager.Instance["StatusReady"];
            }
        };
    }

    public void AddLog(LogLevel level, string message)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            Logs.Add(new LogEntry(DateTime.Now, level, message));
            if (Logs.Count > 1000)
            {
                Logs.RemoveAt(0);
            }
        });
    }

    public void UpdateProgress(double percentage, string statusMessage)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            ProgressPercentage = percentage;
            StatusMessage = statusMessage;
        });
    }

    [RelayCommand]
    public void ClearLogs()
    {
        Logs.Clear();
        ProgressPercentage = 0;
        StatusMessage = LocalizationManager.Instance["StatusReady"];
    }
}
