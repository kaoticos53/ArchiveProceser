using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace FileFlow.Plugin.Archives;

[NodeDefinition("ArchiveCompressorNode_Name", "Archives", "ArchiveCompressorNode_Desc")]
public class ArchiveCompressorNode : IFlowNode
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
        ["DestinationDirectory"] = @"{RelativeDir}\Compressed",
        ["ArchiveName"] = @"{FileNameNoExt}_archive.zip",
        ["ArchiveFormat"] = "ZIP", // ZIP, TAR, GZ, 7Z
        ["CompressionType"] = "Deflate" // Deflate, Store, LZMA, BZip2
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string inputPath = item.CurrentPath;
        string destDirPattern = Parameters.TryGetValue("DestinationDirectory", out var dVal) ? ParameterHelper.GetString(dVal, "Compressed") : "Compressed";
        string destDir = ParameterHelper.ResolveOutputPath(destDirPattern, item);

        string archiveName = Parameters.TryGetValue("ArchiveName", out var aVal) ? ParameterHelper.GetString(aVal, @"{FileNameNoExt}_archive.zip") : @"{FileNameNoExt}_archive.zip";
        archiveName = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(archiveName, item);

        string formatStr = Parameters.TryGetValue("ArchiveFormat", out var fVal) ? ParameterHelper.GetString(fVal, "ZIP").ToUpperInvariant() : "ZIP";
        string compTypeStr = Parameters.TryGetValue("CompressionType", out var cVal) ? ParameterHelper.GetString(cVal, "Deflate").ToUpperInvariant() : "DEFLATE";

        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(inputPath) || (!File.Exists(inputPath) && !Directory.Exists(inputPath)))
        {
            context.Log($"[Compresor] Ruta de entrada no encontrada: '{inputPath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            if (Directory.Exists(inputPath))
            {
                string fullInput = Path.GetFullPath(inputPath);
                string fullDest = Path.GetFullPath(destDir);
                if (fullDest.StartsWith(fullInput, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Destination directory '{destDir}' cannot be inside input source directory '{inputPath}' to prevent recursive compression loops.");
                }
            }

            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
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

            using (var stream = File.Create(targetArchivePath))
            using (var writer = WriterFactory.OpenWriter(stream, archiveType, new WriterOptions(compType)))
            {
                if (File.Exists(inputPath))
                {
                    writer.Write(Path.GetFileName(inputPath), inputPath);
                }
                else if (Directory.Exists(inputPath))
                {
                    writer.WriteAll(inputPath, "*", SearchOption.AllDirectories);
                }
            }

            sw.Stop();
            long outSize = new FileInfo(targetArchivePath).Length;
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
