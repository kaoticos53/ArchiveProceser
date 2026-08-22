using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;

namespace FileFlow.App.ViewModels;

public partial class NodeParameterViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private EventHandler? _presetsChangedHandler;

    public NodeViewModel? NodeOwner { get; set; }

    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBooleanAndNoOptions))]
    [NotifyPropertyChangedFor(nameof(IsFolderPath))]
    [NotifyPropertyChangedFor(nameof(IsFilePath))]
    [NotifyPropertyChangedFor(nameof(HasBrowseButton))]
    [NotifyPropertyChangedFor(nameof(IsStandardInput))]
    private object? _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOptions))]
    [NotifyPropertyChangedFor(nameof(IsBooleanAndNoOptions))]
    [NotifyPropertyChangedFor(nameof(IsFolderPath))]
    [NotifyPropertyChangedFor(nameof(IsFilePath))]
    [NotifyPropertyChangedFor(nameof(HasBrowseButton))]
    [NotifyPropertyChangedFor(nameof(IsStandardInput))]
    private ObservableCollection<string> _options = [];

    [ObservableProperty]
    private List<VariableGroupItem> _availableVariables = [];

    public bool HasOptions => Options.Count > 0;

    public bool IsBooleanAndNoOptions => !HasOptions && (Value is bool || (Value != null && bool.TryParse(Value.ToString(), out _)));

    public bool IsFolderPath => !HasOptions && !IsBooleanAndNoOptions && DetectIsFolderPath(Key);

    public bool IsFilePath => !HasOptions && !IsBooleanAndNoOptions && DetectIsFilePath(Key);

    public bool IsPasswordList => Key.Equals("PasswordList", StringComparison.OrdinalIgnoreCase);

    public bool IsMediaPreset => Key.Equals("Preset", StringComparison.OrdinalIgnoreCase);

    public bool HasBrowseButton => IsFolderPath || IsFilePath;

    public bool IsVariableInjectorNode => NodeOwner != null && NodeOwner.IsVariableInjectorNode;

    public bool IsStandardInput => !HasOptions && !IsBooleanAndNoOptions && !HasBrowseButton && !IsVariableInjectorNode;

    [RelayCommand]
    public void OpenMediaPresetManager()
    {
        try
        {
            var win = new Views.Components.MediaPresetManagerWindow();
            if (Application.Current?.MainWindow != null)
            {
                win.Owner = Application.Current.MainWindow;
            }
            if (win.ShowDialog() == true)
            {
                RefreshMediaPresetOptions();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir el Gestor de Presets: {ex.Message}", "Error UI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void RefreshMediaPresetOptions()
    {
        if (IsMediaPreset)
        {
            var names = Services.MediaPresetManagerService.Instance.GetPresetNames();
            Options.Clear();
            foreach (var n in names) Options.Add(n);

            string? valStr = Value?.ToString();
            if (valStr == null || !Options.Contains(valStr))
            {
                Value = Options.FirstOrDefault();
            }
        }
    }

    [RelayCommand]
    public void OpenPasswordManager()
    {
        try
        {
            var win = new Views.Components.PasswordManagerWindow(Value?.ToString() ?? string.Empty);
            if (Application.Current?.MainWindow != null)
            {
                win.Owner = Application.Current.MainWindow;
            }
            if (win.ShowDialog() == true)
            {
                Value = win.PasswordsText;
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

    public NodeParameterViewModel(string key, object? value, IEnumerable<string>? options = null, NodeViewModel? nodeOwner = null)
    {
        _key = key;
        _value = value;
        NodeOwner = nodeOwner;

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

        if (IsMediaPreset)
        {
            _presetsChangedHandler = (_, _) => RefreshMediaPresetOptions();
            Services.MediaPresetManagerService.Instance.PresetsChanged += _presetsChangedHandler;
        }
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

        if (IsMediaPreset && _presetsChangedHandler != null)
        {
            Services.MediaPresetManagerService.Instance.PresetsChanged -= _presetsChangedHandler;
            _presetsChangedHandler = null;
        }
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
            "actiontype" => ["Keep", "MoveToQuarantine", "PermanentDelete"],
            "conflictstrategy" => ["Overwrite", "Skip", "RenameIncremental"],
            "collisionstrategy" => ["AutoIncrement", "Overwrite", "Skip", "Fail"],
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
            "preset" => Services.MediaPresetManagerService.Instance.GetPresetNames(),
            _ => []
        };
    }
}
