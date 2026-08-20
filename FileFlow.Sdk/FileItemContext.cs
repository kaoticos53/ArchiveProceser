namespace FileFlow.Sdk;

public record FileItemContext
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CurrentPath { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long FileSizeBytes { get; set; }
    public Dictionary<string, object?> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ExecutionLog { get; init; } = [];

    public FileItemContext() { }

    public FileItemContext(string path, bool isDirectory = false)
    {
        CurrentPath = path;
        OriginalPath = path;
        IsDirectory = isDirectory;
        if (!isDirectory && File.Exists(path))
        {
            FileSizeBytes = new FileInfo(path).Length;
        }
        else if (isDirectory && Directory.Exists(path))
        {
            FileSizeBytes = 0;
        }
    }

    public void AddLog(string message)
    {
        string timestamped = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        ExecutionLog.Add(timestamped);
    }
}
