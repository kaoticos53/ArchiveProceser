using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using MiniExcelLibs;

namespace FileFlow.Plugin.Data;

/// <summary>
/// Gestor de caché y carga en memoria de tablas de datos tabulares (Excel, CSV, JSON)
/// para operaciones ultrarrápidas de búsqueda y cruce O(1).
/// </summary>
public static class DataLookupTableLoader
{
    private record CacheEntry(DateTime LastModifiedUtc, Dictionary<string, Dictionary<string, string>> LookupIndex);
    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<Dictionary<string, Dictionary<string, string>>> LoadLookupTableAsync(string filePath, string keyColumn, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        var fileInfo = new FileInfo(filePath);
        string cacheKey = $"{filePath}::{keyColumn}";

        if (_cache.TryGetValue(cacheKey, out var entry) && entry.LastModifiedUtc == fileInfo.LastWriteTimeUtc)
        {
            return entry.LookupIndex;
        }

        var index = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext is ".xlsx" or ".xls")
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var rows = await stream.QueryAsync(useHeaderRow: true).ConfigureAwait(false);

            foreach (IDictionary<string, object> row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? rawKeyVal = null;

                foreach (var (k, v) in row)
                {
                    if (string.IsNullOrWhiteSpace(k)) continue;
                    string valStr = v?.ToString() ?? string.Empty;
                    rowDict[k.Trim()] = valStr;

                    if (k.Trim().Equals(keyColumn, StringComparison.OrdinalIgnoreCase))
                    {
                        rawKeyVal = valStr.Trim();
                    }
                }

                if (!string.IsNullOrWhiteSpace(rawKeyVal) && !index.ContainsKey(rawKeyVal))
                {
                    index[rawKeyVal] = rowDict;
                }
            }
        }
        else if (ext is ".csv" or ".tsv" or ".txt")
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8);
            string? headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(headerLine))
            {
                char delimiter = headerLine.Contains(';') ? ';' : (headerLine.Contains('\t') ? '\t' : ',');
                var headers = headerLine.Split(delimiter).Select(h => h.Trim(' ', '"')).ToList();
                int keyColIdx = headers.FindIndex(h => h.Equals(keyColumn, StringComparison.OrdinalIgnoreCase));

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = line.Split(delimiter).Select(c => c.Trim(' ', '"')).ToList();
                    var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < headers.Count; i++)
                    {
                        rowDict[headers[i]] = i < cols.Count ? cols[i] : string.Empty;
                    }

                    if (keyColIdx >= 0 && keyColIdx < cols.Count)
                    {
                        string keyVal = cols[keyColIdx].Trim();
                        if (!string.IsNullOrWhiteSpace(keyVal) && !index.ContainsKey(keyVal))
                        {
                            index[keyVal] = rowDict;
                        }
                    }
                }
            }
        }
        else if (ext is ".json")
        {
            string json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;
                    var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    string? rawKeyVal = null;

                    foreach (var prop in el.EnumerateObject())
                    {
                        string valStr = prop.Value.ToString();
                        rowDict[prop.Name] = valStr;
                        if (prop.Name.Equals(keyColumn, StringComparison.OrdinalIgnoreCase))
                        {
                            rawKeyVal = valStr.Trim();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(rawKeyVal) && !index.ContainsKey(rawKeyVal))
                    {
                        index[rawKeyVal] = rowDict;
                    }
                }
            }
        }

        _cache[cacheKey] = new CacheEntry(fileInfo.LastWriteTimeUtc, index);
        return index;
    }

    public static void ClearCache()
    {
        _cache.Clear();
    }
}
