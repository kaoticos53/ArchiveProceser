using FileFlow.Sdk;

namespace FileFlow.Core.Telemetry;

/// <summary>
/// Constructor de consultas parametrizadas SQL para el almacén de telemetría de SQLite.
/// </summary>
public static class SqliteLogQueryBuilder
{
    public static (string WhereClause, Dictionary<string, object> Parameters) BuildFilterClause(LogFilterCriteria? filter)
    {
        if (filter == null) return (string.Empty, []);

        var clauses = new List<string>();
        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (filter.ExactLevel.HasValue)
        {
            clauses.Add("Level = @exactLevel");
            parameters["@exactLevel"] = (int)filter.ExactLevel.Value;
        }
        else if (filter.MinLevel.HasValue)
        {
            clauses.Add("Level >= @minLevel");
            parameters["@minLevel"] = (int)filter.MinLevel.Value;
        }

        if (!string.IsNullOrWhiteSpace(filter.NodeId))
        {
            clauses.Add("(NodeId = @nodeId OR NodeName = @nodeId)");
            parameters["@nodeId"] = filter.NodeId;
        }

        if (!string.IsNullOrWhiteSpace(filter.ItemId))
        {
            clauses.Add("(ItemId = @itemId OR ItemId LIKE @itemIdLike)");
            parameters["@itemId"] = filter.ItemId;
            parameters["@itemIdLike"] = $"%{filter.ItemId}%";
        }

        if (!string.IsNullOrWhiteSpace(filter.FilePattern))
        {
            clauses.Add("(FileName LIKE @filePattern OR FilePath LIKE @filePattern)");
            parameters["@filePattern"] = $"%{filter.FilePattern}%";
        }

        if (filter.HasDetailsOnly == true)
        {
            clauses.Add("(DetailsJson IS NOT NULL AND Length(DetailsJson) > 0)");
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            clauses.Add("(Message LIKE @searchText OR FileName LIKE @searchText OR NodeName LIKE @searchText OR ItemId LIKE @searchText OR DetailsJson LIKE @searchText)");
            parameters["@searchText"] = $"%{filter.SearchText}%";
        }

        if (filter.FromTimestamp.HasValue)
        {
            clauses.Add("Timestamp >= @fromTs");
            parameters["@fromTs"] = filter.FromTimestamp.Value;
        }

        if (filter.ToTimestamp.HasValue)
        {
            clauses.Add("Timestamp <= @toTs");
            parameters["@toTs"] = filter.ToTimestamp.Value;
        }

        string where = clauses.Count > 0 ? "WHERE " + string.Join(" AND ", clauses) : string.Empty;
        return (where, parameters);
    }
}
