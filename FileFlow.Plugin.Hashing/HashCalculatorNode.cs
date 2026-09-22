using System.IO;
using System.Security.Cryptography;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.Hashing;

[NodeDefinition("HashCalculatorNode_Name", "Security", "HashCalculatorNode_Desc", PipelineRole.Analyze,
    "hash", "sha256", "md5", "sha1", "sha512", "checksum", "integridad")]
public sealed class HashCalculatorNode : FlowNodeBase
{
    public override string Name => "Calculador Hash";
    public override string Category => "Security";
    public override string Description => "Calcula la firma criptográfica (SHA-256, MD5, SHA-1, SHA-512) del archivo y la inyecta en los metadatos.";

    public HashCalculatorNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Algorithm"] = "SHA256";
        Parameters["StoreInMetadataKey"] = "Hash:SHA256";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("Algorithm", ParameterEditorType.Dropdown, DefaultValue: "SHA256", DisplayOrder: 1, Options: ["SHA256", "MD5", "SHA1", "SHA512"]),
        new("StoreInMetadataKey", ParameterEditorType.Text, DefaultValue: "Hash:SHA256", DisplayOrder: 2)
    ];

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var storage = context.GetStorage();

        bool exists = await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);

        if (!exists && !item.IsVirtual && !context.IsVirtualFileSystemEnabled)
        {
            context.Log($"[Calculador Hash] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            string algo = GetParameter("Algorithm", "SHA256");
            string metaKey = GetParameter("StoreInMetadataKey", $"Hash:{algo}");

            string hashResult = await ComputeHashAsync(storage, item.CurrentPath, algo, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            item.Metadata[metaKey] = hashResult;
            item.Metadata["Hash"] = hashResult;
            item.AddLog($"Computed {algo} hash: {hashResult}");

            string detailsJson = $"{{\"algorithm\": \"{algo}\", \"hash\": \"{hashResult}\", \"metadataKey\": \"{metaKey}\", \"fileSizeBytes\": {item.FileSizeBytes}}}";
            context.Log($"[Calculador Hash] {algo}: {hashResult[..Math.Min(16, hashResult.Length)]}...", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            await context.EmitAsync("Out", item);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"file\": \"{item.CurrentPath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Calculador Hash] Error al calcular hash: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"Hash computation failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static async Task<string> ComputeHashAsync(IStorageService storage, string filePath, string algorithm, CancellationToken ct)
    {
        await using var stream = await storage.OpenReadAsync(filePath, ct).ConfigureAwait(false);

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
