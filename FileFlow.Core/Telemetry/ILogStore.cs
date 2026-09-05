using System.IO;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.Core.Telemetry;

/// <summary>
/// Contrato de puerto para persistencia, ingesta y consulta analítica de telemetría y logs estructurados.
/// </summary>
public interface ILogStore : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Encola un registro estructurado para inserción asíncrona en bloque en el almacén de logs.
    /// </summary>
    void EnqueueLog(StructuredLogRecord record);

    /// <summary>
    /// Encola múltiples registros estructurados para inserción asíncrona en bloque.
    /// </summary>
    void EnqueueLogs(IEnumerable<StructuredLogRecord> records);

    /// <summary>
    /// Fuerza el vaciado de todos los registros encolados pendientes hacia el almacenamiento subyacente.
    /// </summary>
    Task FlushPendingLogsAsync();

    /// <summary>
    /// Recupera una ventana paginada y filtrada de registros de log.
    /// </summary>
    Task<IReadOnlyList<StructuredLogRecord>> GetLogsWindowAsync(
        int offset,
        int limit,
        LogFilterCriteria? filter = null,
        bool newestFirst = false);

    /// <summary>
    /// Obtiene el total de registros de log que cumplen el criterio de filtrado.
    /// </summary>
    Task<int> GetTotalCountAsync(LogFilterCriteria? filter = null);

    /// <summary>
    /// Recupera la traza histórica de registros vinculados a un archivo.
    /// </summary>
    Task<IReadOnlyList<StructuredLogRecord>> GetFileTraceAsync(string fileNameOrPath);

    /// <summary>
    /// Recupera la traza histórica de registros vinculados a un elemento de flujo específico.
    /// </summary>
    Task<IReadOnlyList<StructuredLogRecord>> GetItemTraceAsync(string itemId);

    /// <summary>
    /// Obtiene métricas agregadas de rendimiento de ejecución por nodo.
    /// </summary>
    Task<IReadOnlyList<NodeExecutionMetrics>> GetNodeExecutionMetricsAsync(string? executionId = null);

    /// <summary>
    /// Exporta los registros de log a un flujo de texto.
    /// </summary>
    Task ExportLogsAsync(TextWriter writer, LogFilterCriteria? filter = null);

    /// <summary>
    /// Limpia todos los registros almacenados en el log.
    /// </summary>
    Task ClearAsync();
}
