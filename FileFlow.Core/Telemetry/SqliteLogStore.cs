using System.Data;
using System.Threading.Channels;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;
using Microsoft.Data.Sqlite;

namespace FileFlow.Core.Telemetry;

public record LogFilterCriteria(
    LogLevel? MinLevel = null,
    LogLevel? ExactLevel = null,
    string? SearchText = null,
    string? NodeId = null,
    string? ItemId = null,
    string? FilePattern = null,
    bool? HasDetailsOnly = null,
    long? FromTimestamp = null,
    long? ToTimestamp = null,
    string? SortColumn = "Id",
    bool IsAscending = true
);

public record NodeExecutionMetrics(
    string NodeId,
    string NodeName,
    int TotalExecutions,
    double AvgDurationMs,
    double MaxDurationMs,
    double MinDurationMs,
    int ErrorCount
);

/// <summary>
/// Motor analítico y almacén de logs estructurados en memoria de ultra-alto rendimiento basado en SQLite.
/// </summary>
public sealed class SqliteLogStore : ILogStore, IAsyncDisposable, IDisposable
{
    private static readonly Lazy<SqliteLogStore> _instance = new(() => new SqliteLogStore());
    public static SqliteLogStore Instance => _instance.Value;

    private readonly string _connectionString;
    private readonly SqliteConnection _keepAliveConnection;
    private readonly Channel<StructuredLogRecord> _ingestionChannel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _workerTask;
    private readonly Lock _dbLock = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    public SqliteLogStore(string dbName = "FileFlowLogs")
    {
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();

        SqliteLogSchema.Initialize(_keepAliveConnection, _dbLock);

        _ingestionChannel = Channel.CreateBounded<StructuredLogRecord>(new BoundedChannelOptions(100_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = true
        });

        _workerTask = Task.Run(ProcessIngestionQueueAsync);
    }

    public void EnqueueLog(StructuredLogRecord record)
    {
        _ingestionChannel.Writer.TryWrite(record);
    }

    public void EnqueueLogs(IEnumerable<StructuredLogRecord> records)
    {
        foreach (var record in records)
        {
            _ingestionChannel.Writer.TryWrite(record);
        }
    }

