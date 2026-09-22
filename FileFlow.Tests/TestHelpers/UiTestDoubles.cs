using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Almacén de logs en memoria. Existe para que montar la consola de ejecución en una prueba no cree la base
/// de datos SQLite del usuario ni escriba en ella: la captura y el resto del test deben ser observadores
/// puros. Las consultas devuelven los registros encolados, así que también sirve para verificar la ingesta.
/// </summary>
public sealed class InMemoryLogStore : ILogStore
{
    private readonly List<StructuredLogRecord> _records = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<StructuredLogRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return _records.ToList();
            }
        }
    }

    public void EnqueueLog(StructuredLogRecord record)
    {
        lock (_gate)
        {
            _records.Add(record);
        }
    }

    public void EnqueueLogs(IEnumerable<StructuredLogRecord> records)
    {
        lock (_gate)
        {
            _records.AddRange(records);
        }
    }

    public Task FlushPendingLogsAsync() => Task.CompletedTask;

    public Task<IReadOnlyList<StructuredLogRecord>> GetLogsWindowAsync(
        int offset,
        int limit,
        LogFilterCriteria? filter = null,
        bool newestFirst = false)
    {
        IReadOnlyList<StructuredLogRecord> window = Records.Skip(offset).Take(limit).ToList();
        return Task.FromResult(window);
    }

    public Task<int> GetTotalCountAsync(LogFilterCriteria? filter = null) => Task.FromResult(Records.Count);

    public Task<IReadOnlyList<StructuredLogRecord>> GetFileTraceAsync(string fileNameOrPath) =>
        Task.FromResult<IReadOnlyList<StructuredLogRecord>>([]);

    public Task<IReadOnlyList<StructuredLogRecord>> GetItemTraceAsync(string itemId) =>
        Task.FromResult<IReadOnlyList<StructuredLogRecord>>([]);

    public Task<IReadOnlyList<NodeExecutionMetrics>> GetNodeExecutionMetricsAsync(string? executionId = null) =>
        Task.FromResult<IReadOnlyList<NodeExecutionMetrics>>([]);

    public Task ExportLogsAsync(TextWriter writer, LogFilterCriteria? filter = null) => Task.CompletedTask;

    public Task ClearAsync()
    {
        lock (_gate)
        {
            _records.Clear();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Servicio de diálogos que se acuerda de lo que se le mostró, en vez de abrir una ventana que ninguna
/// prueba puede cerrar. Los mensajes se separan por nivel a propósito: casi todo lo que hay que fijar en
/// estas pruebas es <b>que no se avisó</b> de algo, y un único saco de textos no distingue un error de una
/// información.
/// </summary>
public sealed class RecordingDialogService : IDialogService
{
    public List<string> ErrorMessages { get; } = [];

    public List<string> WarningMessages { get; } = [];

    public List<string> InformationMessages { get; } = [];

    public void ShowInformation(string message, string title = "FileFlow Studio") => InformationMessages.Add(message);

    public void ShowWarning(string message, string title = "FileFlow Studio") => WarningMessages.Add(message);

    public void ShowError(string message, string title = "Error") => ErrorMessages.Add(message);

    public bool ShowConfirmation(string message, string title = "FileFlow Studio") => true;

    public DialogResult ShowYesNoCancel(string message, string title = "FileFlow Studio") => DialogResult.Yes;
}

/// <summary>Diálogos de archivo que nunca se abren: devuelven «cancelado» y no bloquean la prueba.</summary>
public sealed class NullFileDialogService : IFileDialogService
{
    public string? ShowOpenFileDialog(string title, string filter, string defaultExt = "") => null;

    public string? ShowSaveFileDialog(string title, string filter, string defaultExt = "", string defaultFileName = "") => null;

    public string? ShowFolderBrowserDialog(string title) => null;
}

/// <summary>
/// Herramientas externas en memoria, con rutas fijas.
///
/// La pestaña «Herramientas Externas» de los ajustes y su captura muestran las rutas configuradas: leerlas del
/// servicio real haría que la línea base dependiera de la máquina que la generó (y que la prueba escribiera en
/// la configuración del usuario al guardar). Las rutas de este doble son deliberadamente ficticias.
/// </summary>
public sealed class InMemoryExternalToolsService : IExternalToolsService
{
    private ExternalToolsConfig _config = new()
    {
        FfmpegPath = "/workflow/tools/ffmpeg",
        FfprobePath = "/workflow/tools/ffprobe",
        SevenZipPath = "/workflow/tools/7z",
        PythonPath = "/workflow/tools/python3"
    };

    public ExternalToolsConfig Config => _config;

    public int SaveCount { get; private set; }

    public string ResolveToolPath(string toolName) => toolName switch
    {
        "ffmpeg" => _config.FfmpegPath,
        "ffprobe" => _config.FfprobePath,
        "7z" => _config.SevenZipPath,
        "python" => _config.PythonPath,
        _ => string.Empty
    };

    public bool IsToolAvailable(string toolName) => !string.IsNullOrWhiteSpace(ResolveToolPath(toolName));

    public void SaveConfig(ExternalToolsConfig config)
    {
        SaveCount++;
        _config = config;
    }

    public Task<ExternalToolsConfig> AutoDetectToolsAsync() => Task.FromResult(_config);
}

/// <summary>
/// Almacenamiento de flujos en memoria. La barra de control expone comandos de guardar y cargar, así que
/// pasarle el servicio real haría que un despiste de un test escribiera en la carpeta de flujos del usuario.
/// </summary>
public sealed class InMemoryWorkflowStorageService : IWorkflowStorageService
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask SaveWorkflowAsync(string filePath, WorkflowGraph graph, CancellationToken ct = default)
    {
        _files[filePath] = SerializeGraph(graph);
        return ValueTask.CompletedTask;
    }

    public ValueTask<WorkflowGraph> LoadWorkflowAsync(string filePath, CancellationToken ct = default) =>
        ValueTask.FromResult(
            _files.TryGetValue(filePath, out string? json) ? DeserializeGraph(json) : new WorkflowGraph());

    public string SerializeGraph(WorkflowGraph graph) => graph.ToJson();

    public WorkflowGraph DeserializeGraph(string json) => WorkflowGraph.FromJson(json);
}
