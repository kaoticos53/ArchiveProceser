using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public partial class NodeParameterViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly EventHandler<CultureInfo> _languageChangedHandler;

    public NodeViewModel? NodeOwner { get; set; }
    public NodeParameterDescriptor? Descriptor { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _key = string.Empty;

    public string DisplayName => LocalizationManager.Instance.GetString($"Param_{Key}", GetDefaultDisplayName(Key));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBooleanAndNoOptions))]
    [NotifyPropertyChangedFor(nameof(IsFolderPath))]
    [NotifyPropertyChangedFor(nameof(IsFilePath))]
    [NotifyPropertyChangedFor(nameof(HasBrowseButton))]
    [NotifyPropertyChangedFor(nameof(IsStandardInput))]
    [NotifyPropertyChangedFor(nameof(IsSlider))]
    [NotifyPropertyChangedFor(nameof(IsToggle))]
    [NotifyPropertyChangedFor(nameof(IsDropdown))]
    private object? _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOptions))]
    [NotifyPropertyChangedFor(nameof(IsBooleanAndNoOptions))]
    [NotifyPropertyChangedFor(nameof(IsFolderPath))]
    [NotifyPropertyChangedFor(nameof(IsFilePath))]
    [NotifyPropertyChangedFor(nameof(HasBrowseButton))]
    [NotifyPropertyChangedFor(nameof(IsStandardInput))]
    [NotifyPropertyChangedFor(nameof(IsDropdown))]
    private ObservableCollection<string> _options = [];

    [ObservableProperty]
    private List<VariableGroupItem> _availableVariables = [];

    public ParameterEditorType EditorType => Descriptor?.EditorType ?? DetectEditorType();

    public double SliderMin => Descriptor?.Min ?? 0;
    public double SliderMax => Descriptor?.Max ?? 100;
    public double SliderStep => Descriptor?.Step ?? 1;

    public bool HasOptions => Options.Count > 0;

    public bool IsSlider => EditorType == ParameterEditorType.Slider;

    public bool IsToggle => EditorType == ParameterEditorType.Toggle;

    public bool IsDropdown => EditorType == ParameterEditorType.Dropdown || HasOptions;

    public bool IsBooleanAndNoOptions => !IsSlider && !IsDropdown && (IsToggle || (!HasOptions && (Value is bool || (Value != null && bool.TryParse(Value.ToString(), out _)))));

    public bool IsFolderPath => EditorType == ParameterEditorType.FolderPath || (Descriptor == null && !HasOptions && !IsBooleanAndNoOptions && DetectIsFolderPath(Key));

    public bool IsFilePath => EditorType == ParameterEditorType.FilePath || (Descriptor == null && !HasOptions && !IsBooleanAndNoOptions && DetectIsFilePath(Key));

    public bool IsPasswordList => EditorType == ParameterEditorType.PasswordList || Key.Equals("PasswordList", StringComparison.OrdinalIgnoreCase);

    public bool IsMediaPreset => EditorType == ParameterEditorType.MediaPreset || Key.Equals("Preset", StringComparison.OrdinalIgnoreCase);

    public bool HasBrowseButton => IsFolderPath || IsFilePath;

    public bool IsVariableInjectorNode => NodeOwner != null && NodeOwner.IsVariableInjectorNode;

    public bool IsStandardInput => !IsSlider && !IsDropdown && !IsBooleanAndNoOptions && !HasBrowseButton && !IsPasswordList && !IsVariableInjectorNode;

    private ParameterEditorType DetectEditorType()
    {
        if (DetectIsFolderPath(Key)) return ParameterEditorType.FolderPath;
        if (DetectIsFilePath(Key)) return ParameterEditorType.FilePath;
        if (Key.Equals("PasswordList", StringComparison.OrdinalIgnoreCase)) return ParameterEditorType.PasswordList;
        if (Key.Equals("Preset", StringComparison.OrdinalIgnoreCase)) return ParameterEditorType.MediaPreset;
        return ParameterEditorType.Text;
    }

    [RelayCommand]
    public void OpenMediaPresetManager()
    {
        try
        {
            if (NodeOwner?.NodeInstance is INodeCustomActionProvider provider)
            {
                provider.ExecuteCustomAction("ManageMediaPresets", Application.Current?.MainWindow);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir el Gestor de Presets: {ex.Message}", "Error UI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void RefreshMediaPresetOptions()
    {
        // Opciones gestionadas por el descriptor del nodo
    }

    [RelayCommand]
    public void OpenPasswordManager()
    {
        try
        {
            if (NodeOwner?.NodeInstance is INodeCustomActionProvider provider)
            {
                provider.ExecuteCustomAction("ManagePasswords", Application.Current?.MainWindow);
                if (NodeOwner.NodeInstance.Parameters.TryGetValue(Key, out var updatedVal))
                {
                    Value = updatedVal;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir el Gestor de Contraseñas: {ex.Message}", "Error UI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    partial void OnKeyChanged(string? oldValue, string newValue)
    {
        if (oldValue != null)
        {
            NodeOwner?.OnParameterKeyRenamed(oldValue, newValue, Value);
        }
    }

    partial void OnValueChanged(object? oldValue, object? newValue)
    {
        NodeOwner?.OnParameterValueChanged(Key, newValue);
    }

    public NodeParameterViewModel(NodeParameterDescriptor descriptor, object? value, NodeViewModel? nodeOwner = null)
        : this(descriptor.Key, value, descriptor.Options, nodeOwner)
    {
        Descriptor = descriptor;
    }

    public NodeParameterViewModel(string key, object? value, IEnumerable<string>? options = null, NodeViewModel? nodeOwner = null)
    {
        _key = key;
        _value = value;
        NodeOwner = nodeOwner;

        // Si el valor es de tipo booleano o string booleano, asegurar tipo bool para CheckBox
        if (value is bool)
        {
            _value = value;
        }
        else if (value != null && bool.TryParse(value.ToString(), out var bVal))
        {
            _value = bVal;
        }

        if (options != null)
        {
            foreach (var opt in options)
            {
                _options.Add(opt);
            }
        }
        else
        {
            var detected = DetectOptionsForKey(key);
            foreach (var opt in detected)
            {
                _options.Add(opt);
            }
        }

        if (_options.Count > 0 && value != null)
        {
            string valStr = value.ToString() ?? string.Empty;
            var matchedOpt = _options.FirstOrDefault(o => o.Equals(valStr, StringComparison.OrdinalIgnoreCase));
            if (matchedOpt != null)
            {
                _value = matchedOpt;
            }
            else if (!string.IsNullOrWhiteSpace(valStr))
            {
                _options.Insert(0, valStr);
            }
        }

        _languageChangedHandler = (_, _) =>
        {
            OnPropertyChanged(nameof(DisplayName));
        };
        LocalizationManager.Instance.LanguageChanged += _languageChangedHandler;
    }

    [RelayCommand]
    public void OpenVariablePicker(object? targetObject)
    {
        if (targetObject is FrameworkElement element && element.Tag is EditorViewModel editor && NodeOwner != null)
        {
            RefreshAvailableVariables(editor);

            var cm = new System.Windows.Controls.ContextMenu();
            foreach (var group in AvailableVariables)
            {
                var groupHeader = new System.Windows.Controls.MenuItem
                {
                    Header = group.GroupName,
                    IsEnabled = false,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.DimGray
                };
                cm.Items.Add(groupHeader);

                foreach (var v in group.Variables)
                {
                    var mi = new System.Windows.Controls.MenuItem
                    {
                        Header = $"{v.Token}  —  {v.Description}",
                        Command = InsertVariableTokenCommand,
                        CommandParameter = v.Token
                    };
                    cm.Items.Add(mi);
                }

                cm.Items.Add(new System.Windows.Controls.Separator());
            }

            cm.PlacementTarget = element;
            cm.IsOpen = true;
        }
    }

    [RelayCommand]
    public void RefreshAvailableVariables(EditorViewModel editor)
    {
        if (NodeOwner != null && editor != null)
        {
            AvailableVariables = editor.GetUpstreamAvailableVariables(NodeOwner);
        }
    }

    [RelayCommand]
    public void InsertVariableToken(string token)
    {
        string currentVal = Value?.ToString() ?? string.Empty;
        Value = currentVal + token;
    }

    [RelayCommand]
    public void BrowsePath()
    {
        if (IsFolderPath)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = $"Seleccionar directorio para '{Key}'",
                InitialDirectory = Value?.ToString()
            };

            if (dialog.ShowDialog() == true)
            {
                Value = dialog.FolderName;
            }
        }
        else if (IsFilePath)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Seleccionar archivo para '{Key}'",
                Filter = "Todos los archivos (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                Value = dialog.FileName;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        LocalizationManager.Instance.LanguageChanged -= _languageChangedHandler;
    }

    private static string GetDefaultDisplayName(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return key;

        // Si la clave ya tiene formato PascalCase o camelCase, se puede formatear con espacios
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < key.Length; i++)
        {
            if (i > 0 && char.IsUpper(key[i]) && (!char.IsUpper(key[i - 1]) || (i + 1 < key.Length && !char.IsUpper(key[i + 1]))))
            {
                sb.Append(' ');
            }
            sb.Append(key[i]);
        }
        return sb.ToString();
    }

    private static bool DetectIsFolderPath(string key)
    {
        var k = key.ToLowerInvariant();
        if (k.Contains("file")) return false;
        return k.Contains("path") || k.Contains("folder") || k.Contains("dir") || k.Contains("destination") || k.Contains("source") || k.Contains("output");
    }

    private static bool DetectIsFilePath(string key)
    {
        var k = key.ToLowerInvariant();
        return k.Contains("file");
    }

    private static List<string> DetectOptionsForKey(string key)
    {
        return key.ToLowerInvariant() switch
        {
            "actiontype" => ["Keep", "MoveToRecycleBin", "MoveToQuarantine", "PermanentDelete"],
            "conflictstrategy" => ["Overwrite", "Skip", "RenameIncremental"],
            "collisionstrategy" => ["AutoIncrement", "Overwrite", "Skip", "Fail"],
            "renamemode" => ["Virtual", "DirectInPlace"],
            "targetformat" => ["WebP", "Jpeg", "Png"],
            "loglevel" => ["Information", "Warning", "Error", "Debug", "Critical"],
            "emitmode" => ["FilesOnly", "DirectoriesOnly", "FilesAndDirectories"],
            "casetransformation" => ["None", "Lowercase", "Uppercase", "TitleCase"],
            "operation" => ["Move", "Copy"],
            "algorithm" => ["SHA256", "MD5", "SHA512", "SHA1"],
            "operator" => [">", ">=", "<", "<=", "==", "!=", "Contains"],
            "hashmetadatakey" => ["Hash:SHA256", "Hash:MD5", "Hash:SHA512", "Hash:SHA1", "Hash"],
            "archiveformat" => ["ZIP", "TAR", "GZ", "7Z"],
            "compressiontype" => ["Deflate", "Store", "LZMA", "BZip2"],
            "preset" => ["Convertir 1080p H.264 (Universal MP4)", "Convertir 720p H.264 (MP4 Rápido)", "Convertir 4K H.265 / HEVC", "Extraer Audio MP3", "Extraer Audio AAC (M4A)", "Extraer Audio FLAC Lossless", "Convertir a GIF Animado", "WebM VP9 Open Video", "Móvil Ultra-Comprimido H.264", "Personalizado / Argumentos Libres"],
            "reportformat" => ["HTML", "Markdown", "Text", "JSON", "CSV"],
            "reportscope" => ["Consolidated", "PerFile", "Both"],
            "groupby" => ["Directory", "Flat", "Extension", "Status"],
            "theme" => ["ModernDark", "CleanLight"],
            _ => []
        };
    }
}
