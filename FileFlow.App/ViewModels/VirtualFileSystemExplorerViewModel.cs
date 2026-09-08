using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.VirtualFileSystem;

namespace FileFlow.App.ViewModels;

public class VirtualDirectoryTreeNode : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public VirtualFileRole? Role { get; set; }
    public ObservableCollection<VirtualDirectoryTreeNode> Children { get; } = [];
    public int FileCount { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}

public record VirtualMetadataItem(string Category, string Key, string Value);

public partial class VirtualFileSystemExplorerViewModel : ObservableObject
{
    private readonly IVirtualFileSystemStore _store;
    private readonly IProcessLauncherService _processLauncher;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategoryFilter = "Todas";

    [ObservableProperty]
    private string _selectedRoleFilter = "Todos";

    [ObservableProperty]
    private VirtualDirectoryTreeNode? _selectedTreeNode;

    [ObservableProperty]
    private VirtualFileEntry? _selectedFile;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _totalSourceFiles;

    [ObservableProperty]
    private int _totalDestinationFiles;

    [ObservableProperty]
    private long _totalBytes;

    [ObservableProperty]
    private string _totalSizeFormatted = "0 B";

    [ObservableProperty]
    private int _totalDirectories;

    [ObservableProperty]
    private int _totalConflicts;

    [ObservableProperty]
    private string _asciiTreeText = string.Empty;

    public ObservableCollection<VirtualDirectoryTreeNode> DirectoryTreeNodes { get; } = [];
    public ObservableCollection<VirtualFileEntry> FilteredFiles { get; } = [];
    public ObservableCollection<VirtualMetadataItem> SelectedFileMetadata { get; } = [];

    public IReadOnlyList<string> AvailableRoles { get; } =
    [
        "Todos",
        "📥 Origen",
        "📤 Destino"
    ];

    public IReadOnlyList<string> AvailableCategories { get; } =
    [
        "Todas",
        "Películas",
        "Series",
        "Música",
        "Fotos",
        "Documentos",
        "Cómics y Manga",
        "Otros"
    ];

