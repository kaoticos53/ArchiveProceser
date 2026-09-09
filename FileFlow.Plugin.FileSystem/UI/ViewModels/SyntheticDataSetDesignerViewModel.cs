using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Plugin.FileSystem.Services;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.SyntheticData;
using Microsoft.Win32;

namespace FileFlow.Plugin.FileSystem.UI.ViewModels;

public partial class SyntheticDataSetDesignerViewModel : ObservableObject
{
    private readonly ISyntheticDataSetStorageService _storageService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private SyntheticDataSet? _selectedDataSet;

    [ObservableProperty]
    private string _dataSetName = string.Empty;

    [ObservableProperty]
    private string _dataSetCategory = "General";

    [ObservableProperty]
    private string _dataSetDescription = string.Empty;

    [ObservableProperty]
    private bool _isBuiltInSelected;

    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0 = Árbol Jerárquico / Grilla, 1 = DSL Árbol, 2 = JSON

    [ObservableProperty]
    private string _dslText = string.Empty;

    [ObservableProperty]
    private string _jsonText = string.Empty;

    [ObservableProperty]
    private SyntheticFileDefinition? _selectedItem;

    [ObservableProperty]
    private SyntheticTreeNodeItem? _selectedTreeNode;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<SyntheticDataSet> FilteredDataSets { get; } = [];
    public ObservableCollection<SyntheticFileDefinition> EditableItems { get; } = [];
    public ObservableCollection<SyntheticTreeNodeItem> RootTreeNodes { get; } = [];

    public IReadOnlyList<string> AvailableCategories =>
    [
        "General",
        "Películas",
        "Series",
        "Cómics y Manga",
        "Música",
        "Fotos",
        "Documentos",
        "Personalizada"
    ];

    public int TotalFiles => EditableItems.Count(i => !i.IsDirectory);
    public int TotalDirectories => EditableItems.Count(i => i.IsDirectory);
    public string TotalSizeFormatted => SyntheticTreeDslParser.FormatSize(EditableItems.Where(i => !i.IsDirectory).Sum(i => i.FileSizeBytes));

    public SyntheticDataSetDesignerViewModel(ISyntheticDataSetStorageService? storageService = null, IDialogService? dialogService = null)
    {
        _storageService = storageService ?? SyntheticDataSetStorageService.Instance;
        _dialogService = dialogService ?? NullDialogService.Instance;
        _storageService.DataSetsChanged += OnDataSetsChanged;
        RefreshDataSetsList();

        if (FilteredDataSets.Count > 0)
        {
            SelectedDataSet = FilteredDataSets[0];
        }
    }

    private void OnDataSetsChanged(object? sender, EventArgs e)
    {
        RefreshDataSetsList();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshDataSetsList();
    }

