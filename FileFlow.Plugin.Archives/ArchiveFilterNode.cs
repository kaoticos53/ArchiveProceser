using System.Text.RegularExpressions;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Archives;

[NodeDefinition("ArchiveFilterNode_Name", "Archives", "ArchiveFilterNode_Desc", PipelineRole.Filter,
    "inspeccionar", "filtrar", "comprimido", "contenido", "zip", "tar", "filter", "archive")]
public class ArchiveFilterNode : IFlowNode
{
    private static readonly Regex SecondaryVolumeRegex = new(
        @"\.(r\d{2,3}|z\d{2,3}|part(?!0*1\.)\d+\.rar)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PrimaryArchiveRegex = new(
        @"\.(zip|rar|7z|tar|gz|tgz|bz2|xz|part0*1\.rar)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ArchiveFilterNode_Name", "Clasificador de Archivos Comprimidos");
    public string Category => "Archives";
    public string Description => LocalizationManager.Instance.GetString("ArchiveFilterNode_Desc", "Clasifica elementos entrantes entre archivos comprimidos principales, volúmenes secundarios divididos (split-RAR) y archivos normales.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Archive", typeof(FileItemContext), PortDirection.Output, "Archive"),
        new NodePort("RegularFile", typeof(FileItemContext), PortDirection.Output, "RegularFile"),
        new NodePort("SecondaryVolume", typeof(FileItemContext), PortDirection.Output, "SecondaryVolume")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FilterSecondaryVolumes"] = true
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string filePath = item.CurrentPath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            await context.EmitAsync("RegularFile", item);
            return;
        }

        string fileName = Path.GetFileName(filePath);

        // 1. Check if it's a secondary split-volume (e.g. .r01, .r02, .part02.rar)
        if (SecondaryVolumeRegex.IsMatch(fileName))
        {
            string detailsJson = $"{{\"classification\": \"SecondaryVolume\", \"fileName\": \"{fileName}\"}}";
            context.Log($"[Filtro Archivos] Volumen secundario detectado: '{fileName}' -> Rama 'SecondaryVolume'", LogLevel.Information, item, durationMs: 0.0, detailsJson: detailsJson);
            item.AddLog($"ArchiveFilterNode classified as SecondaryVolume ({fileName})");
            item.Metadata["IsSecondaryArchiveVolume"] = true;
            await context.EmitAsync("SecondaryVolume", item);
            return;
        }

        // 2. Check if it's a primary archive (.zip, .rar, .7z, .part01.rar)
        if (PrimaryArchiveRegex.IsMatch(fileName))
        {
            string detailsJson = $"{{\"classification\": \"PrimaryArchive\", \"fileName\": \"{fileName}\"}}";
            context.Log($"[Filtro Archivos] Archivo comprimido principal detectado: '{fileName}' -> Rama 'Archive'", LogLevel.Information, item, durationMs: 0.0, detailsJson: detailsJson);
            item.AddLog($"ArchiveFilterNode classified as PrimaryArchive ({fileName})");
            item.Metadata["IsPrimaryArchive"] = true;
            await context.EmitAsync("Archive", item);
            return;
        }

        // 3. Uncompressed regular file
        context.Log($"[Filtro Archivos] Archivo regular no comprimido: '{fileName}' -> Rama 'RegularFile'", LogLevel.Debug, item);
        item.AddLog($"ArchiveFilterNode classified as RegularFile ({fileName})");
        await context.EmitAsync("RegularFile", item);
    }
}
