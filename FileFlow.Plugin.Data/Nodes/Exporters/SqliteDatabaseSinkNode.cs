using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using Microsoft.Data.Sqlite;

namespace FileFlow.Plugin.Data;

[NodeDefinition("SqliteDatabaseSinkNode_Name", "Data", "SqliteDatabaseSinkNode_Desc", PipelineRole.Sink,
    "sqlite", "sql", "base de datos", "db", "guardar", "insertar", "auditoria")]
public sealed class SqliteDatabaseSinkNode : FlowNodeBase
{
    private static readonly Lock _initLock = new();
    private static readonly ConcurrentDictionary<string, bool> _initializedDbs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex _validTableNameRegex = new(@"^[a-zA-Z_]\w{0,127}$", RegexOptions.Compiled);

    public override string Name => LocalizationManager.Instance.GetString("SqliteDatabaseSinkNode_Name", "Registro de Auditoría SQLite");
    public override string Category => "Data";
    public override string Description => LocalizationManager.Instance.GetString("SqliteDatabaseSinkNode_Desc", "Inserta un registro histórico y de auditoría en una base de datos SQLite con los metadatos y trazabilidad de cada archivo procesado.");

    public SqliteDatabaseSinkNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
        ];

        Parameters["DatabasePath"] = @"{GlobalOutputDir}\fileflow_audit.db";
        Parameters["TableName"] = "FileProcessingLog";
        Parameters["AutoCreateTable"] = true;
        Parameters["StoreMetadataAsJson"] = true;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("DatabasePath", ParameterEditorType.FilePath, DefaultValue: @"{GlobalOutputDir}\fileflow_audit.db", DisplayOrder: 1),
        new("TableName", ParameterEditorType.Text, DefaultValue: "FileProcessingLog", DisplayOrder: 2),
        new("AutoCreateTable", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 3),
        new("StoreMetadataAsJson", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        string dbPath = GetParameter("DatabasePath", string.Empty);
        dbPath = Environment.ExpandEnvironmentVariables(dbPath);

        if (item.Metadata.TryGetValue("GlobalOutputDir", out var gOutObj) && gOutObj is string gOut)
        {
            dbPath = dbPath.Replace("{GlobalOutputDir}", gOut, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(dbPath))
        {
            dbPath = Path.Combine(Path.GetTempPath(), "fileflow_audit.db");
        }

        var storage = context.GetStorage();
        string? dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dir) && !await storage.DirectoryExistsAsync(dir, cancellationToken).ConfigureAwait(false))
        {
            await storage.CreateDirectoryAsync(dir, cancellationToken).ConfigureAwait(false);
        }

        string tableName = GetParameter("TableName", "FileProcessingLog");
        if (string.IsNullOrWhiteSpace(tableName)) tableName = "FileProcessingLog";

        // CRIT-01: Validación estricta del nombre de tabla para prevenir inyección SQL
        if (!_validTableNameRegex.IsMatch(tableName))
        {
            context.Log($"[SqliteSink] Nombre de tabla inválido o inseguro: '{tableName}'. Solo se permiten letras, dígitos y guión bajo.", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        bool autoCreate = GetParameter("AutoCreateTable", false);
        bool storeMetadata = GetParameter("StoreMetadataAsJson", false);

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
        if (_initializedDbs.ContainsKey(initKey)) return;

        lock (_initLock)
        {
            if (_initializedDbs.ContainsKey(initKey)) return;

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

            _initializedDbs.TryAdd(initKey, true);
        }
    }
}
