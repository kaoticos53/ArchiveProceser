using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;

namespace FileFlow.App.ViewModels;

public partial class NodeParameterViewModel : ObservableObject
{
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

    public bool HasBrowseButton => IsFolderPath || IsFilePath;

    public bool IsVariableInjectorNode => NodeOwner != null && NodeOwner.IsVariableInjectorNode;

    public bool IsStandardInput => !HasOptions && !IsBooleanAndNoOptions && !HasBrowseButton && !IsVariableInjectorNode;

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