    partial void OnSelectedDataSetChanged(SyntheticDataSet? value)
    {
        if (value == null)
        {
            DataSetName = string.Empty;
            DataSetCategory = "General";
            DataSetDescription = string.Empty;
            IsBuiltInSelected = false;
            EditableItems.Clear();
            RootTreeNodes.Clear();
            SelectedTreeNode = null;
            DslText = string.Empty;
            JsonText = string.Empty;
            NotifyMetrics();
            return;
        }

        DataSetName = value.Name;
        DataSetCategory = value.Category;
        DataSetDescription = value.Description;
        IsBuiltInSelected = value.IsBuiltIn;

        EditableItems.Clear();
        foreach (var item in value.Items)
        {
            EditableItems.Add(item.Clone());
        }

        BuildTreeFromItems();
        SyncViewsFromItems();
        NotifyMetrics();
        StatusMessage = LocalizationManager.Instance.GetFormattedString("Msg_DataSetLoaded", "Dataset '{0}' cargado.", value.Name);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 0) // Árbol / Explorador
        {
            BuildTreeFromItems();
        }
        else if (value == 1) // Modo Árbol DSL
        {
            SyncItemsFromTree();
            DslText = SyntheticTreeDslParser.Serialize(EditableItems);
        }
        else if (value == 2) // Modo JSON
        {
            SyncItemsFromTree();
            RefreshJsonText();
        }
    }

    public void RefreshDataSetsList()
    {
        string? currentSelectedId = SelectedDataSet?.Id;
        var all = _storageService.GetAllDataSets();

        FilteredDataSets.Clear();
        foreach (var ds in all)
        {
            if (string.IsNullOrWhiteSpace(SearchText) ||
                ds.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                ds.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                FilteredDataSets.Add(ds);
            }
        }

        if (currentSelectedId != null)
        {
            SelectedDataSet = FilteredDataSets.FirstOrDefault(d => d.Id == currentSelectedId);
        }
    }

    public void BuildTreeFromItems()
    {
        RootTreeNodes.Clear();
        var folderLookup = new Dictionary<string, SyntheticTreeNodeItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in EditableItems)
        {
            string rawPath = item.RelativePath.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(rawPath)) continue;

            var segments = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) continue;

            SyntheticTreeNodeItem? currentParent = null;
            string currentPathAccumulator = "";

            // Create intermediate folder nodes
            for (int i = 0; i < segments.Length - 1; i++)
            {
                string segment = segments[i];
                currentPathAccumulator = string.IsNullOrEmpty(currentPathAccumulator) ? segment : $"{currentPathAccumulator}/{segment}";

                if (!folderLookup.TryGetValue(currentPathAccumulator, out var folderNode))
                {
                    folderNode = new SyntheticTreeNodeItem(segment, currentPathAccumulator, isDirectory: true, sizeBytes: 0, currentParent);
                    folderLookup[currentPathAccumulator] = folderNode;

                    if (currentParent == null)
                    {
                        RootTreeNodes.Add(folderNode);
                    }
                    else
                    {
                        currentParent.Children.Add(folderNode);
                    }
                }

                currentParent = folderNode;
            }

            // Create or register the leaf item
            string leafSegment = segments[^1];
            string leafPath = string.IsNullOrEmpty(currentPathAccumulator) ? leafSegment : $"{currentPathAccumulator}/{leafSegment}";

            if (item.IsDirectory)
            {
                if (!folderLookup.TryGetValue(leafPath, out var leafFolderNode))
                {
                    leafFolderNode = new SyntheticTreeNodeItem(item, currentParent);
                    folderLookup[leafPath] = leafFolderNode;

                    if (currentParent == null)
                    {
                        RootTreeNodes.Add(leafFolderNode);
                    }
                    else
                    {
                        currentParent.Children.Add(leafFolderNode);
                    }
                }
            }
            else
            {
                var fileNode = new SyntheticTreeNodeItem(item, currentParent);
                if (fileNode.IsArchive)
                {
                    EnsureDefaultArchiveEntries(fileNode);
                    PopulateArchiveChildren(fileNode);
                }

                if (currentParent == null)
                {
                    RootTreeNodes.Add(fileNode);
                }
                else
                {
                    currentParent.Children.Add(fileNode);
                }
            }
        }

        SortTreeRecursively(RootTreeNodes);
        SelectedTreeNode = RootTreeNodes.FirstOrDefault();
    }

    private static void EnsureDefaultArchiveEntries(SyntheticTreeNodeItem fileNode)
    {
        if (!fileNode.IsArchive || fileNode.SimulatedArchiveEntries.Count > 0) return;

        string ext = Path.GetExtension(fileNode.Name).ToLowerInvariant();
        if (ext is ".cbr" or ".cbz")
        {
            int pageCount = 24;
            if (fileNode.Metadata.TryGetValue("Doc:PageCount", out var pcObj) && pcObj != null && int.TryParse(pcObj.ToString(), out int parsedPc) && parsedPc > 0)
            {
                pageCount = Math.Min(parsedPc, 30);
            }

            long pageAvgBytes = Math.Max(50000, fileNode.FileSizeBytes / Math.Max(1, pageCount));
            for (int i = 1; i <= Math.Min(pageCount, 12); i++)
            {
                fileNode.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition($"page_{i:D3}.jpg", pageAvgBytes));
            }
            fileNode.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("ComicInfo.xml", 2048));
        }
        else if (ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz")
        {
            long partSize = Math.Max(1024, fileNode.FileSizeBytes / 3);
            fileNode.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("documento_interno.pdf", partSize));
            fileNode.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("datos_extra.csv", Math.Max(512, partSize / 4)));
            fileNode.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("leeme.txt", 1024));
        }
    }

    private static void PopulateArchiveChildren(SyntheticTreeNodeItem archiveNode)
    {
        archiveNode.Children.Clear();
        foreach (var entry in archiveNode.SimulatedArchiveEntries)
        {
            var entryNode = new SyntheticTreeNodeItem(
                name: Path.GetFileName(entry.InnerPath),
                relativePath: entry.InnerPath,
                isDirectory: entry.IsDirectory,
                sizeBytes: entry.FileSizeBytes,
                parent: archiveNode)
            {
                IsArchiveEntry = true,
                Metadata = new Dictionary<string, object?>(entry.Metadata, StringComparer.OrdinalIgnoreCase)
            };
            archiveNode.Children.Add(entryNode);
        }
    }

    private static void SortTreeRecursively(ObservableCollection<SyntheticTreeNodeItem> nodes)
    {
        var sorted = nodes.OrderByDescending(n => n.IsDirectory && !n.IsArchiveEntry).ThenBy(n => n.Name).ToList();
        nodes.Clear();
        foreach (var node in sorted)
        {
            nodes.Add(node);
            SortTreeRecursively(node.Children);
        }
    }

    public void SyncItemsFromTree()
    {
        EditableItems.Clear();
        void CollectNodes(IEnumerable<SyntheticTreeNodeItem> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsArchiveEntry)
                {
                    // Las entradas internas están contenidas dentro del paquete padre
                    continue;
                }

                if (node.IsDirectory)
                {
                    if (node.Children.Count == 0)
                    {
                        EditableItems.Add(new SyntheticFileDefinition($"{node.RelativePath}/", 0, isDirectory: true));
                    }
                    CollectNodes(node.Children);
                }
                else
                {
                    var fileDef = new SyntheticFileDefinition(node.RelativePath, node.FileSizeBytes, isDirectory: false, node.Metadata);
                    if (node.IsArchive)
                    {
                        var internalNodes = node.Children.Where(c => c.IsArchiveEntry).ToList();
                        if (internalNodes.Count > 0)
                        {
                            node.SimulatedArchiveEntries.Clear();
                            foreach (var internalNode in internalNodes)
                            {
                                var entryDef = new SyntheticArchiveEntryDefinition
                                {
                                    InnerPath = internalNode.Name,
                                    FileSizeBytes = internalNode.FileSizeBytes,
                                    IsDirectory = internalNode.IsDirectory,
                                    Metadata = new Dictionary<string, object?>(internalNode.Metadata, StringComparer.OrdinalIgnoreCase)
                                };
                                node.SimulatedArchiveEntries.Add(entryDef);
                                fileDef.SimulatedArchiveEntries.Add(entryDef);
                            }
                        }
                        else
                        {
                            foreach (var entry in node.SimulatedArchiveEntries)
                            {
                                fileDef.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition
                                {
                                    InnerPath = entry.InnerPath,
                                    FileSizeBytes = entry.FileSizeBytes,
                                    IsDirectory = entry.IsDirectory,
                                    Metadata = new Dictionary<string, object?>(entry.Metadata, StringComparer.OrdinalIgnoreCase)
                                });
                            }
                        }
                    }

                    EditableItems.Add(fileDef);
                }
            }
        }
        CollectNodes(RootTreeNodes);
        SyncViewsFromItems();
        NotifyMetrics();
    }

    [RelayCommand]
    private void NewDataSet()
    {
        var newDs = new SyntheticDataSet("Nuevo Conjunto de Pruebas", "Personalizada", "Dataset creado a medida por el usuario.")
        {
            Items =
            [
                new SyntheticFileDefinition("Documentos/notas.txt", 2048, false),
                new SyntheticFileDefinition("Imágenes/foto_01.jpg", 3145728, false, new Dictionary<string, object?> { ["Exif:CameraModel"] = "Sony A7IV" }),
                new SyntheticFileDefinition("Archivos/paquete.zip", 1048576, false)
                {
                    SimulatedArchiveEntries =
                    [
                        new SyntheticArchiveEntryDefinition("doc.pdf", 512000),
                        new SyntheticArchiveEntryDefinition("data.csv", 12000)
                    ]
                }
            ]
        };

        _storageService.SaveDataSet(newDs);
        RefreshDataSetsList();
        SelectedDataSet = FilteredDataSets.FirstOrDefault(d => d.Id == newDs.Id);
        StatusMessage = LocalizationManager.Instance.GetString("Msg_NewDataSetCreated", "Nuevo dataset creado con éxito.");
    }

    [RelayCommand]
    private void DuplicateDataSet()
    {
        if (SelectedDataSet == null) return;

        var clone = _storageService.CloneDataSet(SelectedDataSet.Id, $"{SelectedDataSet.Name} (Copia)");
        RefreshDataSetsList();
        SelectedDataSet = FilteredDataSets.FirstOrDefault(d => d.Id == clone.Id);
        StatusMessage = LocalizationManager.Instance.GetFormattedString("Msg_DataSetDuplicated", "Copia creada: '{0}'.", clone.Name);
    }

    [RelayCommand]
    private void DeleteDataSet()
    {
        if (SelectedDataSet == null || SelectedDataSet.IsBuiltIn) return;

        bool confirm = _dialogService.ShowConfirmation(
            LocalizationManager.Instance.GetFormattedString("Msg_ConfirmDeleteDataSet", "¿Deseas eliminar de forma permanente el dataset '{0}'?", SelectedDataSet.Name),
            LocalizationManager.Instance.GetString("Title_ConfirmDelete", "Confirmar Eliminación"));

        if (confirm)
        {
            string deletedName = SelectedDataSet.Name;
            _storageService.DeleteDataSet(SelectedDataSet.Id);
            RefreshDataSetsList();
            SelectedDataSet = FilteredDataSets.FirstOrDefault();
            StatusMessage = LocalizationManager.Instance.GetFormattedString("Msg_DataSetDeleted", "Dataset '{0}' eliminado.", deletedName);
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (SelectedDataSet == null) return;

        // Si estaba en la pestaña del árbol, sincronizar items
        if (SelectedTabIndex == 0)
        {
            SyncItemsFromTree();
        }
        else if (SelectedTabIndex == 1)
        {
            ApplyDslToItems();
        }
        else if (SelectedTabIndex == 2)
        {
            ApplyJsonToItems();
        }

        if (SelectedDataSet.IsBuiltIn)
        {
            // Si intenta guardar sobre un Built-in, crear una copia editable para no romper los oficiales
            var clone = _storageService.CloneDataSet(SelectedDataSet.Id, $"{DataSetName} (Personalizado)");
            clone.Description = DataSetDescription;
            clone.Category = DataSetCategory;
            clone.Items = EditableItems.Select(i => i.Clone()).ToList();
            _storageService.SaveDataSet(clone);

            RefreshDataSetsList();
            SelectedDataSet = FilteredDataSets.FirstOrDefault(d => d.Id == clone.Id);
            StatusMessage = LocalizationManager.Instance.GetFormattedString("Msg_BuiltInClonedOnSave", "Los datasets oficiales son de solo lectura. Se ha guardado una copia personalizada: '{0}'.", clone.Name);
            return;
        }

        SelectedDataSet.Name = DataSetName;
        SelectedDataSet.Category = DataSetCategory;
        SelectedDataSet.Description = DataSetDescription;
        SelectedDataSet.Items = EditableItems.Select(i => i.Clone()).ToList();

        _storageService.SaveDataSet(SelectedDataSet);
        RefreshDataSetsList();
        NotifyMetrics();
        StatusMessage = LocalizationManager.Instance.GetFormattedString("Msg_DataSetSavedSuccess", "Dataset '{0}' guardado correctamente.", DataSetName);
    }

    [RelayCommand]
    private void AddFile()
    {
        AddFileToTree();
    }

    [RelayCommand]
    private void AddFolder()
    {
        AddFolderToTree();
    }

    [RelayCommand]
    private void RemoveItem()
    {
        RemoveTreeNode();
    }

    [RelayCommand]
    public void AddFileToTree()
    {
        if (SelectedTreeNode != null && (SelectedTreeNode.IsArchive || SelectedTreeNode.IsArchiveEntry))
        {
            var targetArchive = SelectedTreeNode.IsArchive ? SelectedTreeNode : SelectedTreeNode.Parent;
            if (targetArchive != null)
            {
                string innerFileName = $"archivo_interno_{targetArchive.Children.Count + 1}.dat";
                var newEntryNode = new SyntheticTreeNodeItem(innerFileName, innerFileName, isDirectory: false, sizeBytes: 524288, targetArchive)
                {
                    IsArchiveEntry = true
                };
                targetArchive.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition(innerFileName, 524288));
                targetArchive.Children.Add(newEntryNode);
                targetArchive.IsExpanded = true;
                targetArchive.NotifyParentMetricsChanged();
                SelectedTreeNode = newEntryNode;
                SyncItemsFromTree();
                return;
            }
        }

        SyntheticTreeNodeItem? targetParent = SelectedTreeNode?.IsDirectory == true
            ? SelectedTreeNode
            : SelectedTreeNode?.Parent;

        string fileName = "nuevo_archivo.dat";
        string relativePath = targetParent != null && !string.IsNullOrWhiteSpace(targetParent.RelativePath)
            ? $"{targetParent.RelativePath.TrimEnd('/')}/{fileName}"
            : fileName;

        var newFile = new SyntheticTreeNodeItem(fileName, relativePath, isDirectory: false, sizeBytes: 1048576, targetParent);

        if (targetParent != null)
        {
            targetParent.Children.Add(newFile);
            targetParent.IsExpanded = true;
            targetParent.NotifyParentMetricsChanged();
        }
        else
        {
            RootTreeNodes.Add(newFile);
        }

        SelectedTreeNode = newFile;
        SyncItemsFromTree();
    }

    [RelayCommand]
    public void AddFolderToTree()
    {
        SyntheticTreeNodeItem? targetParent = SelectedTreeNode?.IsDirectory == true
            ? SelectedTreeNode
            : SelectedTreeNode?.Parent;

        string folderName = "Nueva_Carpeta";
        string relativePath = targetParent != null && !string.IsNullOrWhiteSpace(targetParent.RelativePath)
            ? $"{targetParent.RelativePath.TrimEnd('/')}/{folderName}"
            : folderName;

        var newFolder = new SyntheticTreeNodeItem(folderName, relativePath, isDirectory: true, sizeBytes: 0, targetParent);

        if (targetParent != null)
        {
            targetParent.Children.Add(newFolder);
            targetParent.IsExpanded = true;
            targetParent.NotifyParentMetricsChanged();
        }
        else
        {
            RootTreeNodes.Add(newFolder);
        }

        SelectedTreeNode = newFolder;
        SyncItemsFromTree();
    }

    [RelayCommand]
    public void AddArchiveToTree()
    {
        SyntheticTreeNodeItem? targetParent = SelectedTreeNode?.IsDirectory == true
            ? SelectedTreeNode
            : SelectedTreeNode?.Parent;

        string archiveName = "paquete_simulado.zip";
        string relativePath = targetParent != null && !string.IsNullOrWhiteSpace(targetParent.RelativePath)
            ? $"{targetParent.RelativePath.TrimEnd('/')}/{archiveName}"
            : archiveName;

        var newArchive = new SyntheticTreeNodeItem(archiveName, relativePath, isDirectory: false, sizeBytes: 2097152, targetParent)
        {
            IsArchive = true
        };

        newArchive.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("documento_interno.pdf", 524288));
        newArchive.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("datos_extra.csv", 32768));
        newArchive.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("leeme.txt", 1024));

        PopulateArchiveChildren(newArchive);

        if (targetParent != null)
        {
            targetParent.Children.Add(newArchive);
            targetParent.IsExpanded = true;
            targetParent.NotifyParentMetricsChanged();
        }
        else
        {
            RootTreeNodes.Add(newArchive);
        }

        SelectedTreeNode = newArchive;
        SyncItemsFromTree();
    }

    [RelayCommand]
    public void AddArchiveEntry()
    {
        var targetArchive = SelectedTreeNode?.IsArchive == true
            ? SelectedTreeNode
            : (SelectedTreeNode?.IsArchiveEntry == true ? SelectedTreeNode.Parent : null);

        if (targetArchive == null) return;

        string innerPath = $"nuevo_archivo_{targetArchive.Children.Count + 1}.dat";
        var newEntry = new SyntheticArchiveEntryDefinition(innerPath, 524288);
        targetArchive.SimulatedArchiveEntries.Add(newEntry);

        var entryNode = new SyntheticTreeNodeItem(
            name: innerPath,
            relativePath: innerPath,
            isDirectory: false,
            sizeBytes: 524288,
            parent: targetArchive)
        {
            IsArchiveEntry = true
        };
        targetArchive.Children.Add(entryNode);
        targetArchive.IsExpanded = true;
        targetArchive.NotifyParentMetricsChanged();
        SelectedTreeNode = entryNode;
        SyncItemsFromTree();
    }

    [RelayCommand]
    public void RemoveArchiveEntry(SyntheticArchiveEntryDefinition? entry)
    {
        if (entry == null || SelectedTreeNode == null) return;
        var targetArchive = SelectedTreeNode.IsArchive ? SelectedTreeNode : SelectedTreeNode.Parent;
        if (targetArchive == null) return;

        targetArchive.SimulatedArchiveEntries.Remove(entry);
        var childNode = targetArchive.Children.FirstOrDefault(c => c.Name == entry.InnerPath);
        if (childNode != null)
        {
            targetArchive.Children.Remove(childNode);
        }
        targetArchive.NotifyParentMetricsChanged();
        SyncItemsFromTree();
    }

    [RelayCommand]
    public void RemoveTreeNode()
    {
        if (SelectedTreeNode == null) return;

        var parent = SelectedTreeNode.Parent;
        if (parent != null)
        {
            parent.Children.Remove(SelectedTreeNode);
            if (SelectedTreeNode.IsArchiveEntry)
            {
                var matchingEntry = parent.SimulatedArchiveEntries.FirstOrDefault(e => e.InnerPath == SelectedTreeNode.Name);
                if (matchingEntry != null)
                {
                    parent.SimulatedArchiveEntries.Remove(matchingEntry);
                }
            }
            parent.NotifyParentMetricsChanged();
            SelectedTreeNode = parent;
        }
        else
        {
            RootTreeNodes.Remove(SelectedTreeNode);
            SelectedTreeNode = RootTreeNodes.FirstOrDefault();
        }

        SyncItemsFromTree();
    }

    [RelayCommand]
    public void ExpandAllTree()
    {
        foreach (var node in RootTreeNodes)
        {
            node.SetExpandedRecursively(true);
        }
    }

    [RelayCommand]
    public void CollapseAllTree()
    {
        foreach (var node in RootTreeNodes)
        {
            node.SetExpandedRecursively(false);
        }
    }

    [RelayCommand]
    private void ApplyDslToItems()
    {
        try
        {
            var parsed = SyntheticTreeDslParser.Parse(DslText);
            EditableItems.Clear();
            foreach (var item in parsed)
            {
                EditableItems.Add(item);
            }
            BuildTreeFromItems();
            NotifyMetrics();
            RefreshJsonText();
            StatusMessage = LocalizationManager.Instance.GetFormattedString("Msg_DslApplied", "Estructura DSL aplicada: {0} elementos creados.", parsed.Count);
        }
        catch (Exception ex)
        {
            _dialogService.ShowWarning(ex.Message, "Error en DSL");
        }
    }

    [RelayCommand]
    private void ApplyJsonToItems()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(JsonText)) return;
            var ds = JsonSerializer.Deserialize<SyntheticDataSet>(JsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (ds != null)
            {
                DataSetName = ds.Name;
                DataSetCategory = ds.Category;
                DataSetDescription = ds.Description;
                EditableItems.Clear();
                foreach (var item in ds.Items)
                {
                    EditableItems.Add(item);
                }
                BuildTreeFromItems();
                NotifyMetrics();
                DslText = SyntheticTreeDslParser.Serialize(EditableItems);
                StatusMessage = LocalizationManager.Instance.GetString("Msg_JsonApplied", "JSON aplicado con éxito al dataset.");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowWarning(ex.Message, "Error en JSON");
        }
    }

    [RelayCommand]
    private void Export()
    {
        if (SelectedDataSet == null) return;

        SyncItemsFromTree();

        var dialog = new SaveFileDialog
        {
            Filter = "Archivos JSON (*.json)|*.json|Todos los archivos (*.*)|*.*",
            FileName = $"{SelectedDataSet.Name.Replace(" ", "_")}.json",
            Title = LocalizationManager.Instance.GetString("Title_ExportDataSet", "Exportar Dataset Sintético")
        };

        if (dialog.ShowDialog() == true)
        {
            var target = SelectedDataSet.Clone(DataSetName);
            target.Category = DataSetCategory;
            target.Description = DataSetDescription;
            target.Items = EditableItems.Select(i => i.Clone()).ToList();

            string json = _storageService.ExportDataSetToJson(target);
            File.WriteAllText(dialog.FileName, json);
            StatusMessage = LocalizationManager.Instance.GetFormattedString("Msg_DataSetExported", "Dataset exportado a '{0}'.", Path.GetFileName(dialog.FileName));
        }
    }

    [RelayCommand]
    private void Import()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Archivos JSON (*.json)|*.json|Todos los archivos (*.*)|*.*",
            Title = LocalizationManager.Instance.GetString("Title_ImportDataSet", "Importar Dataset Sintético")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string json = File.ReadAllText(dialog.FileName);
                var imported = _storageService.ImportDataSetFromJson(json, autoSave: true);
                RefreshDataSetsList();
                SelectedDataSet = FilteredDataSets.FirstOrDefault(d => d.Id == imported.Id);
                StatusMessage = LocalizationManager.Instance.GetFormattedString("Msg_DataSetImported", "Dataset '{0}' importado y guardado.", imported.Name);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "Error al importar");
            }
        }
    }

    private void SyncViewsFromItems()
    {
        DslText = SyntheticTreeDslParser.Serialize(EditableItems);
        RefreshJsonText();
    }

    private void RefreshJsonText()
    {
        var tempDs = new SyntheticDataSet(DataSetName, DataSetCategory, DataSetDescription)
        {
            Id = SelectedDataSet?.Id ?? Guid.NewGuid().ToString("N"),
            Items = EditableItems.ToList()
        };
        JsonText = JsonSerializer.Serialize(tempDs, new JsonSerializerOptions { WriteIndented = true });
    }

    public void NotifyMetrics()
    {
        OnPropertyChanged(nameof(TotalFiles));
        OnPropertyChanged(nameof(TotalDirectories));
        OnPropertyChanged(nameof(TotalSizeFormatted));
    }
}

