using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.Sdk.Renaming;
using Microsoft.Win32;

namespace FileFlow.App.ViewModels;

public sealed record PreviewRowItem(string OriginalName, string ResultName, bool IsModified, string StatusMessage);

public sealed record TagPickerItem(string Category, string Tag, string Description);

public partial class AdvancedRenamerEditorViewModel : ObservableObject
{
    private readonly RenamerLivePreviewService _previewService = new();
    private readonly NodeViewModel _nodeViewModel;

    [ObservableProperty]
    private string _pipelineName = "Pipeline Predeterminado";

    [ObservableProperty]
    private string _collisionStrategy = "AutoIncrement";

    [ObservableProperty]
    private RenameMethodStep? _selectedStep;

    [ObservableProperty]
    private RenamerPreset? _selectedPreset;

    [ObservableProperty]
    private string _previewSourceDescription = "(Muestras sintéticas predefinidas)";

    public ObservableCollection<RenameMethodStep> Steps { get; } = [];
    public ObservableCollection<RenamerPreset> AvailablePresets { get; } = [];
    public ObservableCollection<PreviewRowItem> PreviewItems { get; } = [];
    public ObservableCollection<TagPickerItem> AvailableTags { get; } = [];
    public ObservableCollection<string> AvailableCategories { get; } = [];

    public IReadOnlyList<string> CollisionStrategies { get; } = ["AutoIncrement", "Overwrite", "Skip", "Fail"];
    public IReadOnlyList<RenameMethodType> MethodTypes { get; } = Enum.GetValues<RenameMethodType>();
    public IReadOnlyList<ApplyToTarget> ApplyToTargets { get; } = Enum.GetValues<ApplyToTarget>();
    public IReadOnlyList<CaseTransformType> CaseTypes { get; } = Enum.GetValues<CaseTransformType>();
    public IReadOnlyList<CharacterPosition> Positions { get; } = Enum.GetValues<CharacterPosition>();
    public IReadOnlyList<NumberingResetOn> ResetConditions { get; } = Enum.GetValues<NumberingResetOn>();
    public IReadOnlyList<UnicodeNormalizationMode> NormalizationModes { get; } = Enum.GetValues<UnicodeNormalizationMode>();
    public IReadOnlyList<NumberPaddingTarget> NumberPaddingTargets { get; } = Enum.GetValues<NumberPaddingTarget>();

    public AdvancedRenamerEditorViewModel(NodeViewModel nodeViewModel)
    {
        _nodeViewModel = nodeViewModel;
        LoadFromNode();
        LoadPresets();
        LoadAvailableTags();
        GenerateLivePreview();
    }

    private void LoadFromNode()
    {
        if (_nodeViewModel.NodeInstance.Parameters.TryGetValue("PipelineName", out var pnVal) && pnVal != null)
        {
            PipelineName = pnVal.ToString()!;
        }

        if (_nodeViewModel.NodeInstance.Parameters.TryGetValue("CollisionStrategy", out var colVal) && colVal != null)
        {
            CollisionStrategy = colVal.ToString()!;
        }

        string stepsJson = string.Empty;
        if (_nodeViewModel.NodeInstance.Parameters.TryGetValue("MethodSteps", out var msVal) && msVal != null)
        {
            stepsJson = msVal.ToString()!;
        }

        var loadedSteps = RenamerPresetService.DeserializeSteps(stepsJson);

        if (loadedSteps.Count == 0)
        {
            // Migrar parámetros clásicos si existen
            string pattern = _nodeViewModel.NodeInstance.Parameters.TryGetValue("Pattern", out var pVal) && pVal != null ? pVal.ToString()! : "{ParentDir}_{CreationDate:yyyyMMdd}_{FileNameNoExt}.{Ext}";
            string caseTr = _nodeViewModel.NodeInstance.Parameters.TryGetValue("CaseTransformation", out var ctVal) && ctVal != null ? ctVal.ToString()! : "None";

            loadedSteps.Add(new RenameMethodStep
            {
                MethodType = RenameMethodType.NewName,
                ApplyTo = ApplyToTarget.FullName,
                Pattern = pattern,
                Name = "Plantilla Inicial"
            });

            if (Enum.TryParse<CaseTransformType>(caseTr, true, out var ct))
            {
                loadedSteps.Add(new RenameMethodStep
                {
                    MethodType = RenameMethodType.CaseConversion,
                    ApplyTo = ApplyToTarget.FullName,
                    CaseType = ct,
                    Name = "Transformación de Mayúsculas"
                });
            }
        }

        Steps.Clear();
        foreach (var s in loadedSteps)
        {
            Steps.Add(s);
        }

        SelectedStep = Steps.FirstOrDefault();
    }

