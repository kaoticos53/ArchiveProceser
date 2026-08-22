using System.Runtime.InteropServices;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("SafeRecycleDeleteNode_Name", "FileSystem", "SafeRecycleDeleteNode_Desc")]
public class SafeRecycleDeleteNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SafeRecycleDeleteNode_Name", "Borrado Seguro a Papelera");
    public string Category => "FileSystem";
    public string Description => LocalizationManager.Instance.GetString("SafeRecycleDeleteNode_Desc", "Envía archivos o carpetas a la Papelera de reciclaje de Windows mediante la API nativa del Shell, garantizando que sean recuperables y permitiendo deshacer la operación (Rollback).");


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

        if (string.IsNullOrWhiteSpace(targetPath) || (!File.Exists(targetPath) && !Directory.Exists(targetPath)))
        {
            context.Log($"[SafeRecycleDeleteNode] Target file or directory not found: '{targetPath}'", LogLevel.Warning);
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
            await context.EmitAsync("Deleted", item);
            return;
        }

        try
        {
            bool success = SendToWindowsRecycleBin(targetPath);
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

                context.Log($"[SafeRecycleDeleteNode] Successfully sent to Recycle Bin: {targetPath}", LogLevel.Information);
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
            context.Log($"[SafeRecycleDeleteNode] Error recycling '{targetPath}': {ex.Message}", LogLevel.Error);
            item.AddLog($"Recycle failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static bool SendToWindowsRecycleBin(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            string nullTerminatedPath = Path.GetFullPath(path) + "\0\0";
            var fileOp = new SHFILEOPSTRUCT
            {
                hwnd = IntPtr.Zero,
                wFunc = 0x0003, // FO_DELETE
                pFrom = nullTerminatedPath,
                pTo = null,
                fFlags = 0x0040 | 0x0010 | 0x0004 | 0x0400, // FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
                fAnyOperationsAborted = false,
                hNameMappings = IntPtr.Zero,
                lpszProgressTitle = null
            };

            int result = SHFileOperation(ref fileOp);
            return result == 0 && !fileOp.fAnyOperationsAborted;
        }
        catch
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                return true;
            }
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation([In, Out] ref SHFILEOPSTRUCT lpFileOp);
}
