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
    private int _selectedTabIndex = 0; // 0 = Tabla, 1 = DSL Árbol, 2 = JSON

    [ObservableProperty]
    private string _dslText = string.Empty;

    [ObservableProperty]
    private string _jsonText = string.Empty;

    [ObservableProperty]
    private SyntheticFileDefinition? _selectedItem;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<SyntheticDataSet> FilteredDataSets { get; } = [];
    public ObservableCollection<SyntheticFileDefinition> EditableItems { get; } = [];

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

        SyncViewsFromItems();
        NotifyMetrics();
        StatusMessage = LocalizationManager.Instance.GetFormattedString("Msg_DataSetLoaded", "Dataset '{0}' cargado.", value.Name);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 1) // Modo Árbol DSL
        {
            DslText = SyntheticTreeDslParser.Serialize(EditableItems);
        }
        else if (value == 2) // Modo JSON
        {
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

        // Si estaba en la pestaña DSL o JSON, aplicar cambios pendientes antes de guardar
        if (SelectedTabIndex == 1)
        {
            ApplyDslToItems();
        }
        else if (SelectedTabIndex == 2)
        {
            ApplyJsonToItems();
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
        var newFile = new SyntheticFileDefinition("NuevaCarpeta/archivo_prueba.dat", 1048576, false);
        EditableItems.Add(newFile);
        SelectedItem = newFile;
        NotifyMetrics();
    }

    [RelayCommand]
    private void AddFolder()
    {
        var newFolder = new SyntheticFileDefinition("NuevaCarpeta/", 0, true);
        EditableItems.Add(newFolder);
        SelectedItem = newFolder;
        NotifyMetrics();
    }

    [RelayCommand]
    private void RemoveItem()
    {
        if (SelectedItem != null)
        {
            EditableItems.Remove(SelectedItem);
            SelectedItem = EditableItems.FirstOrDefault();
            NotifyMetrics();
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