    private void LoadPresets()
    {
        AvailablePresets.Clear();
        foreach (var preset in RenamerPresetService.GetBuiltinPresets())
        {
            AvailablePresets.Add(preset);
        }
    }

    private void LoadAvailableTags()
    {
        AvailableTags.Clear();
        var tags = RenamerTagCatalogService.GetAvailableTags(_nodeViewModel);
        foreach (var t in tags)
        {
            AvailableTags.Add(t);
        }

        AvailableCategories.Clear();
        foreach (var cat in AvailableTags.Select(t => t.Category).Distinct())
        {
            AvailableCategories.Add(cat);
        }
    }

    [RelayCommand]
    public void AddStep(RenameMethodType methodType)
    {
        var newStep = new RenameMethodStep
        {
            MethodType = methodType,
            Name = methodType switch
            {
                RenameMethodType.NewName => "Nueva Plantilla",
                RenameMethodType.SearchReplace => "Buscar y Reemplazar",
                RenameMethodType.Insert => "Insertar Texto",
                RenameMethodType.Remove => "Eliminar Caracteres",
                RenameMethodType.CaseConversion => "Modificar Mayúsculas",
                RenameMethodType.Numbering => "Numeración Incremental",
                RenameMethodType.ReplaceList => "Tabla de Sustituciones",
                RenameMethodType.TrimClean => "Limpieza y Normalización",
                RenameMethodType.NormalizeNumbers => "Normalizar Números (01, 02...)",
                _ => "Paso de Renombrado"
            },
            Pattern = methodType == RenameMethodType.NewName ? "<FileNameNoExt>_<Inc Nr:001>" : string.Empty
        };

        if (methodType == RenameMethodType.NormalizeNumbers)
        {
            newStep.NumberPaddingDigits = 2;
            newStep.NumberTarget = NumberPaddingTarget.AllNumbers;
        }

        if (methodType == RenameMethodType.ReplaceList)
        {
            newStep.ReplaceList.Add(new ReplaceListEntry { Find = "borrador", ReplaceWith = "FINAL" });
        }

        Steps.Add(newStep);
        SelectedStep = newStep;
        GenerateLivePreview();
    }

    [RelayCommand]
    public void RemoveStep(RenameMethodStep? step)
    {
        if (step == null) step = SelectedStep;
        if (step == null) return;

        int index = Steps.IndexOf(step);
        Steps.Remove(step);

        if (Steps.Count > 0)
        {
            SelectedStep = Steps[Math.Min(index, Steps.Count - 1)];
        }
        else
        {
            SelectedStep = null;
        }

        GenerateLivePreview();
    }

    [RelayCommand]
    public void MoveStepUp(RenameMethodStep? step)
    {
        if (step == null) step = SelectedStep;
        if (step == null) return;

        int index = Steps.IndexOf(step);
        if (index > 0)
        {
            Steps.Move(index, index - 1);
            SelectedStep = step;
            GenerateLivePreview();
        }
    }

    [RelayCommand]
    public void MoveStepDown(RenameMethodStep? step)
    {
        if (step == null) step = SelectedStep;
        if (step == null) return;

        int index = Steps.IndexOf(step);
        if (index < Steps.Count - 1 && index >= 0)
        {
            Steps.Move(index, index + 1);
            SelectedStep = step;
            GenerateLivePreview();
        }
    }

