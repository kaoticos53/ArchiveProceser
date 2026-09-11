using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace FileFlow.Plugin.Archives;

[NodeDefinition("ArchiveCompressorNode_Name", "Archives", "ArchiveCompressorNode_Desc", PipelineRole.Transform,
    "comprimir", "empaquetar", "zip", "7z", "targz", "comprimido", "compress", "archive")]
public sealed class ArchiveCompressorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ArchiveCompressorNode_Name", "Archive Compressor");
    public string Category => "Archives";
    public string Description => LocalizationManager.Instance.GetString("ArchiveCompressorNode_Desc", "Empaqueta y comprime archivos o directorios en formatos ZIP, TAR, GZ o 7Z.");

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
        ["ArchiveFormat"] = "ZIP",
        ["CompressionType"] = "Deflate",
        ["DestinationFolder"] = "",
        ["DestinationDirectory"] = "",
        ["ArchiveName"] = "{FileNameWithoutExtension}.zip"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("DestinationFolder", ParameterEditorType.FolderPath, DisplayOrder: 1),
        new("ArchiveName", ParameterEditorType.Text, DefaultValue: @"{FileNameWithoutExtension}.zip", DisplayOrder: 2),
        new("ArchiveFormat", ParameterEditorType.Dropdown, DefaultValue: "ZIP", DisplayOrder: 3, Options: ["ZIP", "TAR", "GZ", "7Z"]),
        new("CompressionType", ParameterEditorType.Dropdown, DefaultValue: "Deflate", DisplayOrder: 4, Options: ["Deflate", "Store", "LZMA", "BZip2"])
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string inputPath = item.CurrentPath;
        string destFolder = Parameters.TryGetValue("DestinationFolder", out var dfVal) && !string.IsNullOrWhiteSpace(dfVal?.ToString())
            ? ParameterHelper.GetString(dfVal, string.Empty)
            : (Parameters.TryGetValue("DestinationDirectory", out var ddVal) ? ParameterHelper.GetString(ddVal, string.Empty) : string.Empty);

        string destDir = !string.IsNullOrWhiteSpace(destFolder)
            ? ParameterHelper.ResolveOutputPath(destFolder, item)
            : (Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory());

        string archiveName = Parameters.TryGetValue("ArchiveName", out var aVal) ? ParameterHelper.GetString(aVal, "{FileNameWithoutExtension}.zip") : "{FileNameWithoutExtension}.zip";
        archiveName = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(archiveName, item);

        string formatStr = Parameters.TryGetValue("ArchiveFormat", out var fVal) ? ParameterHelper.GetString(fVal, "ZIP").ToUpperInvariant() : "ZIP";
        string compTypeStr = Parameters.TryGetValue("CompressionType", out var cVal) ? ParameterHelper.GetString(cVal, "Deflate").ToUpperInvariant() : "DEFLATE";

        var storage = context.GetStorage();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        bool fileExists = await storage.FileExistsAsync(inputPath, cancellationToken).ConfigureAwait(false);
        bool dirExists = await storage.DirectoryExistsAsync(inputPath, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(inputPath) || (!fileExists && !dirExists))
        {
            context.Log($"[Compresor] Ruta de entrada no encontrada: '{inputPath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            if (dirExists)
            {
                string fullInput = Path.GetFullPath(inputPath);
                string fullDest = Path.GetFullPath(destDir);
                if (fullDest.StartsWith(fullInput, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Destination directory '{destDir}' cannot be inside input source directory '{inputPath}' to prevent recursive compression loops.");
                }
            }

            if (!await storage.DirectoryExistsAsync(destDir, cancellationToken).ConfigureAwait(false))
            {
                await storage.CreateDirectoryAsync(destDir, cancellationToken).ConfigureAwait(false);
            }

            string targetArchivePath = Path.Combine(destDir, archiveName);

            ArchiveType archiveType = formatStr switch
            {
                "TAR" => ArchiveType.Tar,
                "GZ" => ArchiveType.GZip,
                "7Z" => ArchiveType.SevenZip,
                _ => ArchiveType.Zip
            };

            CompressionType compType = compTypeStr switch
            {
                "STORE" or "NONE" => CompressionType.None,
                "LZMA" => CompressionType.LZMA,
                "BZIP2" => CompressionType.BZip2,
                "PPMD" => CompressionType.PPMd,
                _ => CompressionType.Deflate
            };

            await using (var stream = await storage.OpenWriteAsync(targetArchivePath, cancellationToken).ConfigureAwait(false))
            using (var writer = WriterFactory.OpenWriter(stream, archiveType, new WriterOptions(compType)))
            {
                if (fileExists)
                {
                    await using var inStream = await storage.OpenReadAsync(inputPath, cancellationToken).ConfigureAwait(false);
                    writer.Write(Path.GetFileName(inputPath), inStream);
                }
                else if (dirExists)
                {
                    var filesToPack = Directory.GetFiles(inputPath, "*.*", SearchOption.AllDirectories);
                    foreach (var file in filesToPack)
                    {
                        string relativeEntryName = Path.GetRelativePath(inputPath, file).Replace('\\', '/');
                        await using var inStream = await storage.OpenReadAsync(file, cancellationToken).ConfigureAwait(false);
                        writer.Write(relativeEntryName, inStream);
                    }
                }
            }

            sw.Stop();
            long outSize = await storage.FileExistsAsync(targetArchivePath, cancellationToken).ConfigureAwait(false)
                ? await storage.GetFileSizeAsync(targetArchivePath, cancellationToken).ConfigureAwait(false)
                : 0;
            double compressionRatio = item.FileSizeBytes > 0 ? (double)outSize / item.FileSizeBytes * 100.0 : 100.0;

            var outputItem = item.DeepClone();
            outputItem.CurrentPath = targetArchivePath;
            outputItem.IsDirectory = false;
            outputItem.FileSizeBytes = outSize;
            outputItem.Metadata["CompressedFrom"] = inputPath;
            outputItem.Metadata["ArchiveFormat"] = formatStr;
            outputItem.AddLog($"ArchiveCompressorNode created archive {targetArchivePath}");

            string detailsJson = $"{{\"archiveFormat\": \"{formatStr}\", \"compressionType\": \"{compTypeStr}\", \"targetPath\": \"{targetArchivePath.Replace("\\", "\\\\")}\", \"originalSizeBytes\": {item.FileSizeBytes}, \"compressedSizeBytes\": {outSize}, \"ratioPct\": {compressionRatio.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}}}";
            context.Log($"[Compresor] Archivo {formatStr} generado ({compTypeStr}): '{Path.GetFileName(targetArchivePath)}' (Ratio: {compressionRatio:F1}%)", LogLevel.Information, outputItem, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            await context.EmitAsync("Out", outputItem);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"input\": \"{inputPath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Compresor] Error al comprimir archivo: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"ArchiveCompressorNode error: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }
}
