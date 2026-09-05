using System.Collections.Concurrent;
using System.Security.Cryptography;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Hashing;

[NodeDefinition("DeduplicationFilterNode_Name", "Security", "DeduplicationFilterNode_Desc", PipelineRole.Filter,
    "duplicados", "deduplicar", "unicos", "repetidos", "checksum", "duplicate", "filter", "hash")]
public class DeduplicationFilterNode : IFlowNode
{
    private readonly ConcurrentDictionary<string, string> _seenHashes = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastExecutionId;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("DeduplicationFilterNode_Name", "Filtro de Deduplicación por Hash");
    public string Category => "Security";
    public string Description => LocalizationManager.Instance.GetString("DeduplicationFilterNode_Desc", "Compara el hash del contenido para detectar archivos repetidos en el lote actual, separando los archivos originales (Unique) de las copias duplicadas redundantes (Duplicate).");


    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Unique", typeof(FileItemContext), PortDirection.Output, "Unique"),
        new NodePort("Duplicate", typeof(FileItemContext), PortDirection.Output, "Duplicate"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HashMetadataKey"] = "Hash:SHA256"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (item.Metadata.TryGetValue("WorkflowExecutionId", out var execIdObj) && execIdObj?.ToString() is string execId && _lastExecutionId != execId)
        {
            _lastExecutionId = execId;
            _seenHashes.Clear();
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[Filtro Deduplicación] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            string key = Parameters.TryGetValue("HashMetadataKey", out var kVal) ? ParameterHelper.GetString(kVal, "Hash:SHA256") : "Hash:SHA256";
            string hashValue;

            if (item.Metadata.TryGetValue(key, out var hObj) && hObj != null && !string.IsNullOrWhiteSpace(hObj.ToString()))
            {
                hashValue = hObj.ToString()!;
            }
            else
            {
                // Calcular SHA-256 on-the-fly si no venía en los metadatos
                await using var stream = new FileStream(item.CurrentPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
                byte[] hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                hashValue = Convert.ToHexStringLower(hashBytes);
                item.Metadata["Hash:SHA256"] = hashValue;
            }

            sw.Stop();

            if (_seenHashes.TryAdd(hashValue, item.CurrentPath))
            {
                item.AddLog($"Deduplication: Unique file (Hash={hashValue})");
                context.Log($"[Filtro Deduplicación] Archivo único (Hash: {hashValue[..Math.Min(12, hashValue.Length)]}...) -> Rama 'Unique'", LogLevel.Debug, item, durationMs: sw.Elapsed.TotalMilliseconds);
                await context.EmitAsync("Unique", item);
            }
            else
            {
                string firstPath = _seenHashes[hashValue];
                item.Metadata["DuplicateOf"] = firstPath;
                item.AddLog($"Deduplication: DUPLICATE of '{firstPath}' (Hash={hashValue})");

                string detailsJson = $"{{\"hash\": \"{hashValue}\", \"duplicateOf\": \"{firstPath.Replace("\\", "\\\\")}\", \"currentPath\": \"{item.CurrentPath.Replace("\\", "\\\\")}\"}}";
                context.Log($"[Filtro Deduplicación] Duplicado detectado de '{Path.GetFileName(firstPath)}' -> Rama 'Duplicate'", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

                await context.EmitAsync("Duplicate", item);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"file\": \"{item.CurrentPath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Filtro Deduplicación] Error en deduplicación: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"Deduplication failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }
}
