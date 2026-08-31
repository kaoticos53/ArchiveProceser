namespace FileFlow.Plugin.FileSystem.Reporting;

public sealed record ReportItemData
{
    public required string Id { get; init; }
    public required string FileName { get; init; }
    public required string OriginalPath { get; init; }
    public required string FinalPath { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string FormattedSize { get; init; }
    public required IReadOnlyList<string> Steps { get; init; }
    public required IReadOnlyDictionary<string, object?> Metadata { get; init; }
    public required IReadOnlySet<string> Tags { get; init; }
    public required bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}

public sealed record ReportDirectoryGroupData
{
    public required string GroupKey { get; init; }
    public required string DisplayName { get; init; }
    public required int FileCount { get; init; }
    public required int SuccessCount { get; init; }
    public required int ErrorCount { get; init; }
    public required long TotalBytes { get; init; }
    public required string FormattedTotalBytes { get; init; }
    public required IReadOnlyList<ReportItemData> Items { get; init; }
}

public sealed record ReportSummaryData
{
    public required string Title { get; init; }
    public required DateTime GeneratedAt { get; init; }
    public required int TotalFiles { get; init; }
    public required int SuccessCount { get; init; }
    public required int ErrorCount { get; init; }
    public required long TotalBytes { get; init; }
    public required string FormattedTotalBytes { get; init; }
    public required IReadOnlyList<ReportItemData> Items { get; init; }
    public string GroupBy { get; init; } = "Directory";
    public IReadOnlyList<ReportDirectoryGroupData> Groups { get; init; } = [];

    public static IReadOnlyList<ReportDirectoryGroupData> CreateGroups(
        IReadOnlyList<ReportItemData> items,
        string groupBy,
        Func<long, string> formatBytesFunc)
    {
        if (items.Count == 0) return [];

        if (groupBy.Equals("Flat", StringComparison.OrdinalIgnoreCase))
        {
            long bytes = items.Sum(i => i.FileSizeBytes);
            int succ = items.Count(i => i.IsSuccess);
            return
            [
                new ReportDirectoryGroupData
                {
                    GroupKey = "All",
                    DisplayName = "Todos los Archivos",
                    FileCount = items.Count,
                    SuccessCount = succ,
                    ErrorCount = items.Count - succ,
                    TotalBytes = bytes,
                    FormattedTotalBytes = formatBytesFunc(bytes),
                    Items = items
                }
            ];
        }

        IEnumerable<IGrouping<string, ReportItemData>> grouped = groupBy.ToUpperInvariant() switch
        {
            "EXTENSION" or "EXT" => items.GroupBy(i =>
            {
                string ext = Path.GetExtension(i.FileName);
                return string.IsNullOrWhiteSpace(ext) ? "Sin Extensión" : ext.ToLowerInvariant();
            }),
            "STATUS" => items.GroupBy(i => i.IsSuccess ? "✅ Completados con Éxito" : "⚠️ Con Errores / Alertas"),
            "DIRECTORY" or "DIR" or _ => items.GroupBy(i =>
            {
                string path = !string.IsNullOrWhiteSpace(i.OriginalPath) ? i.OriginalPath : i.FinalPath;
                string? dir = Path.GetDirectoryName(path);
                return string.IsNullOrWhiteSpace(dir) ? "Directorio Raíz" : dir;
            })
        };

        var result = new List<ReportDirectoryGroupData>();
        foreach (var grp in grouped.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var grpItems = grp.ToList();
            long grpBytes = grpItems.Sum(i => i.FileSizeBytes);
            int grpSuccess = grpItems.Count(i => i.IsSuccess);
            int grpErrors = grpItems.Count - grpSuccess;

            result.Add(new ReportDirectoryGroupData
            {
                GroupKey = grp.Key,
                DisplayName = grp.Key,
                FileCount = grpItems.Count,
                SuccessCount = grpSuccess,
                ErrorCount = grpErrors,
                TotalBytes = grpBytes,
                FormattedTotalBytes = formatBytesFunc(grpBytes),
                Items = grpItems
            });
        }

        return result;
    }
}
