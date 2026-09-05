using System.Runtime.InteropServices;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("SafeRecycleDeleteNode_Name", "Files", "SafeRecycleDeleteNode_Desc", PipelineRole.Sink,
    "papelera", "borrar", "eliminar", "recycle", "delete", "trash", "recyclebin")]
public class SafeRecycleDeleteNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SafeRecycleDeleteNode_Name", "Safe Recycle Delete");
    public string Category => "Files";
    public string Description => LocalizationManager.Instance.GetString("SafeRecycleDeleteNode_Desc", "Sends files or folders to Windows Recycle Bin using native Shell API, ensuring they are recoverable and supporting rollback.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Deleted", typeof(FileItemContext), PortDirection.Output, "Deleted"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DeleteOriginalPath"] = false,
        ["ConfirmRecycle"] = true
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        bool deleteOriginal = Parameters.TryGetValue("DeleteOriginalPath", out var dVal) && ParameterHelper.GetBoolean(dVal, false);
        string targetPath = deleteOriginal ? item.OriginalPath : item.CurrentPath;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(targetPath) || (!File.Exists(targetPath) && !Directory.Exists(targetPath)))
        {
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_SafeRecycle_NotFound", "[Safe Recycle] File or folder not found: '{0}'", targetPath), LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        if (context.IsDryRun)
        {
            context.RegisterPlannedAction(new PlannedAction(
                Guid.NewGuid(),
                Id,
                Name,
                PlannedOperationType.Recycle,
                targetPath,
                null,
                "Send to Windows Recycle Bin",
                item.FileSizeBytes
            ));
            item.AddLog($"[DryRun] Planned Safe Recycle Delete: {targetPath}");
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_SafeRecycle_DryRun", "[Safe Recycle] [DryRun] Planned sending to Recycle Bin: '{0}'", targetPath), LogLevel.Information, item);
            await context.EmitAsync("Deleted", item);
            return;
        }

        try
        {
            bool success = SendToWindowsRecycleBin(targetPath);
            sw.Stop();

            if (success)
            {
                context.RecordJournalEntry(new JournalEntry(
                    Guid.NewGuid(),
                    Id,
                    JournalOperationType.DeletedToRecycleBin,
                    targetPath,
                    null,
                    Notes: "Sent to Windows Recycle Bin via Shell API"
                ));

                string detailsJson = $"{{\"targetPath\": \"{targetPath.Replace("\\", "\\\\")}\", \"fileSizeBytes\": {item.FileSizeBytes}, \"deleteOriginal\": {deleteOriginal.ToString().ToLowerInvariant()}}}";
                context.Log(LocalizationManager.Instance.GetFormattedString("Log_SafeRecycle_Success", "[Safe Recycle] Item successfully sent to Recycle Bin: '{0}'", Path.GetFileName(targetPath)), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

                item.AddLog($"Sent to Windows Recycle Bin: {targetPath}");
                await context.EmitAsync("Deleted", item);
            }
            else
            {
                throw new IOException($"Windows Shell API failed to recycle '{targetPath}'.");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"targetPath\": \"{targetPath.Replace("\\", "\\\\")}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_SafeRecycle_Error", "[Safe Recycle] Error sending to Recycle Bin: {0}", ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"Recycle failed: {ex.Message}");
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
                // Windows SHFileOperation requiere doble null-terminator (\0\0)
                IntPtr pFrom = Marshal.StringToHGlobalUni(fullPath + "\0\0");
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
                    Marshal.FreeHGlobal(pFrom);
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public IntPtr pFrom;
        public IntPtr pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public IntPtr lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation([In, Out] ref SHFILEOPSTRUCT lpFileOp);
}
