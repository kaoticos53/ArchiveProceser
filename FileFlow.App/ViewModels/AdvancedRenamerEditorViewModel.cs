using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;
using FileFlow.Sdk.TemplateEngine;
using Microsoft.Win32;

namespace FileFlow.App.ViewModels;

public sealed record PreviewRowItem(string OriginalName, string ResultName, bool IsModified, string StatusMessage);

public sealed record TagPickerItem(string Category, string Tag, string Description);

public partial class AdvancedRenamerEditorViewModel : ObservableObject
{
    private readonly IRenameTransformEngine _transformEngine = new RenameTransformEngine();
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

        // 1. Sistema y Archivo
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "<FileName>", "Nombre de archivo completo con extensión (ej. foto.jpg)"));
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "<FileNameNoExt>", "Nombre de archivo sin extensión (ej. foto)"));
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "<Ext>", "Extensión del archivo (ej. jpg)"));
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "<DirName>", "Nombre del directorio contenedor directo"));
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "<ParentDir>", "Nombre de la carpeta superior"));
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "<FileSize>", "Tamaño del archivo formateado (ej. 4.25 MB)"));
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "<FileSizeBytes>", "Tamaño exacto en bytes"));
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "<CurrentPath>", "Ruta absoluta actual"));
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "<OriginalPath>", "Ruta de origen original del archivo"));
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "<RelativePath>", "Ruta relativa respecto al directorio raíz"));
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "{UserName}", "Nombre del usuario del sistema"));
        AvailableTags.Add(new TagPickerItem("Sistema y Archivo", "{MachineName}", "Nombre del equipo / host"));

        // 2. Secuencias y Contadores
        AvailableTags.Add(new TagPickerItem("Secuencias", "<Inc Nr:001>", "Contador incremental con 3 dígitos (001, 002...)"));
        AvailableTags.Add(new TagPickerItem("Secuencias", "<Inc Nr:1>", "Contador incremental simple (1, 2, 3...)"));
        AvailableTags.Add(new TagPickerItem("Secuencias", "<File Count>", "Cantidad total de archivos en el lote"));
        AvailableTags.Add(new TagPickerItem("Secuencias", "{Counter}", "Índice de elemento en el flujo de ejecución"));

        // 3. Fechas y Horas
        AvailableTags.Add(new TagPickerItem("Fechas y Horas", "<Year>", "Año actual a 4 dígitos (ej. 2026)"));
        AvailableTags.Add(new TagPickerItem("Fechas y Horas", "<Month>", "Mes a 2 dígitos (01-12)"));
        AvailableTags.Add(new TagPickerItem("Fechas y Horas", "<Day>", "Día a 2 dígitos (01-31)"));
        AvailableTags.Add(new TagPickerItem("Fechas y Horas", "<Hour>", "Hora actual (00-23)"));
        AvailableTags.Add(new TagPickerItem("Fechas y Horas", "<Min>", "Minutos actuales (00-59)"));
        AvailableTags.Add(new TagPickerItem("Fechas y Horas", "<Sec>", "Segundos actuales (00-59)"));
        AvailableTags.Add(new TagPickerItem("Fechas y Horas", "<Date Created:yyyyMMdd>", "Fecha de creación del archivo (formato yyyyMMdd)"));
        AvailableTags.Add(new TagPickerItem("Fechas y Horas", "<Date Modified:yyyyMMdd_HHmmss>", "Fecha de modificación del archivo"));
        AvailableTags.Add(new TagPickerItem("Fechas y Horas", "{DateNow}", "Fecha actual del sistema (yyyy-MM-dd)"));
        AvailableTags.Add(new TagPickerItem("Fechas y Horas", "{TimeNow}", "Hora actual del sistema (HH-mm-ss)"));
        AvailableTags.Add(new TagPickerItem("Fechas y Horas", "{DateTimeNow}", "Timestamp completo del sistema"));

        // 4. Fotografía y EXIF
        AvailableTags.Add(new TagPickerItem("Fotografía EXIF", "<Date Taken:yyyyMMdd>", "Fecha de captura original EXIF"));
        AvailableTags.Add(new TagPickerItem("Fotografía EXIF", "<Exif:CameraModel>", "Modelo de la cámara fotográfica"));
        AvailableTags.Add(new TagPickerItem("Fotografía EXIF", "<Exif:CameraMake>", "Fabricante de la cámara"));
        AvailableTags.Add(new TagPickerItem("Fotografía EXIF", "<Img Width>", "Ancho de la imagen en píxeles"));
        AvailableTags.Add(new TagPickerItem("Fotografía EXIF", "<Img Height>", "Alto de la imagen en píxeles"));
        AvailableTags.Add(new TagPickerItem("Fotografía EXIF", "{Orientation}", "Orientación (Landscape / Portrait / Square)"));
        AvailableTags.Add(new TagPickerItem("Fotografía EXIF", "{AspectRatio}", "Relación de aspecto (ej. 16:9)"));
        AvailableTags.Add(new TagPickerItem("Fotografía EXIF", "{Megapixels}", "Resolución en megapíxeles"));

        // 5. Audio y Video (ID3 / Tags)
        AvailableTags.Add(new TagPickerItem("Audio y Video", "<Audio:Artist>", "Nombre del artista o banda"));
        AvailableTags.Add(new TagPickerItem("Audio y Video", "<Audio:Title>", "Título de la canción / pista"));
        AvailableTags.Add(new TagPickerItem("Audio y Video", "<Audio:Album>", "Nombre del álbum"));
        AvailableTags.Add(new TagPickerItem("Audio y Video", "<Audio:TrackNumber>", "Número de pista en el álbum"));
        AvailableTags.Add(new TagPickerItem("Audio y Video", "<Audio:Year>", "Año de publicación del audio"));
        AvailableTags.Add(new TagPickerItem("Audio y Video", "<Audio:Genre>", "Género musical"));
        AvailableTags.Add(new TagPickerItem("Audio y Video", "<Video:Width>", "Ancho del fotograma de video"));
        AvailableTags.Add(new TagPickerItem("Audio y Video", "<Video:Height>", "Alto del fotograma de video"));

        // 6. Hashes y Documentos
        AvailableTags.Add(new TagPickerItem("Metadatos y Hashes", "{Hash:SHA256}", "Hash criptográfico SHA-256"));
        AvailableTags.Add(new TagPickerItem("Metadatos y Hashes", "{Hash:MD5}", "Hash MD5 del archivo"));
        AvailableTags.Add(new TagPickerItem("Metadatos y Hashes", "{Doc:WordCount}", "Número de palabras del documento"));
        AvailableTags.Add(new TagPickerItem("Metadatos y Hashes", "{Doc:PageCount}", "Número de páginas del documento"));
        AvailableTags.Add(new TagPickerItem("Metadatos y Hashes", "{Cli:StdOut}", "Salida estándar de ejecución CLI"));
        AvailableTags.Add(new TagPickerItem("Metadatos y Hashes", "{Cli:ExitCode}", "Código de salida de proceso CLI"));

        // 7. Funciones de Expresión
        AvailableTags.Add(new TagPickerItem("Funciones de Expresión", "{Upper(FileNameNoExt)}", "Convertir a MAYÚSCULAS"));
        AvailableTags.Add(new TagPickerItem("Funciones de Expresión", "{Lower(Ext)}", "Convertir a minúsculas"));
        AvailableTags.Add(new TagPickerItem("Funciones de Expresión", "{Substring(FileNameNoExt, 0, 8)}", "Extraer subcadena por índice y longitud"));
        AvailableTags.Add(new TagPickerItem("Funciones de Expresión", "{Sanitize(CameraModel)}", "Sanitizar caracteres ilegales"));
        AvailableTags.Add(new TagPickerItem("Funciones de Expresión", "{PadLeft(Counter, 4, \"0\")}", "Rellenar con ceros a la izquierda"));
        AvailableTags.Add(new TagPickerItem("Funciones de Expresión", "{RegexMatch(FileNameNoExt, \"[0-9]+\")}", "Extraer primera coincidencia Regex"));
        AvailableTags.Add(new TagPickerItem("Funciones de Expresión", "{RegexReplace(FileNameNoExt, \"[^a-zA-Z0-9]\", \"_\")}", "Reemplazar patrón con Regex"));
        AvailableTags.Add(new TagPickerItem("Funciones de Expresión", "{Coalesce(DateTaken, DateCreated, DateNow)}", "Primer valor no vacío de la lista"));
        AvailableTags.Add(new TagPickerItem("Funciones de Expresión", "{FormatDate(DateCreated, \"yyyy-MM\")}", "Formatear fecha con patrón"));
        AvailableTags.Add(new TagPickerItem("Funciones de Expresión", "{Default(DateTaken, \"2026-01-01\")}", "Valor de respaldo si está vacío"));
        AvailableTags.Add(new TagPickerItem("Funciones de Expresión", "{FileAgeDays(DateCreated)}", "Días transcurridos desde fecha"));

        // 8. Cargar variables de nodos aguas arriba
        var connections = _nodeViewModel.ParentEditor?.Connections?.ToList()
            ?? (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm ? mainVm.Editor.Connections.ToList() : new List<ConnectionViewModel>());
        
        var upstreamGroups = new VariableDiscoveryService().GetAvailableVariables(_nodeViewModel, connections);
        foreach (var group in upstreamGroups)
        {
            if (group.GroupName.Contains("System", StringComparison.OrdinalIgnoreCase) ||
                group.GroupName.Contains("Expression", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var v in group.Variables)
            {
                if (!AvailableTags.Any(t => t.Tag.Equals(v.Token, StringComparison.OrdinalIgnoreCase)))
                {
                    AvailableTags.Add(new TagPickerItem(group.GroupName, v.Token, v.Description));
                }
            }
        }

        // 9. Cargar variables inyectadas de cualquier VariableInjectorNode presente en el flujo
        var editorNodes = _nodeViewModel.ParentEditor?.Nodes?.ToList()
            ?? (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm2 ? mainVm2.Editor.Nodes.ToList() : new List<NodeViewModel>());

        foreach (var injectorNode in editorNodes.Where(n => n.IsVariableInjectorNode))
        {
            string groupName = $"🔗 {injectorNode.Title}";
            foreach (var param in injectorNode.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(param.Key))
                {
                    string token = $"{{{param.Key}}}";
                    if (!AvailableTags.Any(t => t.Tag.Equals(token, StringComparison.OrdinalIgnoreCase)))
                    {
                        AvailableTags.Add(new TagPickerItem(groupName, token, $"Variable personalizada: '{param.Key}' = '{param.Value}'"));
                    }
                }
            }
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

        var sampleItems = GetSampleItemsForPreview(out string srcDesc);
        PreviewSourceDescription = srcDesc;

        var batch = new RenameBatchContext();

        // Inyectar variables personalizadas de nodos VariableInjectorNode del flujo
        var editorNodes = _nodeViewModel.ParentEditor?.Nodes?.ToList()
            ?? (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm ? mainVm.Editor.Nodes.ToList() : new List<NodeViewModel>());

        var injectorNodes = editorNodes.Where(n => n.IsVariableInjectorNode).ToList();

        foreach (var item in sampleItems)
        {
            foreach (var injectorNode in injectorNodes)
            {
                foreach (var param in injectorNode.Parameters)
                {
                    if (!string.IsNullOrWhiteSpace(param.Key))
                    {
                        item.Metadata[param.Key] = param.Value?.ToString() ?? string.Empty;
                    }
                }
            }

            var res = _transformEngine.Transform(item.FileName, item, Steps.ToList(), batch);
            bool isModified = !string.Equals(item.FileName, res.ResultFileName, StringComparison.Ordinal);
            string status = isModified ? "✓ Modificado" : "= Sin cambios";

            if (!string.IsNullOrEmpty(res.ErrorMessage))
            {
                status = $"⚠️ {res.ErrorMessage}";
            }

            PreviewItems.Add(new PreviewRowItem(item.FileName, res.ResultFileName, isModified, status));
        }
    }

    private List<FileItemContext> GetSampleItemsForPreview(out string sourceDescription)
    {
        var editorNodes = _nodeViewModel.ParentEditor?.Nodes?.ToList()
            ?? (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm ? mainVm.Editor.Nodes.ToList() : new List<NodeViewModel>());
        var connections = _nodeViewModel.ParentEditor?.Connections?.ToList()
            ?? (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm2 ? mainVm2.Editor.Connections.ToList() : new List<ConnectionViewModel>());

        // 1. Buscar FolderSourceNode (priorizando nodos conectados aguas arriba)
        var folderNode = FindUpstreamFolderSourceNode(_nodeViewModel, connections)
            ?? editorNodes.FirstOrDefault(n => n.IsFolderSourceNode);

        if (folderNode != null)
        {
            string rawSourcePath = folderNode.Parameters.FirstOrDefault(p => p.Key.Equals("SourcePath", StringComparison.OrdinalIgnoreCase))?.Value?.ToString()
                ?? (folderNode.NodeInstance.Parameters.TryGetValue("SourcePath", out var sp) ? sp?.ToString() : null)
                ?? string.Empty;

            bool recursive = true;
            var recParam = folderNode.Parameters.FirstOrDefault(p => p.Key.Equals("Recursive", StringComparison.OrdinalIgnoreCase));
            if (recParam != null && bool.TryParse(recParam.Value?.ToString(), out bool recVal))
            {
                recursive = recVal;
            }
            else if (folderNode.NodeInstance.Parameters.TryGetValue("Recursive", out var rObj) && rObj is bool rBool)
            {
                recursive = rBool;
            }

            string emitMode = folderNode.Parameters.FirstOrDefault(p => p.Key.Equals("EmitMode", StringComparison.OrdinalIgnoreCase))?.Value?.ToString()
                ?? (folderNode.NodeInstance.Parameters.TryGetValue("EmitMode", out var emObj) ? emObj?.ToString() : "FilesOnly")
                ?? "FilesOnly";

            if (!string.IsNullOrWhiteSpace(rawSourcePath))
            {
                string resolvedPath = VariableTemplateResolver.Resolve(rawSourcePath, new FileItemContext(Directory.GetCurrentDirectory()));
                if (!Path.IsPathRooted(resolvedPath))
                {
                    resolvedPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), resolvedPath));
                }

                if (Directory.Exists(resolvedPath))
                {
                    var items = new List<FileItemContext>();
                    var enumOptions = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = recursive,
                        ReturnSpecialDirectories = false
                    };

                    try
                    {
                        if (emitMode.Equals("DirectoriesOnly", StringComparison.OrdinalIgnoreCase))
                        {
                            var dirs = Directory.EnumerateDirectories(resolvedPath, "*", enumOptions).Take(100);
                            foreach (var dir in dirs)
                            {
                                items.Add(new FileItemContext(dir, isDirectory: true));
                            }
                        }
                        else
                        {
                            var files = Directory.EnumerateFiles(resolvedPath, "*", enumOptions).Take(100);
                            foreach (var file in files)
                            {
                                items.Add(new FileItemContext(file, isDirectory: false));
                            }

                            if (emitMode.Equals("FilesAndDirectories", StringComparison.OrdinalIgnoreCase) && items.Count < 100)
                            {
                                int remaining = 100 - items.Count;
                                var dirs = Directory.EnumerateDirectories(resolvedPath, "*", enumOptions).Take(remaining);
                                foreach (var dir in dirs)
                                {
                                    items.Add(new FileItemContext(dir, isDirectory: true));
                                }
                            }
                        }
                    }
                    catch
                    {
                        // En caso de error de lectura I/O, fallback a muestras predefinidas
                    }

                    if (items.Count > 0)
                    {
                        string dirDisplayName = Path.GetFileName(resolvedPath);
                        if (string.IsNullOrWhiteSpace(dirDisplayName)) dirDisplayName = resolvedPath;
                        sourceDescription = $"(Muestra de {items.Count} archivo(s) real(es) de '{dirDisplayName}')";
                        return items;
                    }
                }
            }
        }

        sourceDescription = "(Muestras sintéticas predefinidas)";
        var syntheticSamples = new[]
        {
            ("1 - pepe.jpg", new Dictionary<string, object?> { ["DateTaken"] = new DateTime(2026, 8, 15) }),
            ("2 - jaco.jpg", new Dictionary<string, object?> { ["DateTaken"] = new DateTime(2026, 8, 15) }),
            ("10 - kilo.jpg", new Dictionary<string, object?> { ["DateTaken"] = new DateTime(2026, 8, 15) }),
            ("serie guapa 1x1.mov", new Dictionary<string, object?> { ["Show"] = "Serie Guapa" }),
            ("serie guapa papo 1x2.mov", new Dictionary<string, object?> { ["Show"] = "Serie Guapa" }),
            ("serie guapa jose 1x10.mov", new Dictionary<string, object?> { ["Show"] = "Serie Guapa" }),
            ("DSC_0042.JPG", new Dictionary<string, object?> { ["Exif:CameraModel"] = "SonyA7", ["DateTaken"] = new DateTime(2026, 8, 15), ["CameraModel"] = "SonyA7" }),
            ("01 - Daft Punk [Live 2007].mp3", new Dictionary<string, object?> { ["Audio:Artist"] = "Daft Punk", ["Audio:Title"] = "Around The World", ["Audio:TrackNumber"] = "01" }),
            ("informe_financiero_borrador_v1.docx", new Dictionary<string, object?> { ["Year"] = "2026", ["DirName"] = "Contabilidad", ["ParentDir"] = "Contabilidad", ["Hash:SHA256"] = "a1b2c3d4e5f6" })
        };

        var result = new List<FileItemContext>();
        foreach (var (fileName, metadata) in syntheticSamples)
        {
            var item = new FileItemContext(Path.Combine(@"C:\Muestras", fileName));
            foreach (var (k, v) in metadata) item.Metadata[k] = v;
            result.Add(item);
        }
        return result;
    }

    private static NodeViewModel? FindUpstreamFolderSourceNode(NodeViewModel targetNode, IEnumerable<ConnectionViewModel> connections)
    {
        var visited = new HashSet<NodeViewModel>();
        var queue = new Queue<NodeViewModel>();
        queue.Enqueue(targetNode);
        var connList = connections.ToList();

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            var incoming = connList.Where(c => c.Target.NodeOwner == curr).ToList();
            foreach (var conn in incoming)
            {
                var upstream = conn.Source.NodeOwner;
                if (upstream.IsFolderSourceNode)
                {
                    return upstream;
                }
                if (visited.Add(upstream))
                {
                    queue.Enqueue(upstream);
                }
            }
        }
        return null;
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
