using System.Security.Cryptography;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Hashing;

[NodeDefinition("HashCalculatorNode_Name", "Metadata", "HashCalculatorNode_Desc")]
public class HashCalculatorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("HashCalculatorNode_Name", "Calculador de Hash Criptográfico");
    public string Category => "Metadata";
    public string Description => LocalizationManager.Instance.GetString("HashCalculatorNode_Desc", "Calcula el checksum o hash del contenido del archivo (SHA-256, MD5, SHA-512, SHA-1) y lo almacena en los metadatos para renombrado, verificación de integridad o deduplicación.");


    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Algorithm"] = "SHA256", // SHA256, MD5, SHA512, SHA1
        ["StoreInMetadataKey"] = "Hash:SHA256"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[Calculador Hash] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            string algo = Parameters.TryGetValue("Algorithm", out var aVal) ? ParameterHelper.GetString(aVal, "SHA256") : "SHA256";
            string metaKey = Parameters.TryGetValue("StoreInMetadataKey", out var kVal) ? ParameterHelper.GetString(kVal, $"Hash:{algo}") : $"Hash:{algo}";

            string hashResult = await ComputeHashAsync(item.CurrentPath, algo, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            item.Metadata[metaKey] = hashResult;
            item.Metadata["Hash"] = hashResult;
            item.AddLog($"Computed {algo} hash: {hashResult}");

            string detailsJson = $"{{\"algorithm\": \"{algo}\", \"hash\": \"{hashResult}\", \"metadataKey\": \"{metaKey}\", \"fileSizeBytes\": {item.FileSizeBytes}}}";
            context.Log($"[Calculador Hash] {algo}: {hashResult[..Math.Min(16, hashResult.Length)]}...", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"file\": \"{item.CurrentPath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Calculador Hash] Error al calcular hash: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"Hash computation failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static async Task<string> ComputeHashAsync(string filePath, string algorithm, CancellationToken ct)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, useAsync: true);

        byte[] hashBytes = algorithm.ToUpperInvariant() switch
        {
            "MD5" => await MD5.HashDataAsync(stream, ct).ConfigureAwait(false),
            "SHA1" => await SHA1.HashDataAsync(stream, ct).ConfigureAwait(false),
            "SHA512" => await SHA512.HashDataAsync(stream, ct).ConfigureAwait(false),
            "SHA256" or _ => await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false)
        };

        return Convert.ToHexStringLower(hashBytes);
    }
}
