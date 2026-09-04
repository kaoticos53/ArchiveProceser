using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace FileFlow.Core.Engine;

/// <summary>
/// Representa el estado persistido de un punto de control para reanudar un flujo interrumpido.
/// </summary>
public class WorkflowCheckpointData
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");
    public string WorkflowName { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public HashSet<string> CompletedFileKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public long ProcessedItemsCount { get; set; }
}

/// <summary>
/// Gestor de puntos de control (checkpoints) y reanudación determinista de flujos interrumpidos.
/// </summary>
public class WorkflowCheckpointManager
{
    private static readonly Lazy<WorkflowCheckpointManager> _instance = new(() => new WorkflowCheckpointManager());
    public static WorkflowCheckpointManager Instance => _instance.Value;

    private readonly string _checkpointDirectory;
    private readonly Lock _lock = new();

    public WorkflowCheckpointManager(string? baseDir = null)
    {
        _checkpointDirectory = baseDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileFlowStudio",
            "checkpoints");

        try
        {
            if (!Directory.Exists(_checkpointDirectory))
            {
                Directory.CreateDirectory(_checkpointDirectory);
            }
        }
        catch { }
    }

    private string GetCheckpointFilePath(string workflowName)
    {
        string safeName = string.Join("_", workflowName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "unnamed_workflow";
        return Path.Combine(_checkpointDirectory, $"{safeName}.checkpoint.json");
    }

    public bool HasPendingCheckpoint(string workflowName, out WorkflowCheckpointData? checkpoint)
    {
        checkpoint = null;
        if (string.IsNullOrWhiteSpace(workflowName)) return false;

        string path = GetCheckpointFilePath(workflowName);
        if (!File.Exists(path)) return false;

        lock (_lock)
        {
            try
            {
                string json = File.ReadAllText(path);
                checkpoint = JsonSerializer.Deserialize<WorkflowCheckpointData>(json);
                return checkpoint != null && checkpoint.CompletedFileKeys.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public void SaveCheckpoint(WorkflowCheckpointData checkpoint)
    {
        if (checkpoint == null || string.IsNullOrWhiteSpace(checkpoint.WorkflowName)) return;

        string path = GetCheckpointFilePath(checkpoint.WorkflowName);
        lock (_lock)
        {
            try
            {
                string json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }

    public void ClearCheckpoint(string workflowName)
    {
        if (string.IsNullOrWhiteSpace(workflowName)) return;

        string path = GetCheckpointFilePath(workflowName);
        lock (_lock)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { }
        }
    }

    public int ClearAllCheckpoints()
    {
        int deletedCount = 0;
        lock (_lock)
        {
            try
            {
                if (Directory.Exists(_checkpointDirectory))
                {
                    var files = Directory.GetFiles(_checkpointDirectory, "*.checkpoint.json");
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
        return deletedCount;
    }
}
