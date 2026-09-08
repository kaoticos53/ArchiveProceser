using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using FileFlow.Sdk.Storage;
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

    public static async Task<Dictionary<string, Dictionary<string, string>>> LoadLookupTableAsync(
        string filePath,
        string keyColumn,
        CancellationToken cancellationToken,
        IStorageService? storage = null)
    {
        storage ??= NullStorageService.Instance;

        if (string.IsNullOrWhiteSpace(filePath) || !await storage.FileExistsAsync(filePath, cancellationToken).ConfigureAwait(false))
        {
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        var lastModified = await storage.GetLastWriteTimeAsync(filePath, cancellationToken).ConfigureAwait(false);
        string cacheKey = $"{filePath}::{keyColumn}";

        if (_cache.TryGetValue(cacheKey, out var entry) && entry.LastModifiedUtc == lastModified.UtcDateTime)
        {
            return entry.LookupIndex;
        }

        var index = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext is ".xlsx" or ".xls")
        {
            await using var stream = await storage.OpenReadAsync(filePath, cancellationToken).ConfigureAwait(false);
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
            await using var stream = await storage.OpenReadAsync(filePath, cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
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
            string json = await storage.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
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

        _cache[cacheKey] = new CacheEntry(lastModified.UtcDateTime, index);
        return index;
    }

    public static void ClearCache()
    {
        _cache.Clear();
    }
}
