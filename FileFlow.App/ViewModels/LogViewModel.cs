using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Collections;
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
    public FastObservableRingBuffer<StructuredLogRecord> Logs { get; } = new(MaxLiveBufferSize);

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

    [ObservableProperty]
    private StructuredLogRecord? _selectedLog;

    public event Action<StructuredLogRecord?>? LogSelectionChanged;

    partial void OnSelectedLogChanged(StructuredLogRecord? value)
    {
        LogSelectionChanged?.Invoke(value);
    }

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
        SqliteLogStore.Instance.EnqueueLog(record);
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

            if (IsLiveMode && string.IsNullOrWhiteSpace(SearchFilter) && ActiveFilter == LogFilterLevel.All && (string.IsNullOrEmpty(SortColumn) || SortColumn == "Id") && IsSortAscending)
            {
                Logs.AddRange(batch);
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
        if (value == LogFilterLevel.All && string.IsNullOrWhiteSpace(SearchFilter) && (string.IsNullOrEmpty(SortColumn) || SortColumn == "Id") && IsSortAscending)
        {
            IsLiveMode = true;
        }
        else
        {
            IsLiveMode = false;
        }
        await LoadQueryResultsAsync();
        OnFilterChanged?.Invoke();
    }

    async partial void OnSearchFilterChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) && ActiveFilter == LogFilterLevel.All && (string.IsNullOrEmpty(SortColumn) || SortColumn == "Id") && IsSortAscending)
        {
            IsLiveMode = true;
        }
        else
        {
            IsLiveMode = false;
        }
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
            FlushAllPendingLogs();
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
            FlushAllPendingLogs();
            await SqliteLogStore.Instance.FlushPendingLogsAsync().ConfigureAwait(false);

            IReadOnlyList<StructuredLogRecord> queryResults;

            if (ActiveFilter == LogFilterLevel.All && string.IsNullOrWhiteSpace(SearchFilter) && (string.IsNullOrEmpty(SortColumn) || SortColumn == "Id") && IsSortAscending)
            {
                int total = await SqliteLogStore.Instance.GetTotalCountAsync().ConfigureAwait(false);
                int offset = Math.Max(0, total - MaxLiveBufferSize);
                queryResults = await SqliteLogStore.Instance.GetLogsWindowAsync(offset, MaxLiveBufferSize, newestFirst: false).ConfigureAwait(false);
            }
            else
            {
                var filter = BuildCurrentFilter();
                queryResults = await SqliteLogStore.Instance.GetLogsWindowAsync(0, MaxLiveBufferSize, filter).ConfigureAwait(false);
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Logs.Clear();
                foreach (var item in queryResults)
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
        IsLiveMode = true;
        ActiveFilter = LogFilterLevel.All;
        SearchFilter = string.Empty;
        SortColumn = "Id";
        IsSortAscending = true;

        await SqliteLogStore.Instance.ClearAsync().ConfigureAwait(false);

        OnLogsCleared?.Invoke();
    }

    [RelayCommand]
    public async Task FilterByItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        IsLiveMode = false;
        SearchFilter = itemId.Trim();
        await LoadQueryResultsAsync();
    }

    [RelayCommand]
    public void CopyFullLogLine(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null) return;
        try
        {
            Clipboard.SetText(target.FormattedLine);
        }
        catch { }
    }

    [RelayCommand]
    public void CopyLogMessage(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null || string.IsNullOrWhiteSpace(target.Message)) return;
        try
        {
            Clipboard.SetText(target.Message);
        }
        catch { }
    }

    [RelayCommand]
    public void CopyLogFilePath(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null || string.IsNullOrWhiteSpace(target.FilePath)) return;
        try
        {
            Clipboard.SetText(target.FilePath);
        }
        catch { }
    }

    [RelayCommand]
    public void CopyLogFileName(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null || string.IsNullOrWhiteSpace(target.FileName)) return;
        try
        {
            Clipboard.SetText(target.FileName);
        }
        catch { }
    }

    [RelayCommand]
    public void CopyLogItemId(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null || string.IsNullOrWhiteSpace(target.ItemId)) return;
        try
        {
            Clipboard.SetText(target.ItemId);
        }
        catch { }
    }

    [RelayCommand]
    public void CopyLogDetailsJson(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null || string.IsNullOrWhiteSpace(target.DetailsJson)) return;
        try
        {
            Clipboard.SetText(target.DetailsJson);
        }
        catch { }
    }

    [RelayCommand]
    public void FilterByNode(string? nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName)) return;
        SearchFilter = nodeName.Trim();
    }

    [RelayCommand]
    public void FilterByFile(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        SearchFilter = fileName.Trim();
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
    public void PreviewLogFile(StructuredLogRecord? log)
    {
        var targetLog = log ?? SelectedLog;
        if (targetLog == null) return;

        string? filePath = targetLog.FilePath;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            MessageBox.Show("No se encontró ningún archivo físico asociado a esta línea de log para previsualizar.", "Vista Previa", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ctx = new FileFlow.App.Preview.Core.FilePreviewContext(filePath);

        if (!string.IsNullOrWhiteSpace(targetLog.DetailsJson))
        {
            try
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(targetLog.DetailsJson);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        if (kvp.Value is System.Text.Json.JsonElement je)
                        {
                            if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                                ctx.Metadata[kvp.Key] = je.GetString()!;
                            else if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out int intVal))
                                ctx.Metadata[kvp.Key] = intVal;
                            else if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetDouble(out double dblVal))
                                ctx.Metadata[kvp.Key] = dblVal;
                            else if (je.ValueKind == System.Text.Json.JsonValueKind.True || je.ValueKind == System.Text.Json.JsonValueKind.False)
                                ctx.Metadata[kvp.Key] = je.GetBoolean();
                            else
                                ctx.Metadata[kvp.Key] = je.GetRawText();
                        }
                        else
                        {
                            ctx.Metadata[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch { }
        }

        var win = new FileFlow.App.Preview.Views.FilePreviewerWindow();
        _ = win.ShowPreviewAsync(ctx, owner: Application.Current.MainWindow);
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
