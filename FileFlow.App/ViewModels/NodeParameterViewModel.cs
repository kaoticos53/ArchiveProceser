using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.App.ViewModels;

public partial class NodeParameterViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly EventHandler<CultureInfo> _languageChangedHandler;
    private readonly ILocalizationService _loc;
    private readonly IDialogService _dialogService;
    private FileItemContext? _activeEvaluationContext;
    private string? _sourceRootPath;

    public NodeViewModel? NodeOwner { get; set; }
    public NodeParameterDescriptor? Descriptor { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _key = string.Empty;

    public string DisplayName => _loc.GetString($"Param_{Key}", GetDefaultDisplayName(Key));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBooleanAndNoOptions))]
    [NotifyPropertyChangedFor(nameof(IsFolderPath))]
    [NotifyPropertyChangedFor(nameof(IsFilePath))]
    [NotifyPropertyChangedFor(nameof(HasBrowseButton))]
    [NotifyPropertyChangedFor(nameof(IsMultiLine))]
    [NotifyPropertyChangedFor(nameof(IsStandardInput))]
    [NotifyPropertyChangedFor(nameof(IsNumber))]
    [NotifyPropertyChangedFor(nameof(IsSlider))]
    [NotifyPropertyChangedFor(nameof(IsToggle))]
    [NotifyPropertyChangedFor(nameof(IsDropdown))]
    [NotifyPropertyChangedFor(nameof(IsEditableDropdown))]
    [NotifyPropertyChangedFor(nameof(HasOptionsAndNotEditable))]
    [NotifyPropertyChangedFor(nameof(IsFileVersionSelector))]
    [NotifyPropertyChangedFor(nameof(ActiveVersionTag))]
    [NotifyPropertyChangedFor(nameof(IsStandardRow))]
    [NotifyPropertyChangedFor(nameof(IsMultilineRow))]
    [NotifyPropertyChangedFor(nameof(ValueAsBool))]
    [NotifyPropertyChangedFor(nameof(SliderValue))]
    [NotifyPropertyChangedFor(nameof(SliderDisplayValue))]
    private object? _value;

    [ObservableProperty]
    private string _evaluatedValue = string.Empty;

    [ObservableProperty]
    private bool _hasExpression;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isCopied;

    [ObservableProperty]
    private bool _isCustomExpressionMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOptions))]
    [NotifyPropertyChangedFor(nameof(IsBooleanAndNoOptions))]
    [NotifyPropertyChangedFor(nameof(IsFolderPath))]
    [NotifyPropertyChangedFor(nameof(IsFilePath))]
    [NotifyPropertyChangedFor(nameof(HasBrowseButton))]
    [NotifyPropertyChangedFor(nameof(IsMultiLine))]
    [NotifyPropertyChangedFor(nameof(IsStandardInput))]
    [NotifyPropertyChangedFor(nameof(IsNumber))]
    [NotifyPropertyChangedFor(nameof(IsDropdown))]
    [NotifyPropertyChangedFor(nameof(IsEditableDropdown))]
    [NotifyPropertyChangedFor(nameof(HasOptionsAndNotEditable))]
    [NotifyPropertyChangedFor(nameof(IsStandardRow))]
    [NotifyPropertyChangedFor(nameof(IsMultilineRow))]
    private ObservableCollection<string> _options = [];

    [ObservableProperty]
    private List<VariableGroupItem> _availableVariables = [];

    private bool _hasLoadedVersions;
    private bool _isRefreshingVersions;
    private readonly ObservableCollection<FileVersionOption> _availableVersionOptions = [];
    public ObservableCollection<FileVersionOption> AvailableVersionOptions
    {
        get
        {
            if (!_hasLoadedVersions && IsFileVersionSelector)
            {
                _hasLoadedVersions = true;
                RefreshAvailableVersions();
            }
            return _availableVersionOptions;
        }
    }

    public FileVersionOption? SelectedVersionOption
    {
        get
        {
            string valStr = Value?.ToString()?.Trim() ?? string.Empty;
            return AvailableVersionOptions.FirstOrDefault(o =>
                string.Equals(valStr, o.Token, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(valStr, o.Tag, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(ActiveVersionTag) && string.Equals(ActiveVersionTag, o.Tag, StringComparison.OrdinalIgnoreCase)));
        }
        set
        {
            if (value != null && !string.Equals(Value?.ToString(), value.Token, StringComparison.OrdinalIgnoreCase))
            {
                Value = value.Token;
                IsCustomExpressionMode = false;
                UpdateVersionOptionsSelection();
                OnPropertyChanged(nameof(ActiveVersionTag));
                OnPropertyChanged(nameof(SelectedVersionOption));
            }
        }
    }

    public ParameterEditorType EditorType => Descriptor?.EditorType ?? DetectEditorType();

    public double SliderMin => Descriptor?.Min ?? 0;
    public double SliderMax => Descriptor?.Max ?? 100;
    public double SliderStep => Descriptor?.Step ?? 1;

    public bool ValueAsBool
    {
        get
        {
            if (Value is bool b) return b;
            if (Value is int i) return i != 0;
            if (Value is long l) return l != 0;
            if (Value != null)
            {
                string s = Value.ToString()?.Trim() ?? string.Empty;
                if (bool.TryParse(s, out var pb)) return pb;
                if (s == "1") return true;
                if (s == "0") return false;
            }
            return false;
        }
        set
        {
            Value = value;
            OnPropertyChanged(nameof(ValueAsBool));
        }
    }

    public double SliderValue
    {
        get
        {
            if (Value is double d) return d;
            if (Value is float f) return f;
            if (Value is int i) return i;
            if (Value is long l) return l;
            if (Value != null && double.TryParse(Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            if (Value != null && double.TryParse(Value.ToString(), out var parsedLocal))
                return parsedLocal;
            return SliderMin;
        }
        set
        {
            Value = SliderStep == 1 ? (int)Math.Round(value) : Math.Round(value, 2);
            OnPropertyChanged(nameof(SliderValue));
            OnPropertyChanged(nameof(SliderDisplayValue));
        }
    }

    public string SliderDisplayValue
    {
        get
        {
            double val = SliderValue;
            if (SliderStep == 1)
            {
                return ((int)Math.Round(val)).ToString();
            }
            return val.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    public bool HasOptions => Options.Count > 0;

    public bool IsSlider => EditorType == ParameterEditorType.Slider;

    public bool IsNumber => EditorType == ParameterEditorType.Number;

    public bool IsToggle => EditorType == ParameterEditorType.Toggle;

    public bool IsDropdown => (EditorType == ParameterEditorType.Dropdown || EditorType == ParameterEditorType.EditableDropdown || HasOptions) && !IsFileVersionSelector;
    public bool IsEditableDropdown => EditorType == ParameterEditorType.EditableDropdown;
    public bool HasOptionsAndNotEditable => HasOptions && !IsEditableDropdown;
    public bool IsFileVersionSelector => EditorType == ParameterEditorType.FileVersionSelector;

    public bool IsBooleanAndNoOptions
    {
        get
        {
            if (IsSlider || IsDropdown || IsFileVersionSelector || IsNumber) return false;
            if (IsToggle) return true;
            if (HasOptions) return false;
            if (Value is bool) return true;
            if (Value is int vi && (vi == 0 || vi == 1)) return true;
            if (Value != null)
            {
                string s = Value.ToString()?.Trim() ?? string.Empty;
                return s == "0" || s == "1" || bool.TryParse(s, out _);
            }
            return false;
        }
    }

    public bool IsFolderPath => (EditorType == ParameterEditorType.FolderPath || (Descriptor == null && !HasOptions && !IsBooleanAndNoOptions && DetectIsFolderPath(Key))) && !IsFileVersionSelector;

    public bool IsFilePath => (EditorType == ParameterEditorType.FilePath || (Descriptor == null && !HasOptions && !IsBooleanAndNoOptions && DetectIsFilePath(Key))) && !IsFileVersionSelector;

    public bool IsPasswordList => EditorType == ParameterEditorType.PasswordList || Key.Equals("PasswordList", StringComparison.OrdinalIgnoreCase);

    public bool IsMediaPreset => EditorType == ParameterEditorType.MediaPreset || Key.Equals("Preset", StringComparison.OrdinalIgnoreCase);

    public bool IsMultiLine => (EditorType == ParameterEditorType.MultiLineText || (!HasOptions && !HasBrowseButton && !IsBooleanAndNoOptions && !IsSlider && !IsPasswordList && !IsMediaPreset && DetectIsMultiLine(Key))) && !IsFileVersionSelector;

    public bool HasBrowseButton => (IsFolderPath || IsFilePath) && !IsFileVersionSelector;

    public bool IsVariableInjectorNode => NodeOwner != null && NodeOwner.IsVariableInjectorNode;

    public bool IsStandardInput => !IsSlider && !IsNumber && !IsDropdown && !IsBooleanAndNoOptions && !HasBrowseButton && !IsPasswordList && !IsVariableInjectorNode && !IsMultiLine && !IsFileVersionSelector;
    public bool IsStandardRow => !IsVariableInjectorNode && !IsMultiLine;
    public bool IsMultilineRow => !IsVariableInjectorNode && IsMultiLine;

    public string ActiveVersionTag
    {
        get
        {
            string valStr = Value?.ToString()?.Trim() ?? string.Empty;
            foreach (var opt in _availableVersionOptions)
            {
                if (string.Equals(valStr, opt.Token, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(valStr, opt.Tag, StringComparison.OrdinalIgnoreCase))
                {
                    return opt.Tag;
                }
            }

            if (string.Equals(valStr, "{OriginalPath}", StringComparison.OrdinalIgnoreCase) || string.Equals(valStr, "Original", StringComparison.OrdinalIgnoreCase))
                return "Original";
            if (string.Equals(valStr, "{CurrentPath}", StringComparison.OrdinalIgnoreCase) || string.Equals(valStr, "Current", StringComparison.OrdinalIgnoreCase))
                return "Current";
            if (string.Equals(valStr, "{File:Optimized}", StringComparison.OrdinalIgnoreCase) || string.Equals(valStr, "Optimized", StringComparison.OrdinalIgnoreCase))
                return "Optimized";
            if (string.Equals(valStr, "{File:NoBackground}", StringComparison.OrdinalIgnoreCase) || string.Equals(valStr, "NoBackground", StringComparison.OrdinalIgnoreCase))
                return "NoBackground";
            if (string.Equals(valStr, "{File:SuperResolution}", StringComparison.OrdinalIgnoreCase) || string.Equals(valStr, "SuperResolution", StringComparison.OrdinalIgnoreCase))
                return "SuperResolution";

            return string.Empty;
        }
    }

    [RelayCommand]
    public void SelectVersionOption(FileVersionOption? option)
    {
        if (option == null) return;
        Value = option.Token;
        IsCustomExpressionMode = false;
        UpdateVersionOptionsSelection();
        OnPropertyChanged(nameof(ActiveVersionTag));
        OnPropertyChanged(nameof(SelectedVersionOption));
    }

    [RelayCommand]
    public void ToggleCustomExpressionMode()
    {
        IsCustomExpressionMode = !IsCustomExpressionMode;
    }

    public void UpdateVersionOptionsSelection()
    {
        if (!IsFileVersionSelector) return;
        string valStr = Value?.ToString()?.Trim() ?? string.Empty;
        string activeTag = ActiveVersionTag;

        foreach (var opt in _availableVersionOptions)
        {
            opt.IsSelected = string.Equals(valStr, opt.Token, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(valStr, opt.Tag, StringComparison.OrdinalIgnoreCase) ||
                             (!string.IsNullOrEmpty(activeTag) && string.Equals(activeTag, opt.Tag, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void RefreshAvailableVersions()
    {
        if (!IsFileVersionSelector || NodeOwner == null || _isRefreshingVersions) return;

        _isRefreshingVersions = true;
        try
        {
            var editor = ResolveEditor();
            var conns = editor?.Connections ?? Enumerable.Empty<ConnectionViewModel>();
            var versions = (editor?.VariableDiscoveryService ?? VariableDiscoveryService.Instance).GetAvailableFileVersions(NodeOwner, conns);

            void UpdateList()
            {
                bool isSame = _availableVersionOptions.Count == versions.Count &&
                              _availableVersionOptions.Zip(versions, (a, b) => a.Tag == b.Tag && a.Token == b.Token).All(x => x);

                if (!isSame)
                {
                    _availableVersionOptions.Clear();
                    foreach (var v in versions)
                    {
                        _availableVersionOptions.Add(v);
                    }
                }

                string valStr = Value?.ToString()?.Trim() ?? string.Empty;
                bool matchesChip = _availableVersionOptions.Any(o =>
                    string.Equals(valStr, o.Token, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(valStr, o.Tag, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(valStr) && !matchesChip)
                {
                    IsCustomExpressionMode = true;
                }

                _hasLoadedVersions = true;
                UpdateVersionOptionsSelection();
                OnPropertyChanged(nameof(ActiveVersionTag));
                OnPropertyChanged(nameof(SelectedVersionOption));
            }

            UpdateList();
        }
        finally
        {
            _isRefreshingVersions = false;
        }
    }

    private ParameterEditorType DetectEditorType()
    {
        if (DetectIsFileVersion(Key)) return ParameterEditorType.FileVersionSelector;
        if (DetectIsFolderPath(Key)) return ParameterEditorType.FolderPath;
        if (DetectIsFilePath(Key)) return ParameterEditorType.FilePath;
        if (Key.Equals("PasswordList", StringComparison.OrdinalIgnoreCase)) return ParameterEditorType.PasswordList;
        if (Key.Equals("Preset", StringComparison.OrdinalIgnoreCase)) return ParameterEditorType.MediaPreset;
        if (DetectIsMultiLine(Key)) return ParameterEditorType.MultiLineText;
        return ParameterEditorType.Text;
    }

    private static bool DetectIsFileVersion(string key) =>
        key.Equals("TargetFile", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("CandidateA", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("CandidateB", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("TrueFile", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("FalseFile", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("FileVersion", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    public void OpenMediaPresetManager()
    {
        try
        {
            if (NodeOwner?.NodeInstance is INodeCustomActionProvider provider)
            {
                provider.ExecuteCustomAction("ManageMediaPresets", new NodeCustomActionContext(App.MainWindow, () => NodeOwner?.SyncParametersFromNodeInstance()));
            }
        }
        catch (Exception ex)
        {
            string msg = string.Format(_loc.GetString("Msg_OpenPresetsError", "Error al abrir el Gestor de Presets: {0}"), ex.Message);
            string title = _loc.GetString("Error", "Error");
            _dialogService.ShowError(msg, title);
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
                provider.ExecuteCustomAction("ManagePasswords", new NodeCustomActionContext(App.MainWindow, () => NodeOwner?.SyncParametersFromNodeInstance()));
                if (NodeOwner.NodeInstance.Parameters.TryGetValue(Key, out var updatedVal))
                {
                    Value = updatedVal;
                }
            }
        }
        catch (Exception ex)
        {
            string msg = string.Format(_loc.GetString("Msg_OpenPasswordsError", "Error al abrir el Gestor de Contraseñas: {0}"), ex.Message);
            string title = _loc.GetString("Error", "Error");
            _dialogService.ShowError(msg, title);
        }
    }

    public void UpdateOptions(IEnumerable<string>? newOptions)
    {
        if (newOptions == null) return;
        var list = newOptions.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();

        void Apply()
        {
            if (Options.SequenceEqual(list)) return;

            Options.Clear();
            foreach (var opt in list)
            {
                Options.Add(opt);
            }

            string valStr = Value?.ToString() ?? string.Empty;
            var matchedOpt = Options.FirstOrDefault(o => o.Equals(valStr, StringComparison.OrdinalIgnoreCase));
            if (matchedOpt != null)
            {
                Value = matchedOpt;
            }
            else if (!string.IsNullOrWhiteSpace(valStr) && Options.Count > 0)
            {
                Options.Insert(0, valStr);
            }

            OnPropertyChanged(nameof(HasOptions));
            OnPropertyChanged(nameof(IsDropdown));
        }

        Apply();
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
        RecalculateEvaluatedValue();
        if (IsFileVersionSelector)
        {
            UpdateVersionOptionsSelection();
            OnPropertyChanged(nameof(SelectedVersionOption));
        }
    }

    public void UpdateEvaluationContext(FileItemContext? context, string? sourceRootPath = null)
    {
        _activeEvaluationContext = context;
        _sourceRootPath = sourceRootPath;
        RecalculateEvaluatedValue();
    }

    public void RecalculateEvaluatedValue()
    {
        string? valStr = Value?.ToString();
        if (string.IsNullOrEmpty(valStr))
        {
            EvaluatedValue = string.Empty;
            HasExpression = false;
            return;
        }

        bool containsTags = (valStr.Contains('{') && valStr.Contains('}')) || (valStr.Contains('<') && valStr.Contains('>'));
        HasExpression = containsTags;

        if (!containsTags)
        {
            EvaluatedValue = valStr;
            return;
        }

        try
        {
            var ctx = _activeEvaluationContext ?? new FileItemContext();
            EvaluatedValue = VariableTemplateResolver.Resolve(valStr, ctx, _sourceRootPath);
        }
        catch
        {
            EvaluatedValue = valStr;
        }
    }

    [RelayCommand]
    public async Task CopyEvaluatedValueAsync()
    {
        if (string.IsNullOrEmpty(EvaluatedValue)) return;
        try
        {
            LogViewModel.SafeSetClipboardText(EvaluatedValue);
            IsCopied = true;
            await Task.Delay(1500);
            IsCopied = false;
        }
        catch
        {
            // Ignorar excepciones de concurrencia del portapapeles
        }
    }

    public NodeParameterViewModel(NodeParameterDescriptor descriptor, object? value, NodeViewModel? nodeOwner = null, ILocalizationService? localizationService = null, IDialogService? dialogService = null)
        : this(descriptor.Key, value, descriptor.Options, nodeOwner, localizationService, dialogService)
    {
        Descriptor = descriptor;
    }

    public NodeParameterViewModel(string key, object? value, IEnumerable<string>? options = null, NodeViewModel? nodeOwner = null, ILocalizationService? localizationService = null, IDialogService? dialogService = null)
    {
        _loc = localizationService ?? LocalizationManager.Instance;
        _dialogService = dialogService ?? AvaloniaDialogService.Instance;
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

        RecalculateEvaluatedValue();

        _languageChangedHandler = (_, _) =>
        {
            OnPropertyChanged(nameof(DisplayName));
        };
        _loc.LanguageChanged += _languageChangedHandler;
    }

    private EditorViewModel? ResolveEditor(Control? element = null)
    {
        if (element?.Tag is EditorViewModel evm)
        {
            return evm;
        }
        if (element?.Tag is NodeInspectorViewModel nivm)
        {
            return nivm.Editor;
        }
        if (NodeOwner?.ParentEditor != null)
        {
            return NodeOwner.ParentEditor;
        }
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is MainViewModel mainVm)
        {
            return mainVm.Editor;
        }
        return null;
    }

    [RelayCommand]
    public void OpenVariablePicker(object? targetObject)
    {
        var element = targetObject as Control;
        var editor = ResolveEditor(element);

        if (editor != null)
        {
            RefreshAvailableVariables(editor);
        }
        else if (AvailableVariables.Count == 0)
        {
            AvailableVariables = Services.VariableDiscoveryService.Instance.GetAvailableVariables(NodeOwner, []);
        }

        var cm = new ContextMenu
        {
            MaxHeight = 500
        };

        // 1. Catálogo completo como primera opción destacada
        var miFullCatalog = new MenuItem
        {
            Header = _loc.GetString("VarPicker_OpenFullCatalog", "🔍 Abrir Catálogo Completo de Variables..."),
            FontWeight = FontWeight.Bold,
            Command = OpenVariableCatalogCommand,
            CommandParameter = element
        };
        cm.Items.Add(miFullCatalog);
        cm.Items.Add(new Separator());

        // 2. Grupos organizados en submenús para que la lista no se corte por abajo
        foreach (var group in AvailableVariables)
        {
            if (group.Variables.Count == 0) continue;

            var subMenu = new MenuItem
            {
                Header = $"{group.GroupName} ({group.Variables.Count})",
                FontWeight = group.IsUpstream ? FontWeight.SemiBold : FontWeight.Normal
            };

            foreach (var v in group.Variables)
            {
                var mi = new MenuItem
                {
                    Header = $"{v.Token}  —  {v.Description}",
                    Command = InsertVariableTokenCommand,
                    CommandParameter = v.Token
                };
                if (!string.IsNullOrEmpty(v.SampleValue))
                {
                    ToolTip.SetTip(mi, $"Ejemplo: {v.SampleValue}");
                }
                subMenu.Items.Add(mi);
            }

            cm.Items.Add(subMenu);
        }

        if (element != null)
        {
            cm.Open(element);
        }
    }

    [RelayCommand]
    public void OpenVariableCatalog(object? targetObject)
    {
        var element = targetObject as Control;
        var editor = ResolveEditor(element);

        if (editor != null)
        {
            RefreshAvailableVariables(editor);
        }
        else if (AvailableVariables.Count == 0)
        {
            AvailableVariables = Services.VariableDiscoveryService.Instance.GetAvailableVariables(NodeOwner, []);
        }

        var previewContext = (editor?.VariableDiscoveryService ?? Services.VariableDiscoveryService.Instance).CreatePreviewItem(NodeOwner);
        var owner = (element != null ? TopLevel.GetTopLevel(element) as Window : null) ?? App.MainWindow;
        var dialog = new Views.Components.VariablePickerWindow(AvailableVariables, NodeOwner, previewContext, _loc);

        if (owner != null)
        {
            _ = dialog.ShowDialog<bool>(owner).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result && !string.IsNullOrEmpty(dialog.SelectedToken))
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        InsertVariableToken(dialog.SelectedToken);
                    });
                }
            }, TaskScheduler.Default);
        }
    }

    [RelayCommand]
    public void RefreshAvailableVariables(EditorViewModel editor)
    {
        if (editor != null)
        {
            if (NodeOwner != null)
            {
                AvailableVariables = editor.GetUpstreamAvailableVariables(NodeOwner);
            }
            else
            {
                AvailableVariables = editor.VariableDiscoveryService.GetAvailableVariables(null, editor.Connections);
            }
        }
    }

    [RelayCommand]
    public void InsertVariableToken(string token)
    {
        string currentVal = Value?.ToString() ?? string.Empty;
        Value = currentVal + token;
    }

    [RelayCommand]
    public async Task BrowsePathAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel?.StorageProvider != null)
            {
                if (IsFolderPath)
                {
                    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                    {
                        Title = $"Seleccionar directorio para '{Key}'",
                        AllowMultiple = false
                    });
                    if (folders != null && folders.Count > 0)
                    {
                        Value = folders[0].Path.LocalPath;
                    }
                }
                else if (IsFilePath)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                    {
                        Title = $"Seleccionar archivo para '{Key}'",
                        AllowMultiple = false
                    });
                    if (files != null && files.Count > 0)
                    {
                        Value = files[0].Path.LocalPath;
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _loc.LanguageChanged -= _languageChangedHandler;
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

    [RelayCommand]
    public void OpenTextEditor(object? targetObject)
    {
        var owner = (targetObject is Control c ? TopLevel.GetTopLevel(c) as Window : null) ?? App.MainWindow;
        var dialog = new Views.Components.TextEditorDialogWindow(this);

        if (owner != null)
        {
            _ = dialog.ShowDialog<bool>(owner).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        Value = dialog.ResultText;
                    });
                }
            }, TaskScheduler.Default);
        }
    }

    private static bool DetectIsMultiLine(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var k = key.ToLowerInvariant();
        return k.Contains("prompt") || k.Contains("template") || k.Contains("query") || k.Contains("sql") ||
               k.Contains("script") || k.Contains("instructions") || k.Contains("headers") || k.Contains("rules") ||
               k.Contains("labels") || k.Contains("candidatelabels") || k.Contains("body") || k.Contains("message");
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
