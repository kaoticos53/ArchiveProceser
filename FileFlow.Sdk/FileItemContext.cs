namespace FileFlow.Sdk;

public record FileItemContext
{
    private readonly Guid _id = Guid.NewGuid();
    private string? _idString;
    private string? _shortIdString;
    private string _currentPath = string.Empty;
    private string? _fileName;

    public Guid Id
    {
        get => _id;
        init
        {
            _id = value;
            _idString = value.ToString();
            _shortIdString = _idString.Length > 8 ? _idString[..8] : _idString;
        }
    }

    public string IdString => _idString ??= _id.ToString();
    public string ShortIdString => _shortIdString ??= (IdString.Length > 8 ? IdString[..8] : IdString);

    public string CurrentPath
    {
        get => _currentPath;
        set
        {
            _currentPath = value ?? string.Empty;
            _fileName = null; // Invalidate cached filename
        }
    }

    public string FileName => _fileName ??= (!string.IsNullOrWhiteSpace(_currentPath) ? Path.GetFileName(_currentPath) : string.Empty);

    public string OriginalPath { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long FileSizeBytes { get; set; }
    public Dictionary<string, object?> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ExecutionLog { get; init; } = [];

    public string GetExistingPhysicalPath()
    {
        if (!string.IsNullOrWhiteSpace(CurrentPath) && (File.Exists(CurrentPath) || Directory.Exists(CurrentPath)))
            return CurrentPath;
        if (!string.IsNullOrWhiteSpace(PhysicalPath) && (File.Exists(PhysicalPath) || Directory.Exists(PhysicalPath)))
            return PhysicalPath;
        if (!string.IsNullOrWhiteSpace(OriginalPath) && (File.Exists(OriginalPath) || Directory.Exists(OriginalPath)))
            return OriginalPath;
        return !string.IsNullOrWhiteSpace(CurrentPath) ? CurrentPath : (!string.IsNullOrWhiteSpace(PhysicalPath) ? PhysicalPath : OriginalPath);
    }

    public FileItemContext() { }

    public FileItemContext(string path, bool isDirectory = false)
    {
        CurrentPath = path;
        OriginalPath = path;
        PhysicalPath = path;
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

    public FileItemContext(FileInfo fileInfo)
    {
        CurrentPath = fileInfo.FullName;
        OriginalPath = fileInfo.FullName;
        PhysicalPath = fileInfo.FullName;
        IsDirectory = false;
        FileSizeBytes = fileInfo.Length;
    }

    public FileItemContext(DirectoryInfo dirInfo)
    {
        CurrentPath = dirInfo.FullName;
        OriginalPath = dirInfo.FullName;
        PhysicalPath = dirInfo.FullName;
        IsDirectory = true;
        FileSizeBytes = 0;
    }

    public void AddLog(string message)
    {
        string timestamped = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        ExecutionLog.Add(timestamped);
    }

    public FileItemContext DeepClone()
    {
        var clone = new FileItemContext
        {
            Id = Id,
            CurrentPath = CurrentPath,
            OriginalPath = OriginalPath,
            PhysicalPath = PhysicalPath,
            IsDirectory = IsDirectory,
            FileSizeBytes = FileSizeBytes,
            Metadata = Metadata.Count > 0 ? new Dictionary<string, object?>(Metadata, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, object?>(0, StringComparer.OrdinalIgnoreCase),
            Tags = Tags.Count > 0 ? new HashSet<string>(Tags, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(0, StringComparer.OrdinalIgnoreCase),
            ExecutionLog = ExecutionLog.Count > 0 ? new List<string>(ExecutionLog) : new List<string>(0)
        };
        clone._idString = _idString;
        clone._shortIdString = _shortIdString;
        clone._fileName = _fileName;
        return clone;
    }
}
