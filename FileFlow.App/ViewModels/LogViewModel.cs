using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Collections;
using FileFlow.App.Services;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Serialization;
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

public partial class LogViewModel : ObservableObject, IDisposable
{
    private const int MaxLiveBufferSize = 2000;
    public FastObservableRingBuffer<StructuredLogRecord> Logs { get; } = new(MaxLiveBufferSize);

    private readonly ILogStore _logStore;
    private readonly ILocalizationService _loc;
    private readonly IDialogService _dialogService;

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
    private readonly EventHandler<CultureInfo> _languageChangedHandler;

    private volatile bool _isClearingLogs;

    public LogViewModel(
        ILogStore? logStore = null,
        ILocalizationService? localizationService = null,
        IDialogService? dialogService = null)
    {
        _logStore = logStore ?? SqliteLogStore.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;
        _dialogService = dialogService ?? AvaloniaDialogService.Instance;

        _statusMessage = _loc["StatusReady"];

        // Handler guardado (no lambda anónima eterna): Dispose debe poder desuscribirlo. El handler
        // escribe StatusMessage (propiedad observable, segura desde cualquier hilo), pero igual que
        // NodeParameterViewModel/ToolboxViewModel, la desuscripción determinista evita que una instancia
        // efímera deje un suscriptor vivo en el singleton de localización para siempre.
        _languageChangedHandler = (_, _) =>
        {
            if (ProgressPercentage == 0)
            {
                StatusMessage = _loc["StatusReady"];
            }
        };
        _loc.LanguageChanged += _languageChangedHandler;

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
        _logStore.EnqueueLog(record);
    }

    public void AddStructuredLog(StructuredLogRecord record)
    {
        _pendingLogs.Enqueue(record);
    }

