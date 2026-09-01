using Microsoft.Data.Sqlite;

namespace FileFlow.Core.Telemetry;

/// <summary>
/// Encapsula la definición del esquema DDL e índices para el almacén de logs SQLite.
/// </summary>
public static class SqliteLogSchema
{
    public const string SchemaSql = """
        PRAGMA journal_mode = WAL;
        PRAGMA synchronous = NORMAL;
        PRAGMA temp_store = MEMORY;

        CREATE TABLE IF NOT EXISTS ExecutionLogs (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ExecutionId TEXT NOT NULL,
            Timestamp INTEGER NOT NULL,
            Level INTEGER NOT NULL,
            NodeId TEXT,
            NodeName TEXT,
            ItemId TEXT,
            FilePath TEXT,
            FileName TEXT,
            FileSizeBytes INTEGER DEFAULT 0,
            DurationMs REAL DEFAULT 0.0,
            Message TEXT NOT NULL,
            DetailsJson TEXT
        );

        CREATE INDEX IF NOT EXISTS IX_Logs_Timestamp ON ExecutionLogs (Timestamp);
        CREATE INDEX IF NOT EXISTS IX_Logs_ItemId ON ExecutionLogs (ItemId);
        CREATE INDEX IF NOT EXISTS IX_Logs_FileName ON ExecutionLogs (FileName);
        CREATE INDEX IF NOT EXISTS IX_Logs_FilePath ON ExecutionLogs (FilePath);
        CREATE INDEX IF NOT EXISTS IX_Logs_NodeId ON ExecutionLogs (NodeId);
        CREATE INDEX IF NOT EXISTS IX_Logs_Level ON ExecutionLogs (Level);
    """;

    public static void Initialize(SqliteConnection connection, System.Threading.Lock dbLock)
    {
        lock (dbLock)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = SchemaSql;
            cmd.ExecuteNonQuery();
        }
    }
}
