using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Core.Telemetry;
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

/// <summary>Diálogos de archivo que nunca se abren: devuelven «cancelado» y no bloquean la prueba.</summary>
public sealed class NullFileDialogService : IFileDialogService
{
    public string? ShowOpenFileDialog(string title, string filter, string defaultExt = "") => null;

    public string? ShowSaveFileDialog(string title, string filter, string defaultExt = "", string defaultFileName = "") => null;

    public string? ShowFolderBrowserDialog(string title) => null;
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
