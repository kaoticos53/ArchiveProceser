using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("DirectoryInspectorNode_Name", "FileSystem", "DirectoryInspectorNode_Desc")]
public class DirectoryInspectorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("DirectoryInspectorNode_Name", "Directory Inspector");
    public string Category => "FileSystem";
    public string Description => LocalizationManager.Instance.GetString("DirectoryInspectorNode_Desc", "Evaluates folder contents to classify archive and file composition.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("SingleArchive", typeof(FileItemContext), PortDirection.Output, "Single Archive"),
        new NodePort("MixedContent", typeof(FileItemContext), PortDirection.Output, "Mixed Content"),
        new NodePort("DirectoriesOnly", typeof(FileItemContext), PortDirection.Output, "Directories Only")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".tgz"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string path = item.CurrentPath;
        string dirPath = item.IsDirectory ? path : (Path.GetDirectoryName(path) ?? path);

        if (!Directory.Exists(dirPath))
        {
            context.Log($"DirectoryInspectorNode: Path '{dirPath}' does not exist.", LogLevel.Warning);
            await context.EmitAsync("MixedContent", item);
            return;
        }

        string[] files = Directory.GetFiles(dirPath);
        string[] subdirs = Directory.GetDirectories(dirPath);

        if (files.Length == 0 && subdirs.Length > 0)
        {
            item.AddLog("DirectoryInspectorNode classified as DirectoriesOnly");
            await context.EmitAsync("DirectoriesOnly", item);
            return;
        }

        if (files.Length == 1 && subdirs.Length == 0)
        {
            string ext = Path.GetExtension(files[0]);
            if (ArchiveExtensions.Contains(ext))
            {
                item.AddLog($"DirectoryInspectorNode classified as SingleArchive ({files[0]})");
                await context.EmitAsync("SingleArchive", item);
                return;
            }
        }

        item.AddLog("DirectoryInspectorNode classified as MixedContent");
        await context.EmitAsync("MixedContent", item);
    }
}
