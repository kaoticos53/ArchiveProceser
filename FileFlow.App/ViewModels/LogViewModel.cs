using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading;
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
using FileFlow.Sdk.Services;
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

    /// <summary>
    /// El periodo del latido de la consola. Público para que la prueba de cadencia mida <i>este</i> número: un
    /// periodo que sólo vive dentro del constructor no se puede afirmar sin repetir el número en la prueba.
    /// </summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(40);

    /// <summary>Nombre del latido en el registro: con él se busca, se mide su cadencia y se sabe cuál falló.</summary>
    public const string ConsoleFlushBeat = "console-flush";

    private readonly ConcurrentQueue<StructuredLogRecord> _pendingLogs = new();
    private readonly IHeartbeat _flushBeat;
    private readonly EventHandler<CultureInfo> _languageChangedHandler;

    private volatile bool _isClearingLogs;

    /// <summary>
    /// <paramref name="timeProvider"/> y <paramref name="uiDispatcher"/> existen para poder medir el latido: el
    /// periodo se comprueba con un reloj manual (sin esperar 40 ms reales por caso) y el tick, que ahora lo
    /// entrega el reloj desde un hilo del grupo de hilos, se despacha al hilo de la interfaz. Por defecto, el
    /// reloj del sistema y el despacho seguro del host (en línea cuando no hay aplicación).
    /// </summary>
    public LogViewModel(
        ILogStore? logStore = null,
        ILocalizationService? localizationService = null,
        IDialogService? dialogService = null,
        TimeProvider? timeProvider = null,
        IUiDispatcher? uiDispatcher = null,
        IHeartbeatService? heartbeats = null)
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

        // El latido llama al <b>mismo</b> método público que las pruebas (FlushAllPendingLogs), no al privado:
        // así lo que se ejercita desde el suite es exactamente lo que corre en la aplicación, y el latido deja
        // de ser un camino propio sin cubrir (los tests vaciaban a mano y el diferido no corría nunca).
        //
        // El resto —reloj inyectable, despacho al hilo de la interfaz y entrega protegida— lo pone el registro.
        IUiDispatcher ui = uiDispatcher ?? AvaloniaUiDispatcher.Instance;
        TimeProvider clock = timeProvider ?? TimeProvider.System;

        _flushBeat = (heartbeats ?? new HeartbeatService(clock, ui))
            .Declare(ConsoleFlushBeat, FlushInterval, FlushAllPendingLogs)
            .Start();
    }

    public void AddLog(LogLevel level, string message) => AddNodeLog(level, message, nodeId: null, nodeName: null);

    /// <summary>
    /// Lo mismo, pero con el nodo al que se refiere el mensaje: la fila de la consola queda atada a un nodo del
    /// lienzo, y seleccionarla abre ese nodo en el inspector (ver <see cref="NodeInspectorViewModel.InspectLogRecord"/>).
    ///
    /// El identificador tiene que ser el del nodo <b>tal y como existe en el lienzo</b>: uno que no esté se
    /// ignora al seleccionar la fila —y si el nombre coincide con otro nodo, abre el que no es—, así que un
    /// mensaje sobre algo que no está se cuenta sin nodo, con el nombre en el propio texto.
    /// </summary>
    public void AddNodeLog(LogLevel level, string message, string? nodeId, string? nodeName)
    {
        var record = StructuredLogRecord.Create(
            executionId: string.Empty,
            level: level,
            message: message,
            nodeId: nodeId,
            nodeName: nodeName
        );
        _pendingLogs.Enqueue(record);
        _logStore.EnqueueLog(record);
    }

    public void AddStructuredLog(StructuredLogRecord record)
    {
        _pendingLogs.Enqueue(record);
    }

    /// <summary>
    /// Vacía la cola acumulada hacia la lista visible: es el <b>latido</b> de la consola (cada 40 ms).
    ///
    /// <para>Existe para que los productores de registros no toquen la interfaz: encolan y el latido decide
    /// cuándo aparecen, agrupados en un solo lote y con los contadores sumados una sola vez. Público porque es
    /// el paso del latido —el temporizador y la petición explícita del cierre de una ejecución llaman al mismo
    /// sitio— y las pruebas necesitan ejercitar el camino <b>diferido</b>: hasta ahora todas vaciaban a mano y
    /// nadie comprobaba que los registros aparezcan solos.</para>
    /// </summary>
    public void FlushAllPendingLogs()
    {
        if (_isClearingLogs) return;
        FlushPendingLogs();
    }

    /// <summary>Cuerpo del latido: drena la cola y publica el lote en la lista visible.</summary>
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
        _flushBeat.Dispose();
        _loc.LanguageChanged -= _languageChangedHandler;
    }
}
