using System.Collections.ObjectModel;
using System.IO;
using System.Text;
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

    [ObservableProperty]
    private string _fullLogText = string.Empty;

    private readonly StringBuilder _logTextBuffer = new();

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
            var entry = new LogEntry(DateTime.Now, level, message);
            Logs.Add(entry);
            if (Logs.Count > 1000)
            {
                Logs.RemoveAt(0);
            }

            _logTextBuffer.AppendLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}");
            FullLogText = _logTextBuffer.ToString();
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
        _logTextBuffer.Clear();
        FullLogText = string.Empty;
        ProgressPercentage = 0;
        StatusMessage = LocalizationManager.Instance["StatusReady"];
    }

    [RelayCommand]
    public void ExportLogs()
    {
        if (Logs.Count == 0) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Archivos de Log (*.log;*.txt)|*.log;*.txt|Todos los archivos (*.*)|*.*",
            DefaultExt = ".log",
            FileName = $"fileflow_execution_{DateTime.Now:yyyyMMdd_HHmmss}.log"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var lines = Logs.Select(l => $"[{l.Timestamp:yyyy-MM-dd HH:mm:ss}] [{l.Level}] {l.Message}");
                File.WriteAllLines(dialog.FileName, lines);
                AddLog(LogLevel.Information, $"Log exportado exitosamente en: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar el log: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