    private async Task ProcessIngestionQueueAsync()
    {
        var batch = new List<StructuredLogRecord>(1000);

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                if (await _ingestionChannel.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
                {
                    await Task.Yield();

                    while (batch.Count < 2000 && _ingestionChannel.Reader.TryRead(out var item))
                    {
                        batch.Add(item);
                    }

                    if (batch.Count > 0)
                    {
                        await InsertBatchAsync(batch).ConfigureAwait(false);
                        batch.Clear();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(50, _cts.Token).ConfigureAwait(false);
            }
        }

        while (_ingestionChannel.Reader.TryRead(out var remaining))
        {
            batch.Add(remaining);
        }
        if (batch.Count > 0)
        {
            await InsertBatchAsync(batch).ConfigureAwait(false);
        }
    }

    private async Task InsertBatchAsync(List<StructuredLogRecord> records)
    {
        if (records.Count == 0) return;

        await _flushLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var transaction = await _keepAliveConnection.BeginTransactionAsync().ConfigureAwait(false);

            await using var cmd = _keepAliveConnection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = """
                INSERT INTO ExecutionLogs (ExecutionId, Timestamp, Level, NodeId, NodeName, ItemId, FilePath, FileName, FileSizeBytes, DurationMs, Message, DetailsJson)
                VALUES (@ExecutionId, @Timestamp, @Level, @NodeId, @NodeName, @ItemId, @FilePath, @FileName, @FileSizeBytes, @DurationMs, @Message, @DetailsJson);
            """;

            var pExec = cmd.Parameters.Add("@ExecutionId", SqliteType.Text);
            var pTime = cmd.Parameters.Add("@Timestamp", SqliteType.Integer);
            var pLevel = cmd.Parameters.Add("@Level", SqliteType.Integer);
            var pNodeId = cmd.Parameters.Add("@NodeId", SqliteType.Text);
            var pNodeName = cmd.Parameters.Add("@NodeName", SqliteType.Text);
            var pItemId = cmd.Parameters.Add("@ItemId", SqliteType.Text);
            var pFilePath = cmd.Parameters.Add("@FilePath", SqliteType.Text);
            var pFileName = cmd.Parameters.Add("@FileName", SqliteType.Text);
            var pFileSize = cmd.Parameters.Add("@FileSizeBytes", SqliteType.Integer);
            var pDuration = cmd.Parameters.Add("@DurationMs", SqliteType.Real);
            var pMessage = cmd.Parameters.Add("@Message", SqliteType.Text);
            var pDetails = cmd.Parameters.Add("@DetailsJson", SqliteType.Text);

            foreach (var r in records)
            {
                pExec.Value = r.ExecutionId ?? string.Empty;
                pTime.Value = new DateTimeOffset(r.Timestamp).ToUnixTimeMilliseconds();
                pLevel.Value = (int)r.Level;
                pNodeId.Value = (object?)r.NodeId ?? DBNull.Value;
                pNodeName.Value = (object?)r.NodeName ?? DBNull.Value;
                pItemId.Value = (object?)r.ItemId ?? DBNull.Value;
                pFilePath.Value = (object?)r.FilePath ?? DBNull.Value;
                pFileName.Value = (object?)r.FileName ?? DBNull.Value;
                pFileSize.Value = r.FileSizeBytes;
                pDuration.Value = r.DurationMs;
                pMessage.Value = r.Message ?? string.Empty;
                pDetails.Value = (object?)r.DetailsJson ?? DBNull.Value;

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await transaction.CommitAsync().ConfigureAwait(false);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async Task FlushPendingLogsAsync()
    {
        var batch = new List<StructuredLogRecord>();
        while (_ingestionChannel.Reader.TryRead(out var item))
        {
            batch.Add(item);
        }

        if (batch.Count > 0)
        {
            await InsertBatchAsync(batch).ConfigureAwait(false);
        }

        // Asegurar que cualquier escritura del worker concurrente haya terminado
        await _flushLock.WaitAsync().ConfigureAwait(false);
        _flushLock.Release();
    }

    public async Task<IReadOnlyList<StructuredLogRecord>> GetLogsWindowAsync(
        int offset,
        int limit,
        LogFilterCriteria? filter = null,
        bool newestFirst = false)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        var (whereSql, parameters) = BuildFilterClause(filter);

        string sortCol = filter?.SortColumn?.Trim().ToLowerInvariant() switch
        {
            "timestamp" => "Timestamp",
            "level" => "Level",
            "nodename" or "node" => "NodeName",
            "itemid" or "item" => "ItemId",
            "filename" or "file" => "FileName",
            "filesizebytes" or "filesize" or "size" => "FileSizeBytes",
            "durationms" or "duration" => "DurationMs",
            "message" => "Message",
            _ => "Id"
        };

        string dir = filter != null && !string.IsNullOrWhiteSpace(filter.SortColumn)
            ? (filter.IsAscending ? "ASC" : "DESC")
            : (newestFirst ? "DESC" : "ASC");

        string sql = $"""
            SELECT Id, ExecutionId, Timestamp, Level, NodeId, NodeName, ItemId, FilePath, FileName, FileSizeBytes, DurationMs, Message, DetailsJson
            FROM ExecutionLogs
            {whereSql}
            ORDER BY {sortCol} {dir}
            LIMIT @limit OFFSET @offset;
        """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        foreach (var (k, v) in parameters)
        {
            cmd.Parameters.AddWithValue(k, v);
        }

        var results = new List<StructuredLogRecord>(limit);
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            long id = reader.GetInt64(0);
            string execId = reader.GetString(1);
            long unixMs = reader.GetInt64(2);
            DateTime ts = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;
            LogLevel level = (LogLevel)reader.GetInt32(3);
            string? nodeId = reader.IsDBNull(4) ? null : reader.GetString(4);
            string? nodeName = reader.IsDBNull(5) ? null : reader.GetString(5);
            string? itemId = reader.IsDBNull(6) ? null : reader.GetString(6);
            string? filePath = reader.IsDBNull(7) ? null : reader.GetString(7);
            string? fileName = reader.IsDBNull(8) ? null : reader.GetString(8);
            long fileSizeBytes = reader.GetInt64(9);
            double duration = reader.GetDouble(10);
            string message = reader.GetString(11);
            string? detailsJson = reader.IsDBNull(12) ? null : reader.GetString(12);

            results.Add(new StructuredLogRecord(id, execId, ts, level, nodeId, nodeName, itemId, filePath, fileName, fileSizeBytes, duration, message, detailsJson));
        }

        return results;
    }

    public async Task<int> GetTotalCountAsync(LogFilterCriteria? filter = null)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        var (whereSql, parameters) = BuildFilterClause(filter);
        string sql = $"SELECT COUNT(*) FROM ExecutionLogs {whereSql};";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in parameters)
        {
            cmd.Parameters.AddWithValue(k, v);
        }