    public VirtualFileSystemExplorerViewModel(
        IVirtualFileSystemStore store,
        IProcessLauncherService? processLauncher = null,
        IDialogService? dialogService = null,
        ILocalizationService? localizationService = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _processLauncher = processLauncher ?? App.Services?.GetService(typeof(IProcessLauncherService)) as IProcessLauncherService ?? new ProcessLauncherService();
        _dialogService = dialogService ?? App.Services?.GetService(typeof(IDialogService)) as IDialogService ?? new WpfDialogService();
        _loc = localizationService ?? LocalizationManager.Instance;

        RefreshData();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedCategoryFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedRoleFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedTreeNodeChanged(VirtualDirectoryTreeNode? value)
    {
        ApplyFilter();
    }

    partial void OnSelectedFileChanged(VirtualFileEntry? value)
    {
        LoadSelectedFileMetadata(value);
    }

    [RelayCommand]
    public void RefreshData()
    {
        var allFiles = _store.GetAllFiles();
        TotalFiles = _store.TotalFiles;
        TotalBytes = _store.TotalBytes;
        TotalSizeFormatted = FormatBytes(TotalBytes);
        TotalConflicts = allFiles.Count(f => f.OperationType == VirtualOperationType.ConflictRenamed);
        TotalSourceFiles = allFiles.Count(f => f.Role == VirtualFileRole.Source);
        TotalDestinationFiles = allFiles.Count(f => f.Role == VirtualFileRole.Destination);
        AsciiTreeText = _store.GenerateAsciiTree();

        BuildDirectoryTree(allFiles);
        ApplyFilter();

        if (FilteredFiles.Count > 0 && SelectedFile == null)
        {
            SelectedFile = FilteredFiles[0];
        }
    }

    [RelayCommand]
    public void CopyAsciiTree()
    {
        try
        {
            string tree = !string.IsNullOrWhiteSpace(AsciiTreeText) ? AsciiTreeText : _store.GenerateAsciiTree();
            Clipboard.SetText(tree);
            _dialogService.ShowInformation(
                _loc.GetString("VfsExplorer_TreeCopied", "Árbol ASCII copiado al portapapeles con éxito."),
                _loc.GetString("VfsExplorer_Title", "Explorador de Archivos Virtual"));
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    public async Task MaterializeToSandboxAndOpenAsync()
    {
        try
        {
            string sandboxDir = Path.Combine(Path.GetTempPath(), "FileFlowStudio", "VFS_Sandbox", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            if (_store is VirtualFileSystemStore concreteStore)
            {
                await concreteStore.ExportToPhysicalDirectoryAsync(sandboxDir, CancellationToken.None);
            }
            else
            {
                Directory.CreateDirectory(sandboxDir);
                foreach (var file in _store.GetAllFiles())
                {
                    string safeName = Path.GetFileName(file.VirtualPath);
                    string dest = Path.Combine(sandboxDir, safeName);
                    await File.WriteAllTextAsync(dest, $"FileFlow Virtual File: {file.VirtualPath}\nOriginal: {file.OriginalPath}");
                }
            }

            _processLauncher.OpenFolder(sandboxDir);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    public void ClearVirtualFileSystem()
    {
        bool confirm = _dialogService.ShowConfirmation(
            _loc.GetString("VfsExplorer_ConfirmClear", "¿Deseas limpiar todos los archivos registrados en el Sistema de Archivos Virtual?"),
            _loc.GetString("VfsExplorer_Title", "Explorador de Archivos Virtual"));

        if (confirm)
        {
            _store.Clear();
            RefreshData();
        }
    }

    private void BuildDirectoryTree(IReadOnlyList<VirtualFileEntry> allFiles)
    {
        DirectoryTreeNodes.Clear();

        var rootNode = new VirtualDirectoryTreeNode
        {
            Name = _loc.GetString("VfsExplorer_AllDirectories", "📁 (Todas las carpetas)"),
            FullPath = string.Empty,
            Role = null,
            FileCount = allFiles.Count,
            IsExpanded = true,
            IsSelected = true
        };
        DirectoryTreeNodes.Add(rootNode);

        var sourceFiles = allFiles.Where(f => f.Role == VirtualFileRole.Source).ToList();
        if (sourceFiles.Count > 0)
        {
            var sourceRoot = new VirtualDirectoryTreeNode
            {
                Name = _loc.GetString("VfsExplorer_SourceFolders", "📥 Carpetas de Origen"),
                FullPath = string.Empty,
                Role = VirtualFileRole.Source,
                FileCount = sourceFiles.Count,
                IsExpanded = true
            };

            AddSubdirectoryNodes(sourceRoot, sourceFiles, VirtualFileRole.Source);
            rootNode.Children.Add(sourceRoot);
        }

        var destFiles = allFiles.Where(f => f.Role == VirtualFileRole.Destination).ToList();
        if (destFiles.Count > 0)
        {
            var destRoot = new VirtualDirectoryTreeNode
            {
                Name = _loc.GetString("VfsExplorer_DestinationFolders", "📤 Carpetas de Destino"),
                FullPath = string.Empty,
                Role = VirtualFileRole.Destination,
                FileCount = destFiles.Count,
                IsExpanded = true
            };

            AddSubdirectoryNodes(destRoot, destFiles, VirtualFileRole.Destination);
            rootNode.Children.Add(destRoot);
        }

        var intermediateFiles = allFiles.Where(f => f.Role == VirtualFileRole.Intermediate).ToList();
        if (intermediateFiles.Count > 0)
        {
            var interRoot = new VirtualDirectoryTreeNode
            {
                Name = _loc.GetString("VfsExplorer_IntermediateFolders", "⚙️ Archivos Intermedios"),
                FullPath = string.Empty,
                Role = VirtualFileRole.Intermediate,
                FileCount = intermediateFiles.Count,
                IsExpanded = true
            };

            AddSubdirectoryNodes(interRoot, intermediateFiles, VirtualFileRole.Intermediate);
            rootNode.Children.Add(interRoot);
        }

        var directories = allFiles
            .Select(f => f.DirectoryPath)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        TotalDirectories = directories.Count;
    }

    private static void AddSubdirectoryNodes(VirtualDirectoryTreeNode parentNode, List<VirtualFileEntry> files, VirtualFileRole role)
    {
        var dirs = files
            .Select(f => f.DirectoryPath)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var dir in dirs)
        {
            int countInDir = files.Count(f => string.Equals(f.DirectoryPath, dir, StringComparison.OrdinalIgnoreCase));
            string dirName = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(dirName)) dirName = dir;

            var dirNode = new VirtualDirectoryTreeNode
            {
                Name = dirName,
                FullPath = dir,
                Role = role,
                FileCount = countInDir,
                IsExpanded = true
            };

            parentNode.Children.Add(dirNode);
        }
    }

    private void ApplyFilter()
    {
        var allFiles = _store.GetAllFiles();
        IEnumerable<VirtualFileEntry> query = allFiles;

        // Filtro por rol del árbol si el nodo seleccionado tiene rol especificado
        if (SelectedTreeNode != null)
        {
            if (SelectedTreeNode.Role.HasValue)
            {
                query = query.Where(f => f.Role == SelectedTreeNode.Role.Value);
            }

            if (!string.IsNullOrWhiteSpace(SelectedTreeNode.FullPath))
            {
                query = query.Where(f => f.DirectoryPath.StartsWith(SelectedTreeNode.FullPath, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Filtro por rol desde el selector desplegable
        if (!string.IsNullOrWhiteSpace(SelectedRoleFilter) && !SelectedRoleFilter.Equals("Todos", StringComparison.OrdinalIgnoreCase) && !SelectedRoleFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (SelectedRoleFilter.Contains("Origen", StringComparison.OrdinalIgnoreCase) || SelectedRoleFilter.Contains("Source", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(f => f.Role == VirtualFileRole.Source);
            }
            else if (SelectedRoleFilter.Contains("Destino", StringComparison.OrdinalIgnoreCase) || SelectedRoleFilter.Contains("Destination", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(f => f.Role == VirtualFileRole.Destination);
            }
            else if (SelectedRoleFilter.Contains("Intermedio", StringComparison.OrdinalIgnoreCase) || SelectedRoleFilter.Contains("Intermediate", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(f => f.Role == VirtualFileRole.Intermediate);
            }
        }

        // Filtro por categoría
        if (!string.IsNullOrWhiteSpace(SelectedCategoryFilter) && !SelectedCategoryFilter.Equals("Todas", StringComparison.OrdinalIgnoreCase) && !SelectedCategoryFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(f =>
            {
                if (f.Metadata.TryGetValue("Category", out var cat) && cat != null &&
                    cat.ToString()!.Contains(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            });
        }

        // Filtro por texto de búsqueda
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string term = SearchText.Trim();
            query = query.Where(f =>
                f.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                f.VirtualPath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                f.OriginalPath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (f.DestinationPath != null && f.DestinationPath.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (f.RelatedSourcePath != null && f.RelatedSourcePath.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                f.Extension.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                f.Metadata.Values.Any(v => v != null && v.ToString()!.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        FilteredFiles.Clear();
        foreach (var item in query.OrderBy(f => f.VirtualPath, StringComparer.OrdinalIgnoreCase))
        {
            FilteredFiles.Add(item);
        }

        if (SelectedFile != null && !FilteredFiles.Contains(SelectedFile))
        {
            SelectedFile = FilteredFiles.FirstOrDefault();
        }
    }

    private void LoadSelectedFileMetadata(VirtualFileEntry? file)
    {
        SelectedFileMetadata.Clear();
        if (file == null) return;

        // Metadatos esenciales de sistema
        string roleDisplay = file.Role switch
        {
            VirtualFileRole.Source => "📥 Origen",
            VirtualFileRole.Destination => "📤 Destino",
            VirtualFileRole.Intermediate => "⚙️ Intermedio",
            _ => file.Role.ToString()
        };
        SelectedFileMetadata.Add(new VirtualMetadataItem("Archivo", "Rol en Pipeline", roleDisplay));
        SelectedFileMetadata.Add(new VirtualMetadataItem("Archivo", "Nombre Virtual", file.FileName));
        SelectedFileMetadata.Add(new VirtualMetadataItem("Archivo", "Ruta Virtual", file.VirtualPath));
        SelectedFileMetadata.Add(new VirtualMetadataItem("Archivo", "Ruta Original", file.OriginalPath));

        if (!string.IsNullOrWhiteSpace(file.DestinationPath))
        {
            SelectedFileMetadata.Add(new VirtualMetadataItem("Archivo", "Ruta de Destino", file.DestinationPath));
        }
        if (!string.IsNullOrWhiteSpace(file.RelatedSourcePath))
        {
            SelectedFileMetadata.Add(new VirtualMetadataItem("Archivo", "Origen Vinculado", file.RelatedSourcePath));
        }

        SelectedFileMetadata.Add(new VirtualMetadataItem("Archivo", "Extensión", file.Extension));
        SelectedFileMetadata.Add(new VirtualMetadataItem("Archivo", "Tamaño", $"{FormatBytes(file.FileSizeBytes)} ({file.FileSizeBytes:N0} bytes)"));
        SelectedFileMetadata.Add(new VirtualMetadataItem("Archivo", "Operación", file.OperationType.ToString()));
        SelectedFileMetadata.Add(new VirtualMetadataItem("Archivo", "Nodo Emisor", file.SourceNodeName));
        SelectedFileMetadata.Add(new VirtualMetadataItem("Archivo", "Fecha Registro", file.TimestampUtc.ToLocalTime().ToString("g")));

        // Metadatos de dominio ordenados por categoría
        foreach (var kvp in file.Metadata.OrderBy(m => m.Key, StringComparer.OrdinalIgnoreCase))
        {
            string key = kvp.Key;
            string value = kvp.Value?.ToString() ?? string.Empty;
            string category = "General";

            if (key.StartsWith("Exif:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Img:", StringComparison.OrdinalIgnoreCase))
                category = "Fotografía (EXIF)";
            else if (key.StartsWith("Audio:", StringComparison.OrdinalIgnoreCase))
                category = "Música (ID3)";
            else if (key.StartsWith("Video:", StringComparison.OrdinalIgnoreCase))
                category = "Vídeo";
            else if (key.StartsWith("Doc:", StringComparison.OrdinalIgnoreCase))
                category = "Documentos";
            else if (key.StartsWith("Hash:", StringComparison.OrdinalIgnoreCase))
                category = "Integridad (Hashes)";
            else if (key.StartsWith("Series:", StringComparison.OrdinalIgnoreCase))
                category = "Series";

            SelectedFileMetadata.Add(new VirtualMetadataItem(category, key, value));
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