    private void FlushPendingLogs()
    {
        if (_isClearingLogs || _pendingLogs.IsEmpty) return;

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

        if (batch.Count > 0 && !_isClearingLogs)
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
        if (_isClearingLogs) return;
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
        if (_isClearingLogs) return;

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
        if (_isClearingLogs) return;

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
        if (_isClearingLogs) return;

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
        if (_isClearingLogs) return;

        try
        {
            FlushAllPendingLogs();
            await _logStore.FlushPendingLogsAsync().ConfigureAwait(false);
            if (_isClearingLogs) return;

            int total = await _logStore.GetTotalCountAsync().ConfigureAwait(false);
            int offset = Math.Max(0, total - MaxLiveBufferSize);
            var results = await _logStore.GetLogsWindowAsync(offset, MaxLiveBufferSize, newestFirst: false).ConfigureAwait(false);

            if (_isClearingLogs) return;

            await RunOnUiAsync(() =>
            {
                if (_isClearingLogs) return;
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
        if (_isClearingLogs) return;

        try
        {
            FlushAllPendingLogs();
            await _logStore.FlushPendingLogsAsync().ConfigureAwait(false);
            if (_isClearingLogs) return;

            IReadOnlyList<StructuredLogRecord> queryResults;

            if (ActiveFilter == LogFilterLevel.All && string.IsNullOrWhiteSpace(SearchFilter) && (string.IsNullOrEmpty(SortColumn) || SortColumn == "Id") && IsSortAscending)
            {
                int total = await _logStore.GetTotalCountAsync().ConfigureAwait(false);
                int offset = Math.Max(0, total - MaxLiveBufferSize);
                queryResults = await _logStore.GetLogsWindowAsync(offset, MaxLiveBufferSize, newestFirst: false).ConfigureAwait(false);
            }
            else
            {
                var filter = BuildCurrentFilter();
                queryResults = await _logStore.GetLogsWindowAsync(0, MaxLiveBufferSize, filter).ConfigureAwait(false);
            }

            if (_isClearingLogs) return;

            await RunOnUiAsync(() =>
            {
                if (_isClearingLogs) return;
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

    private Task RunOnUiAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task SortBy(string columnName)
    {
        if (_isClearingLogs) return;

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
        ProgressPercentage = percentage;
        StatusMessage = statusMessage;
    }

    [RelayCommand]
    public void SetFilter(string filterName)
    {
        if (_isClearingLogs) return;

        IsLiveMode = true;
        SearchFilter = string.Empty;

        switch (filterName)
        {
            case "All":
                ActiveFilter = LogFilterLevel.All;
                break;
            case "Errors":
                ActiveFilter = LogFilterLevel.ErrorsOnly;
                break;
            case "Warnings":
                ActiveFilter = LogFilterLevel.WarningsOnly;
                break;
            case "Info":
                ActiveFilter = LogFilterLevel.InfoOnly;
                break;
            case "Debug":
                ActiveFilter = LogFilterLevel.DebugOnly;
                break;
            default:
                ActiveFilter = LogFilterLevel.All;
                break;
        }

        _ = LoadQueryResultsAsync();
        OnFilterChanged?.Invoke();
    }

    [RelayCommand]
    public void ClearSearchFilter()
    {
        if (_isClearingLogs) return;
        SearchFilter = string.Empty;
    }

    [RelayCommand]
    public async Task ClearLogs()
    {
        if (_isClearingLogs) return;
        _isClearingLogs = true;

        try
        {
            Logs.Clear();
            await _logStore.ClearAsync();
            ErrorCount = 0;
            WarningCount = 0;
            InfoCount = 0;
            DebugCount = 0;
            TotalLogsCount = 0;
            SelectedLog = null;
            OnLogsCleared?.Invoke();
        }
        finally
        {
            _isClearingLogs = false;
        }
    }

    public async Task SearchAsync(string filter)
    {
        if (_isClearingLogs) return;
        SearchFilter = filter;
        if (string.IsNullOrWhiteSpace(filter))
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

    public async Task FilterByNodeName(string? nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName) || _isClearingLogs) return;
        IsLiveMode = false;
        SearchFilter = nodeName.Trim();
        await LoadQueryResultsAsync();
    }

    [RelayCommand]
    public async Task FilterByItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || _isClearingLogs) return;
        IsLiveMode = false;
        SearchFilter = itemId.Trim();
        await LoadQueryResultsAsync();
    }

    public static void SafeSetClipboardText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var topLevel = App.MainWindow != null ? Avalonia.Controls.TopLevel.GetTopLevel(App.MainWindow) : null;
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(text);
                }
            }
            catch { }
        });
    }

    [RelayCommand]
    public void CopyFullLogLine(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null) return;
        SafeSetClipboardText(target.FormattedLine);
    }

    [RelayCommand]
    public void CopyLogMessage(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null || string.IsNullOrWhiteSpace(target.Message)) return;
        SafeSetClipboardText(target.Message);
    }

    [RelayCommand]
    public void CopyLogFilePath(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null || string.IsNullOrWhiteSpace(target.FilePath)) return;
        SafeSetClipboardText(target.FilePath);
    }

    [RelayCommand]
    public void CopyLogFileName(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null || string.IsNullOrWhiteSpace(target.FileName)) return;
        SafeSetClipboardText(target.FileName);
    }

    [RelayCommand]
    public void CopyLogItemId(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null || string.IsNullOrWhiteSpace(target.ItemId)) return;
        SafeSetClipboardText(target.ItemId);
    }

    [RelayCommand]
    public void CopyLogDetailsJson(StructuredLogRecord? log = null)
    {
        var target = log ?? SelectedLog;
        if (target == null || string.IsNullOrWhiteSpace(target.DetailsJson)) return;
        SafeSetClipboardText(target.DisplayDetails);
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
        SafeSetClipboardText(JsonDefaults.FormatDetailsForDisplay(json));
    }

    [RelayCommand]
    public void CopyText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        SafeSetClipboardText(text);
    }

    [RelayCommand]
    public void PreviewLogFile(StructuredLogRecord? log)
    {
        var targetLog = log ?? SelectedLog;
        if (targetLog == null) return;

        string? filePath = targetLog.FilePath;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            string noFileMsg = _loc.GetString("Preview_NoAssociatedFile", "No se encontró ningún archivo físico asociado a esta línea de log para previsualizar.");
            string title = _loc.GetString("Node_PreviewButton", "Vista Previa");
            _dialogService.ShowInformation(noFileMsg, title);
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
        _ = win.ShowPreviewAsync(ctx, owner: App.MainWindow);
    }

    [RelayCommand]
    public async Task ExportLogs()
    {
        if (TotalLogsCount == 0) return;

        string? exportedPath = await LogExportService.ExportLogsWithDialogAsync();
        if (!string.IsNullOrEmpty(exportedPath))
        {
            AddLog(LogLevel.Information, FileFlow.Sdk.Localization.LocalizationManager.Instance.GetFormattedString("Log_ExportSuccess", "Log exportado exitosamente en: {0}", exportedPath));
        }
    }

    public void Dispose()
    {
        _flushTimer.Stop();
        _loc.LanguageChanged -= _languageChangedHandler;
    }
}
