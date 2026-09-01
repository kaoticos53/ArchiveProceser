using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Plugin.FileSystem.UI.Models;
using FileFlow.Plugin.FileSystem.UI.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;
using Microsoft.Win32;

namespace FileFlow.Plugin.FileSystem.UI.ViewModels;

public partial class AdvancedRenamerEditorViewModel : ObservableObject
{
    private readonly RenamerLivePreviewService _previewService = new();
    private readonly IFlowNode _node;

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

    public AdvancedRenamerEditorViewModel(IFlowNode node)
    {
        _node = node;
        LoadFromNode();
        LoadPresets();
        LoadAvailableTags();
        GenerateLivePreview();
    }

    private void LoadFromNode()
    {
        if (_node.Parameters.TryGetValue("PipelineName", out var pnVal) && pnVal != null)
        {
            PipelineName = pnVal.ToString()!;
        }

        if (_node.Parameters.TryGetValue("CollisionStrategy", out var colVal) && colVal != null)
        {
            CollisionStrategy = colVal.ToString()!;
        }

        string stepsJson = string.Empty;
        if (_node.Parameters.TryGetValue("MethodSteps", out var msVal) && msVal != null)
        {
            stepsJson = msVal.ToString()!;
        }

        var loadedSteps = RenamerPresetService.DeserializeSteps(stepsJson);

        if (loadedSteps.Count == 0)
        {
            string pattern = _node.Parameters.TryGetValue("Pattern", out var pVal) && pVal != null ? pVal.ToString()! : "{ParentDir}_{CreationDate:yyyyMMdd}_{FileNameNoExt}.{Ext}";
            string caseTr = _node.Parameters.TryGetValue("CaseTransformation", out var ctVal) && ctVal != null ? ctVal.ToString()! : "None";

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
        var tags = RenamerTagCatalogService.GetAvailableTags();
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
                RenameMethodType.CaseConversion => "Convertir Mayúsculas",
                RenameMethodType.Numbering => "Numeración Incremental",
                RenameMethodType.ReplaceList => "Tabla de Sustituciones",
                RenameMethodType.TrimClean => "Limpieza y Recorte",
                RenameMethodType.NormalizeNumbers => "Rellenar Números (01, 02...)",
                _ => "Nuevo Método"
            }
        };

        if (methodType == RenameMethodType.ReplaceList)
        {
            newStep.ReplaceList.Add(new ReplaceListEntry { Find = "buscar", ReplaceWith = "reemplazar" });
        }

        Steps.Add(newStep);
        SelectedStep = newStep;
        GenerateLivePreview();
    }

    [RelayCommand]
    public void RemoveStep(RenameMethodStep? step)
    {
        if (step == null) return;
        int idx = Steps.IndexOf(step);
        Steps.Remove(step);
        if (Steps.Count > 0)
        {
            SelectedStep = Steps[Math.Clamp(idx, 0, Steps.Count - 1)];
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
        if (step == null) return;
        int idx = Steps.IndexOf(step);
        if (idx > 0)
        {
            Steps.Move(idx, idx - 1);
            SelectedStep = step;
            GenerateLivePreview();
        }
    }

    [RelayCommand]
    public void MoveStepDown(RenameMethodStep? step)
    {
        if (step == null) return;
        int idx = Steps.IndexOf(step);
        if (idx >= 0 && idx < Steps.Count - 1)
        {
            Steps.Move(idx, idx + 1);
            SelectedStep = step;
            GenerateLivePreview();
        }
    }

    [RelayCommand]
    public void DuplicateStep(RenameMethodStep? step)
    {
        if (step == null) return;
        var clone = step.Clone();
        clone.Name = $"{step.Name} (Copia)";

        int idx = Steps.IndexOf(step);
        Steps.Insert(idx + 1, clone);
        SelectedStep = clone;
        GenerateLivePreview();
    }

    partial void OnSelectedPresetChanged(RenamerPreset? value)
    {
        if (value == null) return;
        PipelineName = value.Name;
        Steps.Clear();
        foreach (var s in value.Steps)
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
        var previewList = _previewService.GeneratePreview(Steps.ToList(), out string srcDesc);
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

        lock (_node.Parameters)
        {
            _node.Parameters["PipelineName"] = PipelineName;
            _node.Parameters["CollisionStrategy"] = CollisionStrategy;
            _node.Parameters["MethodSteps"] = serializedSteps;
        }

        window.DialogResult = true;
        window.Close();
    }
}
