using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("OriginalFileActionNode_Name", "Files", "OriginalFileActionNode_Desc", PipelineRole.Transform,
    "origen", "cuarentena", "papelera", "conservar", "ciclo de vida", "quarantine", "original", "cleanup")]
public class OriginalFileActionNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("OriginalFileActionNode_Name", "Original File Action");
    public string Category => "Files";
    public string Description => LocalizationManager.Instance.GetString("OriginalFileActionNode_Desc", "Applies lifecycle policy to the original file (keep, quarantine, or delete).");

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
        ["ActionType"] = "Keep",
        ["QuarantinePath"] = @"{RelativeDir}\Quarantine"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("ActionType", ParameterEditorType.Dropdown, DefaultValue: "Keep", DisplayOrder: 1, Options: ["Keep", "MoveToRecycleBin", "MoveToQuarantine", "PermanentDelete"]),
        new("QuarantinePath", ParameterEditorType.FolderPath, DefaultValue: @"{RelativeDir}\Quarantine", DisplayOrder: 2)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string actionType = Parameters.TryGetValue("ActionType", out var val) ? ParameterHelper.GetString(val, "Keep") : "Keep";
        string quarantinePattern = Parameters.TryGetValue("QuarantinePath", out var qVal) ? ParameterHelper.GetString(qVal, @"{RelativeDir}\Quarantine") : @"{RelativeDir}\Quarantine";
        string quarantinePath = ParameterHelper.ResolveOutputPath(quarantinePattern, item);
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        string targetFilePath = item.OriginalPath;
        if (string.IsNullOrWhiteSpace(targetFilePath) || (!File.Exists(targetFilePath) && !Directory.Exists(targetFilePath)))
        {
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_SourceNotFound", "[Original File Action] Original file not found: '{0}'", targetFilePath), LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            switch (actionType.ToUpperInvariant())
            {
                case "KEEP":
                    context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_Keep", "[Original File Action] Keeping original file intact: '{0}'", targetFilePath), LogLevel.Information, item);
                    break;

                case "MOVETORECYCLEBIN":
                    string detailsRecycle = $"{{\"action\": \"MoveToRecycleBin\", \"targetPath\": \"{targetFilePath.Replace("\\", "\\\\")}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}}}";
                    context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_Recycle", "[Original File Action] Sending original to Recycle Bin: '{0}' (DryRun={1})", targetFilePath, isDryRun), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsRecycle);
                    if (!isDryRun)
                    {
                        bool recycled = SendToWindowsRecycleBin(targetFilePath);
                        if (!recycled)
                        {
                            throw new IOException($"Windows Shell API failed to send original file '{targetFilePath}' to Recycle Bin.");
                        }
                    }
                    break;

                case "MOVETOQUARANTINE":
                    if (!Directory.Exists(quarantinePath) && !isDryRun)
                    {
                        Directory.CreateDirectory(quarantinePath);
                    }
                    string destPath = Path.Combine(quarantinePath, Path.GetFileName(targetFilePath));
                    string detailsMove = $"{{\"action\": \"MoveToQuarantine\", \"quarantinePath\": \"{destPath.Replace("\\", "\\\\")}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}}}";
                    context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_Quarantine", "[Original File Action] Moving original to quarantine: '{0}' (DryRun={1})", destPath, isDryRun), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsMove);
                    if (!isDryRun)
                    {
                        if (item.IsDirectory)
                        {
                            Directory.Move(targetFilePath, destPath);
                        }
                        else
                        {
                            File.Move(targetFilePath, destPath, overwrite: true);
                        }
                    }
                    break;

                case "PERMANENTDELETE":
                    string detailsDelete = $"{{\"action\": \"PermanentDelete\", \"targetPath\": \"{targetFilePath.Replace("\\", "\\\\")}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}}}";
                    context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_PermanentDelete", "[Original File Action] Permanently deleting original: '{0}' (DryRun={1})", targetFilePath, isDryRun), LogLevel.Warning, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsDelete);
                    if (!isDryRun)
                    {
                        if (item.IsDirectory)
                        {
                            Directory.Delete(targetFilePath, recursive: true);
                        }
                        else
                        {
                            File.Delete(targetFilePath);
                        }
                    }
                    break;

                default:
                    context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_UnknownPolicy", "[Original File Action] Unknown action policy: '{0}', retaining file.", actionType), LogLevel.Warning, item);
                    break;
            }

            sw.Stop();
            item.AddLog($"OriginalFileActionNode applied policy '{actionType}'");
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"targetPath\": \"{targetFilePath.Replace("\\", "\\\\")}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_Error", "[Original File Action] Error applying policy: {0}", ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"OriginalFileActionNode failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static bool SendToWindowsRecycleBin(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                string fullPath = Path.GetFullPath(path);
                IntPtr pFrom = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(fullPath + "\0\0");
                try
                {
                    var fileOp = new SHFILEOPSTRUCT
                    {
                        hwnd = IntPtr.Zero,
                        wFunc = 0x0003, // FO_DELETE
                        pFrom = pFrom,
                        pTo = IntPtr.Zero,
                        fFlags = 0x0040 | 0x0010 | 0x0004 | 0x0400, // FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
                        fAnyOperationsAborted = false,
                        hNameMappings = IntPtr.Zero,
                        lpszProgressTitle = IntPtr.Zero
                    };

                    int result = SHFileOperation(ref fileOp);
                    return result == 0 && !fileOp.fAnyOperationsAborted;
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(pFrom);
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public IntPtr pFrom;
        public IntPtr pTo;
        public ushort fFlags;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public IntPtr lpszProgressTitle;
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SHFileOperation([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] ref SHFILEOPSTRUCT lpFileOp);
}
