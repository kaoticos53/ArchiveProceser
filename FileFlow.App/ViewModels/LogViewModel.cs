using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.App.ViewModels;

public enum LogFilterLevel
{
    All,
    ErrorsOnly,
    WarningsOnly,
    InfoOnly,
    DebugOnly
}

public partial class LogViewModel : ObservableObject
{
    private const int MaxLiveBufferSize = 2000;
    public ObservableCollection<StructuredLogRecord> Logs { get; } = [];

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private LogFilterLevel _activeFilter = LogFilterLevel.All;

    [ObservableProperty]
    private bool _isLiveMode = true;

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private string _sortColumn = "Id";

    [ObservableProperty]
    private bool _isSortAscending = true;

    [ObservableProperty]
    private int _totalLogsCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _infoCount;

    [ObservableProperty]
    private int _debugCount;

    public event Action? OnLogBatchAdded;
    public event Action? OnLogsCleared;
    public event Action? OnFilterChanged;

    private readonly ConcurrentQueue<StructuredLogRecord> _pendingLogs = new();
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
        _flushTimer.Tick += (_, _) => FlushPendingLogs();
        _flushTimer.Start();
    }

    public void AddLog(LogLevel level, string message)
    {
        var record = StructuredLogRecord.Create(
            executionId: string.Empty,
            level: level,
            message: message
        );
        _pendingLogs.Enqueue(record);
        SqliteLogStore.Instance.EnqueueLog(record);
    }

    public void AddStructuredLog(StructuredLogRecord record)
    {
        _pendingLogs.Enqueue(record);
    }

    private void FlushPendingLogs()
    {
        if (_pendingLogs.IsEmpty) return;

        int count = _pendingLogs.Count;
        var batch = new List<StructuredLogRecord>(count);
        int errs = 0, warns = 0, infos = 0, dbgs = 0;

        while (_pendingLogs.TryDequeue(out var entry))
        {
            batch.Add(entry);
            if (entry.Level is LogLevel.Error or LogLevel.Critical) errs++;
            else if (entry.Level == LogLevel.Warning) warns++;
            else if (entry.Level == LogLevel.Information) infos++;
            else if (entry.Level == LogLevel.Debug) dbgs++;
        }

        if (batch.Count > 0)
        {
            ErrorCount += errs;
            WarningCount += warns;
            InfoCount += infos;
            DebugCount += dbgs;
            TotalLogsCount += batch.Count;

            if (IsLiveMode && string.IsNullOrWhiteSpace(SearchFilter) && ActiveFilter == LogFilterLevel.All && SortColumn == "Id")
            {
                foreach (var item in batch)
                {
                    Logs.Add(item);
                }

                while (Logs.Count > MaxLiveBufferSize)
                {
                    Logs.RemoveAt(0);
                }

                OnLogBatchAdded?.Invoke();
            }
        }
    }

    public void FlushAllPendingLogs()
    {
        if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(FlushPendingLogs);
            return;
        }

        FlushPendingLogs();
    }

    private LogFilterCriteria BuildCurrentFilter()
    {
        LogLevel? minLevel = null;
        LogLevel? exactLevel = null;

        switch (ActiveFilter)
        {
            case LogFilterLevel.ErrorsOnly:
                minLevel = LogLevel.Error;
                break;
            case LogFilterLevel.WarningsOnly:
                exactLevel = LogLevel.Warning;
                break;
            case LogFilterLevel.InfoOnly:
                exactLevel = LogLevel.Information;
                break;
            case LogFilterLevel.DebugOnly:
                exactLevel = LogLevel.Debug;
                break;
        }

        string? search = !string.IsNullOrWhiteSpace(SearchFilter) ? SearchFilter.Trim() : null;

        return new LogFilterCriteria(
            MinLevel: minLevel,
            ExactLevel: exactLevel,
            SearchText: search,
            SortColumn: SortColumn,
            IsAscending: IsSortAscending
        );
    }

    async partial void OnActiveFilterChanged(LogFilterLevel value)
    {
        if (value != LogFilterLevel.All)
        {
            IsLiveMode = false;
        }
        await LoadQueryResultsAsync();
        OnFilterChanged?.Invoke();
    }

    async partial void OnSearchFilterChanged(string value)
    {
        await LoadQueryResultsAsync();
        OnFilterChanged?.Invoke();
    }

    async partial void OnIsLiveModeChanged(bool value)
    {
        if (value)
        {
            ActiveFilter = LogFilterLevel.All;
            SearchFilter = string.Empty;
            SortColumn = "Id";
            IsSortAscending = true;
            await LoadRecentLiveLogsAsync();
        }
    }

    private async Task LoadRecentLiveLogsAsync()
    {
        try
        {
            await SqliteLogStore.Instance.FlushPendingLogsAsync().ConfigureAwait(false);
            int total = await SqliteLogStore.Instance.GetTotalCountAsync().ConfigureAwait(false);
            int offset = Math.Max(0, total - MaxLiveBufferSize);
            var results = await SqliteLogStore.Instance.GetLogsWindowAsync(offset, MaxLiveBufferSize, newestFirst: false).ConfigureAwait(false);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Logs.Clear();
                foreach (var item in results)
                {
                    Logs.Add(item);
                }
                OnLogBatchAdded?.Invoke();
            });
        }
        catch
        {
            // Resiliente
        }
    }

    public async Task LoadQueryResultsAsync()
    {
        try
        {
            await SqliteLogStore.Instance.FlushPendingLogsAsync().ConfigureAwait(false);
            var filter = BuildCurrentFilter();
            var results = await SqliteLogStore.Instance.GetLogsWindowAsync(0, 1000, filter).ConfigureAwait(false);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Logs.Clear();
                foreach (var item in results)
                {
                    Logs.Add(item);
                }
                OnFilterChanged?.Invoke();
            });
        }
        catch
        {
            // Resiliente
        }
    }

    [RelayCommand]
    public async Task SortBy(string columnName)
    {
        IsLiveMode = false;
        if (SortColumn.Equals(columnName, StringComparison.OrdinalIgnoreCase))
        {
            IsSortAscending = !IsSortAscending;
        }
        else
        {
            SortColumn = columnName;
            IsSortAscending = true;
        }

        await LoadQueryResultsAsync();
        OnFilterChanged?.Invoke();
    }

    public void ReportProgress(double percentage, string statusMessage)
    {
        if (Application.Current != null)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ProgressPercentage = percentage;
                StatusMessage = statusMessage;
            }, DispatcherPriority.Normal);
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
            "debug" => LogFilterLevel.DebugOnly,
            _ => LogFilterLevel.All
        };
    }

    [RelayCommand]
    public void ClearSearchFilter()
    {
        SearchFilter = string.Empty;
    }

    [RelayCommand]
    public async Task ClearLogs()
    {
        while (_pendingLogs.TryDequeue(out _)) { }
        Logs.Clear();
        ErrorCount = 0;
        WarningCount = 0;
        InfoCount = 0;
        DebugCount = 0;
        TotalLogsCount = 0;
        ProgressPercentage = 0;
        StatusMessage = LocalizationManager.Instance["StatusReady"];

        await SqliteLogStore.Instance.ClearAsync().ConfigureAwait(false);

        OnLogsCleared?.Invoke();
    }

    [ObservableProperty]
    private StructuredLogRecord? _selectedLog;

    [RelayCommand]
    public async Task FilterByItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        IsLiveMode = false;
        SearchFilter = itemId.Trim();
        await LoadQueryResultsAsync();
    }

    [RelayCommand]
    public void CopyDetailsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            Clipboard.SetText(json);
        }
        catch { }
    }

    [RelayCommand]
    public void CopyText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            Clipboard.SetText(text);
        }
        catch { }
    }

    [RelayCommand]
    public async Task ExportLogs()
    {
        if (TotalLogsCount == 0) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Archivos de Log (*.log;*.txt)|*.log;*.txt|Todos los archivos (*.*)|*.*",
            DefaultExt = ".log",
            FileName = $"fileflow_execution_{DateTime.Now:yyyyMMdd_HHmmss}.log"
        };

        if (dialog.ShowDialog() == true)
        {
            string targetPath = dialog.FileName;
            try
            {
                await Task.Run(async () =>
                {
                    await SqliteLogStore.Instance.FlushPendingLogsAsync().ConfigureAwait(false);
                    await using var writer = new StreamWriter(targetPath);
                    await SqliteLogStore.Instance.ExportLogsAsync(writer).ConfigureAwait(false);
                }).ConfigureAwait(false);

                AddLog(LogLevel.Information, $"Log exportado exitosamente en: {targetPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar el log: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