        object? countObj = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToInt32(countObj);
    }

    public async Task<IReadOnlyList<StructuredLogRecord>> GetFileTraceAsync(string fileNameOrPath)
    {
        var filter = new LogFilterCriteria(FilePattern: fileNameOrPath);
        return await GetLogsWindowAsync(0, 1000, filter, newestFirst: false).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StructuredLogRecord>> GetItemTraceAsync(string itemId)
    {
        var filter = new LogFilterCriteria(ItemId: itemId);
        return await GetLogsWindowAsync(0, 1000, filter, newestFirst: false).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<NodeExecutionMetrics>> GetNodeExecutionMetricsAsync(string? executionId = null)
    {
        return SqliteLogMetricsReader.GetNodeExecutionMetricsAsync(_connectionString, executionId);
    }

    public async Task ExportLogsAsync(TextWriter writer, LogFilterCriteria? filter = null)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        var (whereSql, parameters) = BuildFilterClause(filter);
        string sql = $"SELECT Id, ExecutionId, Timestamp, Level, NodeName, ItemId, FileName, Message, DetailsJson FROM ExecutionLogs {whereSql} ORDER BY Id ASC;";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in parameters)
        {
            cmd.Parameters.AddWithValue(k, v);
        }

        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            long unixMs = reader.GetInt64(2);
            DateTime ts = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;
            LogLevel level = (LogLevel)reader.GetInt32(3);
            string node = reader.IsDBNull(4) ? "Core" : reader.GetString(4);
            string? item = reader.IsDBNull(5) ? null : reader.GetString(5);
            string msg = reader.GetString(7);
            string itemPrefix = !string.IsNullOrWhiteSpace(item) ? $" [#{item[..Math.Min(8, item.Length)]}]" : "";

            await writer.WriteLineAsync($"[{ts:yyyy-MM-dd HH:mm:ss}] [{level}]{itemPrefix} [{node}] {msg}").ConfigureAwait(false);
        }
    }

    public async Task ClearAsync()
    {
        while (_ingestionChannel.Reader.TryRead(out _)) { }

        await _flushLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ExecutionLogs;";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private static (string WhereClause, Dictionary<string, object> Parameters) BuildFilterClause(LogFilterCriteria? filter)
        => SqliteLogQueryBuilder.BuildFilterClause(filter);

    public async ValueTask DisposeAsync()
    {
        _ingestionChannel.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            await _workerTask.ConfigureAwait(false);
        }
        catch { }

        _keepAliveConnection.Dispose();
        _cts.Dispose();
        _flushLock.Dispose();
    }

    public void Dispose()
    {
        _ingestionChannel.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            if (!_workerTask.IsCompleted)
            {
                _workerTask.Wait(TimeSpan.FromSeconds(3));
            }
        }
        catch { }

        _keepAliveConnection.Dispose();
        _cts.Dispose();
        _flushLock.Dispose();
    }
}
