using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FileFlow.App.ViewModels;

public partial class NodeParameterViewModel : ObservableObject
{
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

    public bool HasOptions => Options.Count > 0;

    public bool IsBooleanAndNoOptions => !HasOptions && (Value is bool || (Value != null && bool.TryParse(Value.ToString(), out _)));

    public bool IsFolderPath => !HasOptions && !IsBooleanAndNoOptions && DetectIsFolderPath(Key);

    public bool IsFilePath => !HasOptions && !IsBooleanAndNoOptions && DetectIsFilePath(Key);

    public bool HasBrowseButton => IsFolderPath || IsFilePath;

    public bool IsStandardInput => !HasOptions && !IsBooleanAndNoOptions && !HasBrowseButton;

    public NodeParameterViewModel(string key, object? value, IEnumerable<string>? options = null)
    {
        _key = key;
        _value = value;

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
            "targetformat" => ["WebP", "Jpeg", "Png"],
            "loglevel" => ["Information", "Warning", "Error", "Debug", "Critical"],
            _ => []
        };
    }
}
