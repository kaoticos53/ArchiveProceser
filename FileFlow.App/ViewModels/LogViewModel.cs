using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public enum LogFilterLevel
{
    All,
    ErrorsOnly,
    WarningsOnly,
    InfoOnly
}

public partial class LogViewModel : ObservableObject
{
    public ObservableCollection<LogEntry> Logs { get; } = [];

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _fullLogText = string.Empty;

    [ObservableProperty]
    private LogFilterLevel _activeFilter = LogFilterLevel.All;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _infoCount;

    public event Action<LogEntry>? OnLogAdded;
    public event Action? OnLogsCleared;
    public event Action? OnFilterChanged;

    private readonly StringBuilder _logTextBuffer = new();
    private readonly ConcurrentQueue<LogEntry> _pendingLogs = new();
    private readonly DispatcherTimer _flushTimer;

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

        _flushTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _flushTimer.Tick += FlushPendingLogs;
        _flushTimer.Start();
    }

    public void AddLog(LogLevel level, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, message);
        _pendingLogs.Enqueue(entry);
    }

    private void FlushPendingLogs(object? sender, EventArgs? e)
    {
        if (_pendingLogs.IsEmpty) return;

        int maxLogs = Services.UserPreferencesService.Instance.Preferences.MaxLogEntries;
        if (maxLogs <= 0) maxLogs = 100000;

        bool addedAny = false;
        while (_pendingLogs.TryDequeue(out var entry))
        {
            Logs.Add(entry);
            if (Logs.Count > maxLogs)
            {
                Logs.RemoveAt(0);
            }

            if (entry.Level == LogLevel.Error || entry.Level == LogLevel.Critical) ErrorCount++;
            else if (entry.Level == LogLevel.Warning) WarningCount++;
            else if (entry.Level == LogLevel.Information) InfoCount++;

            _logTextBuffer.AppendLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}");
            addedAny = true;

            OnLogAdded?.Invoke(entry);
        }

        if (addedAny)
        {
            FullLogText = _logTextBuffer.ToString();
        }
    }

    public void UpdateProgress(double percentage, string statusMessage)
    {
        if (Application.Current != null)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ProgressPercentage = percentage;
                StatusMessage = statusMessage;
            }, DispatcherPriority.Background);
        }
    }

    [RelayCommand]
    public void SetFilter(string filterName)
    {
        ActiveFilter = filterName.ToLowerInvariant() switch
        {
            "errors" => LogFilterLevel.ErrorsOnly,
            "warnings" => LogFilterLevel.WarningsOnly,
            "info" => LogFilterLevel.InfoOnly,
            _ => LogFilterLevel.All
        };
        OnFilterChanged?.Invoke();
    }

    [RelayCommand]
    public void ClearLogs()
    {
        while (_pendingLogs.TryDequeue(out _)) { }
        Logs.Clear();
        _logTextBuffer.Clear();
        FullLogText = string.Empty;
        ErrorCount = 0;
        WarningCount = 0;
        InfoCount = 0;
        ProgressPercentage = 0;
        StatusMessage = LocalizationManager.Instance["StatusReady"];
        OnLogsCleared?.Invoke();
    }

    [RelayCommand]
    public void ExportLogs()
    {
        FlushPendingLogs(null, null);
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
