using Microsoft.Data.Sqlite;

namespace FileFlow.Core.Telemetry;

/// <summary>
/// Lector y agregador analítico de métricas de ejecución por nodo sobre SQLite.
/// </summary>
public static class SqliteLogMetricsReader
{
    public static async Task<IReadOnlyList<NodeExecutionMetrics>> GetNodeExecutionMetricsAsync(
        string connectionString, 
        string? executionId = null)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        string sql = """
            SELECT 
                NodeId,
                COALESCE(NodeName, NodeId, 'General') AS NodeName,
                COUNT(*) AS TotalCount,
                AVG(DurationMs) AS AvgDuration,
                MAX(DurationMs) AS MaxDuration,
                MIN(DurationMs) AS MinDuration,
                SUM(CASE WHEN Level >= 3 THEN 1 ELSE 0 END) AS ErrorCount
            FROM ExecutionLogs
            WHERE NodeId IS NOT NULL
        """;

        if (!string.IsNullOrWhiteSpace(executionId))
        {
            sql += " AND ExecutionId = @executionId";
        }

        sql += " GROUP BY NodeId ORDER BY AvgDuration DESC;";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (!string.IsNullOrWhiteSpace(executionId))
        {
            cmd.Parameters.AddWithValue("@executionId", executionId);
        }

        var results = new List<NodeExecutionMetrics>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            string nodeId = reader.GetString(0);
            string nodeName = reader.GetString(1);
            int total = reader.GetInt32(2);
            double avg = reader.GetDouble(3);
            double max = reader.GetDouble(4);
            double min = reader.GetDouble(5);
            int errCount = reader.GetInt32(6);

            results.Add(new NodeExecutionMetrics(nodeId, nodeName, total, avg, max, min, errCount));
        }

        return results;
    }
}
