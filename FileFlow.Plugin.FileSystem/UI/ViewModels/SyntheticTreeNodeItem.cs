using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFlow.Plugin.FileSystem.Services;
using FileFlow.Sdk.SyntheticData;

namespace FileFlow.Plugin.FileSystem.UI.ViewModels;

/// <summary>
/// Representa un nodo observable (archivo o carpeta) dentro de la jerarquía de árbol del diseñador de datos sintéticos.
/// </summary>
public partial class SyntheticTreeNodeItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _relativePath = string.Empty;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private bool _isArchive;

    [ObservableProperty]
    private bool _isArchiveEntry;

    [ObservableProperty]
    private long _fileSizeBytes;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private SyntheticTreeNodeItem? _parent;

    public ObservableCollection<SyntheticTreeNodeItem> Children { get; } = [];

    public Dictionary<string, object?> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<SyntheticArchiveEntryDefinition> SimulatedArchiveEntries { get; } = [];

    public SyntheticFileDefinition UnderlyingDefinition { get; set; }

    public string SizeFormatted
    {
        get
        {
            if (IsDirectory)
            {
                long totalSize = CalculateTotalRecursiveBytes();
                return SyntheticTreeDslParser.FormatSize(totalSize);
            }
            return SyntheticTreeDslParser.FormatSize(FileSizeBytes);
        }
    }

    public string BadgeText
    {
        get
        {
            if (IsDirectory)
            {
                int fileCount = CalculateTotalRecursiveFiles();
                return $"{fileCount} arch. • {SizeFormatted}";
            }
            if (IsArchiveEntry)
            {
                return $"[Interno] • {SizeFormatted}";
            }
            if (IsArchive && SimulatedArchiveEntries.Count > 0)
            {
                return $"📦 {SimulatedArchiveEntries.Count} entradas • {SizeFormatted}";
            }
            return SizeFormatted;
        }
    }

    public string IconGlyph
    {
        get
        {
            if (IsDirectory) return "📁";
            if (IsArchive) return "📦";

            string ext = Path.GetExtension(Name).ToLowerInvariant();
            return ext switch
            {
                ".mkv" or ".mp4" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm" or ".m4v" => "🎬",
                ".mp3" or ".flac" or ".wav" or ".m4a" or ".aac" or ".ogg" or ".wma" => "🎵",
                ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp" or ".tiff" or ".heic" => "🖼️",
                ".pdf" or ".doc" or ".docx" or ".txt" or ".rtf" or ".odt" or ".epub" or ".md" => "📄",
                ".xlsx" or ".xls" or ".csv" or ".tsv" or ".parquet" or ".json" or ".xml" => "📊",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz" => "📦",
                ".exe" or ".dll" or ".iso" or ".bin" or ".dat" => "💾",
                ".cs" or ".py" or ".js" or ".ts" or ".html" or ".css" or ".sh" or ".ps1" => "📜",
                _ => "📄"
            };
        }
    }

    public SyntheticTreeNodeItem(SyntheticFileDefinition definition, SyntheticTreeNodeItem? parent = null)
    {
        UnderlyingDefinition = definition;
        _parent = parent;
        _name = definition.FileName;
        _relativePath = definition.RelativePath;
        _isDirectory = definition.IsDirectory;
        _fileSizeBytes = definition.FileSizeBytes;
        _isArchive = definition.IsArchive;

        if (definition.Metadata != null)
        {
            foreach (var kvp in definition.Metadata)
            {
                Metadata[kvp.Key] = kvp.Value;
            }
        }

        if (definition.SimulatedArchiveEntries != null)
        {
            foreach (var entry in definition.SimulatedArchiveEntries)
            {
                SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition
                {
                    InnerPath = entry.InnerPath,
                    FileSizeBytes = entry.FileSizeBytes,
                    IsDirectory = entry.IsDirectory,
                    Metadata = new Dictionary<string, object?>(entry.Metadata, StringComparer.OrdinalIgnoreCase)
                });
            }
        }
    }

    public SyntheticTreeNodeItem(string name, string relativePath, bool isDirectory, long sizeBytes = 1024, SyntheticTreeNodeItem? parent = null)
    {
        UnderlyingDefinition = new SyntheticFileDefinition(relativePath, sizeBytes, isDirectory);
        _parent = parent;
        _name = name;
        _relativePath = relativePath;
        _isDirectory = isDirectory;
        _fileSizeBytes = sizeBytes;
        _isArchive = UnderlyingDefinition.IsArchive;
    }

    partial void OnNameChanged(string value)
    {
        UpdateRelativePathFromParent();
        OnPropertyChanged(nameof(IconGlyph));
        OnPropertyChanged(nameof(BadgeText));
    }

    partial void OnFileSizeBytesChanged(long value)
    {
        if (UnderlyingDefinition != null)
        {
            UnderlyingDefinition.FileSizeBytes = value;
        }
        OnPropertyChanged(nameof(SizeFormatted));
        OnPropertyChanged(nameof(BadgeText));
        NotifyParentMetricsChanged();
    }

    partial void OnIsDirectoryChanged(bool value)
    {
        if (UnderlyingDefinition != null)
        {
            UnderlyingDefinition.IsDirectory = value;
        }
        OnPropertyChanged(nameof(IconGlyph));
        OnPropertyChanged(nameof(SizeFormatted));
        OnPropertyChanged(nameof(BadgeText));
        NotifyParentMetricsChanged();
    }

    public void UpdateRelativePathFromParent()
    {
        if (Parent == null || string.IsNullOrWhiteSpace(Parent.RelativePath))
        {
            RelativePath = Name;
        }
        else
        {
            string parentPath = Parent.RelativePath.TrimEnd('/');
            RelativePath = $"{parentPath}/{Name}";
        }

        if (UnderlyingDefinition != null)
        {
            UnderlyingDefinition.RelativePath = RelativePath;
        }

        foreach (var child in Children)
        {
            child.UpdateRelativePathFromParent();
        }
    }

    public long CalculateTotalRecursiveBytes()
    {
        if (!IsDirectory) return FileSizeBytes;
        long total = 0;
        foreach (var child in Children)
        {
            if (!child.IsArchiveEntry)
            {
                total += child.CalculateTotalRecursiveBytes();
            }
        }
        return total;
    }

    public int CalculateTotalRecursiveFiles()
    {
        if (!IsDirectory) return 1;
        int count = 0;
        foreach (var child in Children)
        {
            if (!child.IsArchiveEntry)
            {
                count += child.CalculateTotalRecursiveFiles();
            }
        }
        return count;
    }

    public void NotifyParentMetricsChanged()
    {
        OnPropertyChanged(nameof(SizeFormatted));
        OnPropertyChanged(nameof(BadgeText));
        Parent?.NotifyParentMetricsChanged();
    }

    public void SetExpandedRecursively(bool expanded)
    {
        IsExpanded = expanded;
        foreach (var child in Children)
        {
            child.SetExpandedRecursively(expanded);
        }
    }
}
