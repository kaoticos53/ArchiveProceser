using System.IO;
using System.Text.Json;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Microsoft.Data.Sqlite;

namespace FileFlow.Plugin.Data;

[NodeDefinition("SqliteDatabaseSinkNode_Name", "Data", "SqliteDatabaseSinkNode_Desc", PipelineRole.Sink,
    "sqlite", "sql", "base de datos", "db", "guardar", "insertar", "auditoria")]
public class SqliteDatabaseSinkNode : IFlowNode
{
    private static readonly Lock _initLock = new();
    private static readonly HashSet<string> _initializedDbs = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SqliteDatabaseSinkNode_Name", "Registro de Auditoría SQLite");
    public string Category => "Data";
    public string Description => LocalizationManager.Instance.GetString("SqliteDatabaseSinkNode_Desc", "Inserta un registro histórico y de auditoría en una base de datos SQLite con los metadatos y trazabilidad de cada archivo procesado.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DatabasePath"] = @"{GlobalOutputDir}\fileflow_audit.db",
        ["TableName"] = "FileProcessingLog",
        ["AutoCreateTable"] = true,
        ["StoreMetadataAsJson"] = true
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("DatabasePath", ParameterEditorType.FilePath, DefaultValue: @"{GlobalOutputDir}\fileflow_audit.db", DisplayOrder: 1),
        new("TableName", ParameterEditorType.Text, DefaultValue: "FileProcessingLog", DisplayOrder: 2),
        new("AutoCreateTable", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 3),
        new("StoreMetadataAsJson", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        string dbPath = Parameters.TryGetValue("DatabasePath", out var dp) ? dp?.ToString() ?? string.Empty : string.Empty;
        dbPath = Environment.ExpandEnvironmentVariables(dbPath);

        if (item.Metadata.TryGetValue("GlobalOutputDir", out var gOutObj) && gOutObj is string gOut)
        {
            dbPath = dbPath.Replace("{GlobalOutputDir}", gOut, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(dbPath))
        {
            dbPath = Path.Combine(Path.GetTempPath(), "fileflow_audit.db");
        }

        string? dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        string tableName = Parameters.TryGetValue("TableName", out var tn) ? tn?.ToString() ?? "FileProcessingLog" : "FileProcessingLog";
        if (string.IsNullOrWhiteSpace(tableName)) tableName = "FileProcessingLog";

        bool autoCreate = Parameters.TryGetValue("AutoCreateTable", out var ac) && ParameterHelper.GetBoolean(ac, true);
        bool storeMetadata = Parameters.TryGetValue("StoreMetadataAsJson", out var sm) && ParameterHelper.GetBoolean(sm, true);

        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        if (autoCreate)
        {
            EnsureTableCreated(connectionString, tableName);
        }

        string metadataJson = storeMetadata ? JsonSerializer.Serialize(item.Metadata) : "{}";
        string sha256 = item.Metadata.TryGetValue("HashSHA256", out var hObj) ? hObj?.ToString() ?? string.Empty : string.Empty;
        string status = item.Metadata.TryGetValue("Status", out var sObj) ? sObj?.ToString() ?? "Processed" : "Processed";

        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        string insertSql = $"""
            INSERT INTO [{tableName}] 
            ([FileName], [CurrentPath], [OriginalPath], [FileSizeBytes], [HashSHA256], [ProcessedAtUtc], [Status], [MetadataJson])
            VALUES (@FileName, @CurrentPath, @OriginalPath, @FileSizeBytes, @HashSHA256, @ProcessedAtUtc, @Status, @MetadataJson);
            """;

        await using var cmd = new SqliteCommand(insertSql, conn);
        cmd.Parameters.AddWithValue("@FileName", item.FileName);
        cmd.Parameters.AddWithValue("@CurrentPath", item.CurrentPath);
        cmd.Parameters.AddWithValue("@OriginalPath", item.OriginalPath);
        cmd.Parameters.AddWithValue("@FileSizeBytes", item.FileSizeBytes);
        cmd.Parameters.AddWithValue("@HashSHA256", sha256);
        cmd.Parameters.AddWithValue("@ProcessedAtUtc", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue("@MetadataJson", metadataJson);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        context.Log($"[SqliteSink] Registro de auditoría guardado en '{Path.GetFileName(dbPath)}' para '{item.FileName}'", LogLevel.Debug, item);

        await context.EmitAsync("Out", item).ConfigureAwait(false);
    }

    private static void EnsureTableCreated(string connectionString, string tableName)
    {
        string initKey = $"{connectionString}::{tableName}";
        if (_initializedDbs.Contains(initKey)) return;

        lock (_initLock)
        {
            if (_initializedDbs.Contains(initKey)) return;

            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            string createSql = $"""
                CREATE TABLE IF NOT EXISTS [{tableName}] (
                    [Id] INTEGER PRIMARY KEY AUTOINCREMENT,
                    [FileName] TEXT NOT NULL,
                    [CurrentPath] TEXT,
                    [OriginalPath] TEXT,
                    [FileSizeBytes] INTEGER,
                    [HashSHA256] TEXT,
                    [ProcessedAtUtc] TEXT NOT NULL,
                    [Status] TEXT,
                    [MetadataJson] TEXT
                );
                CREATE INDEX IF NOT EXISTS [IX_{tableName}_FileName] ON [{tableName}]([FileName]);
                CREATE INDEX IF NOT EXISTS [IX_{tableName}_ProcessedAtUtc] ON [{tableName}]([ProcessedAtUtc]);
                """;

            using var cmd = new SqliteCommand(createSql, conn);
            cmd.ExecuteNonQuery();

            _initializedDbs.Add(initKey);
        }
    }
}