    [RelayCommand]
    public void ApplyPreset(RenamerPreset? preset)
    {
        if (preset == null) return;

        var result = MessageBox.Show(
            $"¿Deseas reemplazar los pasos actuales con el preset '{preset.Name}'?",
            "Cargar Preset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        PipelineName = preset.Name;
        Steps.Clear();
        foreach (var s in preset.Steps)
        {
            Steps.Add(s);
        }

        SelectedStep = Steps.FirstOrDefault();
        GenerateLivePreview();
    }

    [RelayCommand]
    public void SaveCurrentAsPreset()
    {
        var saveDialog = new SaveFileDialog
        {
            Filter = "Ajustes de Renombrado (*.ffren)|*.ffren|Archivos JSON (*.json)|*.json",
            DefaultExt = ".ffren",
            Title = "Guardar Preset de Renombrado Avanzado"
        };

        if (saveDialog.ShowDialog() == true)
        {
            var preset = new RenamerPreset
            {
                Name = Path.GetFileNameWithoutExtension(saveDialog.FileName),
                Description = "Preset personalizado creado por el usuario",
                Category = "Personalizado",
                Steps = Steps.ToList()
            };

            string json = RenamerPresetService.SerializePreset(preset);
            File.WriteAllText(saveDialog.FileName, json);
            MessageBox.Show("Preset guardado exitosamente.", "FileFlow Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    public void LoadPresetFromFile()
    {
        var openDialog = new OpenFileDialog
        {
            Filter = "Ajustes de Renombrado (*.ffren;*.json)|*.ffren;*.json",
            Title = "Importar Preset de Renombrado"
        };

        if (openDialog.ShowDialog() == true)
        {
            try
            {
                string json = File.ReadAllText(openDialog.FileName);
                var preset = RenamerPresetService.DeserializePreset(json);
                if (preset != null && preset.Steps.Count > 0)
                {
                    PipelineName = preset.Name;
                    Steps.Clear();
                    foreach (var s in preset.Steps) Steps.Add(s);
                    SelectedStep = Steps.FirstOrDefault();
                    GenerateLivePreview();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar preset: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void InsertTagIntoSelectedStep(string tag)
    {
        if (SelectedStep == null) return;
        SelectedStep.Pattern = (SelectedStep.Pattern ?? string.Empty) + tag;
        OnPropertyChanged(nameof(SelectedStep));
        GenerateLivePreview();
    }

    [RelayCommand]
    public void AddReplaceListEntry()
    {
        if (SelectedStep != null && SelectedStep.MethodType == RenameMethodType.ReplaceList)
        {
            SelectedStep.ReplaceList.Add(new ReplaceListEntry { Find = "buscar", ReplaceWith = "reemplazar" });
            GenerateLivePreview();
        }
    }

    [RelayCommand]
    public void RemoveReplaceListEntry(ReplaceListEntry entry)
    {
        if (SelectedStep != null && SelectedStep.MethodType == RenameMethodType.ReplaceList)
        {
            SelectedStep.ReplaceList.Remove(entry);
            GenerateLivePreview();
        }
    }

    public void GenerateLivePreview()
    {
        PreviewItems.Clear();
        var previewList = _previewService.GeneratePreview(_nodeViewModel, Steps.ToList(), out string srcDesc);
        PreviewSourceDescription = srcDesc;

        foreach (var p in previewList)
        {
            PreviewItems.Add(p);
        }
    }

    [RelayCommand]
    public void SaveAndClose(Window window)
    {
        if (string.IsNullOrWhiteSpace(PipelineName))
        {
            PipelineName = "Pipeline Predeterminado";
        }

        string serializedSteps = RenamerPresetService.SerializeSteps(Steps.ToList());

        _nodeViewModel.NodeInstance.Parameters["PipelineName"] = PipelineName;
        _nodeViewModel.NodeInstance.Parameters["CollisionStrategy"] = CollisionStrategy;
        _nodeViewModel.NodeInstance.Parameters["MethodSteps"] = serializedSteps;

        // Actualizar parámetros para reflejarse en el Canvas e Inspector
        var pnParam = _nodeViewModel.Parameters.FirstOrDefault(p => p.Key.Equals("PipelineName", StringComparison.OrdinalIgnoreCase));
        if (pnParam != null)
        {
            pnParam.Value = PipelineName;
        }
        else
        {
            _nodeViewModel.Parameters.Insert(0, new NodeParameterViewModel("PipelineName", PipelineName, nodeOwner: _nodeViewModel));
        }

        var csParam = _nodeViewModel.Parameters.FirstOrDefault(p => p.Key.Equals("CollisionStrategy", StringComparison.OrdinalIgnoreCase));
        if (csParam != null)
        {
            csParam.Value = CollisionStrategy;
        }
        else
        {
            _nodeViewModel.Parameters.Add(new NodeParameterViewModel("CollisionStrategy", CollisionStrategy, nodeOwner: _nodeViewModel));
        }

        window.DialogResult = true;
        window.Close();
    }
}
