using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("SwitchActiveFileNode_Name", "Logic", "SwitchActiveFileNode_Desc", PipelineRole.Control,
    "switch", "cambiar", "activar", "version", "original", "intercambiar")]
public sealed class SwitchActiveFileNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SwitchActiveFileNode_Name", "Cambiar Archivo Activo");
    public string Category => "Logic";
    public string Description => LocalizationManager.Instance.GetString("SwitchActiveFileNode_Desc", "Cambia el archivo activo del contexto (CurrentPath) por el archivo original o una versión registrada, con opción de eliminar el archivo intermedio actual.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TargetFile"] = "{OriginalPath}",
        ["DeleteCurrentFileFirst"] = false
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("TargetFile", ParameterEditorType.FileVersionSelector, DefaultValue: "{OriginalPath}", DisplayOrder: 1, HelpText: "Versión del archivo a establecer como activa (ej. Original, Actual o versiones intermedias upstream)"),
        new("DeleteCurrentFileFirst", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 2, HelpText: "Elimina el archivo intermedio actual antes de cambiar (nunca elimina el archivo original inmutable)")
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string targetPattern = Parameters.TryGetValue("TargetFile", out var tf) ? ParameterHelper.GetString(tf, "{OriginalPath}") : "{OriginalPath}";
        bool deleteCurrentFirst = Parameters.TryGetValue("DeleteCurrentFileFirst", out var dc) ? ParameterHelper.GetBoolean(dc, false) : false;

        string previousPath = item.CurrentPath;
        string targetPath = ResolveVersionOrPath(targetPattern, item);

        if (deleteCurrentFirst && !string.IsNullOrWhiteSpace(previousPath) && File.Exists(previousPath))
        {
            if (!string.Equals(Path.GetFullPath(previousPath), Path.GetFullPath(item.OriginalPath), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(previousPath);
                    context.Log($"[SwitchActiveFile] Deleted intermediate file before switch: '{previousPath}'", LogLevel.Debug, item);
                }
                catch (Exception ex)
                {
                    context.Log($"[SwitchActiveFile] Could not delete intermediate file '{previousPath}': {ex.Message}", LogLevel.Warning, item);
                }
            }
            else
            {
                context.Log($"[SwitchActiveFile] Preserved file because it is the original file: '{previousPath}'", LogLevel.Debug, item);
            }
        }

        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            item.CurrentPath = targetPath;
            item.PhysicalPath = targetPath;
            if (File.Exists(targetPath))
            {
                item.FileSizeBytes = new FileInfo(targetPath).Length;
            }
        }

        item.AddLog($"[SwitchActiveFile] Switched active file from '{previousPath}' to '{targetPath}'");
        context.Log($"[SwitchActiveFile] Active file switched to '{Path.GetFileName(targetPath)}'", LogLevel.Information, item);

        await context.EmitAsync("Out", item).ConfigureAwait(false);
    }

    private static string ResolveVersionOrPath(string input, FileItemContext item)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return item.CurrentPath;
        }

        string resolved = VariableTemplateResolver.Resolve(input, item);
        if (File.Exists(resolved))
        {
            return resolved;
        }

        string? versionPath = item.GetVersionPath(input);
        if (!string.IsNullOrWhiteSpace(versionPath) && File.Exists(versionPath))
        {
            return versionPath;
        }

        return resolved;
    }
}
