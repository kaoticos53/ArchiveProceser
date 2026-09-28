using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.App.ViewModels;

public partial class NodeParameterViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly EventHandler<CultureInfo> _languageChangedHandler;
    private readonly ILocalizationService _loc;
    private readonly IDialogService _dialogService;
    private readonly IWindowService _windows;
    private readonly IFileDialogService _files;
    private readonly IPopupMenuService _menus;
    private readonly IUiDispatcher _uiDispatcher;

    /// <summary>Reloj del que cuelga la duración del aviso de «copiado» (ver <see cref="CopyFeedbackDuration"/>).</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Generación del aviso de copiado en curso: un clic nuevo abre una y descarta el vencimiento anterior.</summary>
    private int _copyFeedbackGeneration;

    /// <summary>
    /// Cuánto se queda encendido el aviso de «copiado». Es una duración con <b>semántica</b> —lo justo para
    /// confirmar el gesto sin que el aviso se quede pegado—, así que los tests la usan en lugar de repetir el
    /// número: cambiar la duración no debe dejar una aserción mintiendo en nombre de otro valor.
    /// </summary>
    public static readonly TimeSpan CopyFeedbackDuration = TimeSpan.FromMilliseconds(1500);

    private FileItemContext? _activeEvaluationContext;
    private string? _sourceRootPath;

    public NodeViewModel? NodeOwner { get; set; }

    /// <summary>Editor que el host ancla al construir el VM; sustituye al sondeo de Application.Current.</summary>
    public EditorViewModel? HostEditor { get; set; }
    public NodeParameterDescriptor? Descriptor { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _key = string.Empty;

    public string DisplayName => _loc.GetString($"Param_{Key}", GetDefaultDisplayName(Key));

    /// <summary>
    /// Aclaración del parámetro: lo que no cabe en su nombre —qué significa dejarlo vacío, qué formato espera—.
    /// Se resuelve del recurso <c>Param_{clave}_Help</c>, la convención con la que los plugins escriben la ayuda
    /// (Archives, FileSystem, Network y AI). Sin recurso cae en la clave, que es lo que la ficha del parámetro
    /// mostraba antes de esto: la ficha gana la aclaración donde la hay y no pierde nada donde no.
    ///
    /// <para>Deliberadamente <b>no</b> se pinta <c>NodeParameterDescriptor.HelpText</c>: son casi un centenar de
    /// textos literales sin localizar, así que mostrarlos pondría castellano en la interfaz inglesa. La ayuda
    /// visible vive en los recursos, en los dos idiomas (hito 208).</para>
    /// </summary>
    public string Help => _loc.GetString($"Param_{Key}_Help", Key);

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

    /// <summary>
    /// Abre el GESTOR DE PRESETS del nodo por el camino que este host pueda cumplir.
    ///
    /// <para><b>Por qué hay dos caminos.</b> El botón «🎬» de la fila del preset y el de la tarjeta del nodo
    /// llaman a la MISMA acción (<c>ManageMediaPresets</c>), y esa acción, en el host que tiene el toolkit del
    /// plugin, monta la ventana del plugin. Un host que no lo tiene no puede montarla: por eso el nodo declara
    /// la superficie al SDK (<see cref="INodeDialogSurfaceProvider"/>, con su clave del catálogo y su view model
    /// portable) y dice qué acción sustituye. Cuando la declara, la sirve el servicio de ventanas del host
    /// —el ÚNICO que sabe pintar en este host— sobre ese mismo view model; cuando no, se cae al camino del
    /// toolkit, que es el del escritorio. La lógica del gestor no se duplica: cambia quién la pinta.</para>
    ///
    /// <para>La vuelta también importa: al cerrarse la superficie se resincronizan los parámetros del nodo,
    /// porque el catálogo de presets pudo cambiar y la fila tiene que enseñar el catálogo nuevo.</para>
    /// </summary>
    [RelayCommand]
    public async Task OpenMediaPresetManagerAsync()
    {
        const string ActionId = "ManageMediaPresets";

        try
        {
            // Los avisos del contenido salen por el servicio de diálogos de ESTE host, no por el nulo: el nodo
            // no puede resolverlo (no conoce la UI del host) y se lo pasa quien abre.
            var context = new NodeCustomActionContext(
                _windows.MainWindowOwner,
                () => NodeOwner?.SyncParametersFromNodeInstance(),
                _dialogService);

            if (NodeOwner?.NodeInstance is INodeDialogSurfaceProvider surface
                && surface.ReplacesCustomActionId is { } replaced
                && string.Equals(replaced, ActionId, StringComparison.OrdinalIgnoreCase))
            {
                await _windows.ShowDialogAsync(surface.DialogKey, surface.CreateDialogPayload(context));
                NodeOwner?.SyncParametersFromNodeInstance();
                return;
            }

            if (NodeOwner?.NodeInstance is INodeCustomActionProvider provider)
            {
                provider.ExecuteCustomAction(ActionId, context);
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
                // El gestor de contraseñas es una VENTANA DEL ESCRITORIO: en un host sin su toolkit el nodo no la
                // monta y lo DECLARA por los diálogos de quien lo abrió (hito 268). El contexto tiene que llevar
                // el servicio del host —el mismo `_dialogService` de esta fila— o la declaración cae al nulo
                // declarado: el botón «🔑» no abre nada y no avisa (el defecto del hito 270).
                provider.ExecuteCustomAction("ManagePasswords", new NodeCustomActionContext(
                    _windows.MainWindowOwner,
                    () => NodeOwner?.SyncParametersFromNodeInstance(),
                    _dialogService));
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

    /// <summary>
    /// Añade el valor actual a las opciones si no está entre ellas, para que el desplegable pueda mostrarlo.
    ///
    /// Es la mitad de la regla que ya aplica <see cref="UpdateOptions"/>: allí el valor se inserta cuando
    /// cambian las opciones, aquí cuando cambia el valor. Sin la segunda, cualquier escritura posterior (cargar
    /// un flujo, pegar un nodo, deshacer) podía dejar el parámetro fuera de la lista y el campo en blanco.
    /// Los desplegables editables quedan fuera: en ellos el valor es texto libre y se muestra en su caja.
    /// </summary>
    private void EnsureValueIsSelectable()
    {
        string value = Value?.ToString()?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(value) ||
            Options.Any(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Options.Insert(0, value);
        OnPropertyChanged(nameof(HasOptions));
        OnPropertyChanged(nameof(IsDropdown));
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

        // Un desplegable atado por valor no pinta nada si el valor no está entre sus opciones —y devuelve
        // 'null' al view model—: un valor heredado (un flujo guardado con otras opciones, un nodo pegado, una
        // opción que ya no existe) dejaba el campo en blanco y se perdía sin que el usuario pudiera verlo.
        // El valor actual se añade a la lista, igual que hace 'UpdateOptions' cuando el nodo define opciones.
        if (HasOptions && !IsEditableDropdown)
        {
            EnsureValueIsSelectable();
        }

        if (IsFileVersionSelector)
        {
            UpdateVersionOptionsSelection();
            OnPropertyChanged(nameof(SelectedVersionOption));
        }

        if (!Equals(oldValue, newValue))
        {
            var editor = NodeOwner?.ParentEditor;
            if (editor != null && !editor.UndoRedoService.IsExecuting)
            {
                editor.UndoRedoService.Record(new FileFlow.App.Services.UndoRedo.ChangeParameterAction(this, oldValue, newValue));
            }
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

    /// <summary>
    /// Copia el valor evaluado al portapapeles y enciende el aviso de confirmación durante
    /// <see cref="CopyFeedbackDuration"/>.
    ///
    /// <para>Dos cosas importan aquí y las dos tienen red: si el portapapeles falla no se anuncia una copia que
    /// no ocurrió, y el <b>vencimiento</b> del aviso —que es lo que apaga el icono de confirmación— se mide con el
    /// reloj inyectado, no con una espera real: con el reloj del sistema probarlo costaría la espera entera por
    /// caso, así que no se probaba.</para>
    /// </summary>
    [RelayCommand]
    public async Task CopyEvaluatedValueAsync()
    {
        if (string.IsNullOrEmpty(EvaluatedValue)) return;

        try
        {
            LogViewModel.SafeSetClipboardText(EvaluatedValue);
        }
        catch
        {
            // Excepciones de concurrencia del portapapeles: sin copia no hay nada que confirmar.
            return;
        }

        // Un clic nuevo reabre la ventana del aviso; la generación descarta el vencimiento del clic anterior,
        // que si no apagaría el aviso del segundo a mitad de camino.
        int generation = ++_copyFeedbackGeneration;
        IsCopied = true;

        try
        {
            await Task.Delay(CopyFeedbackDuration, _timeProvider).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Un reloj que no puede programar no puede dejar el aviso encendido para siempre.
            System.Diagnostics.Debug.WriteLine($"[NodeParameterViewModel] No se pudo programar el fin del aviso de copiado: {ex.Message}");

            if (generation == _copyFeedbackGeneration)
            {
                IsCopied = false;
            }

            return;
        }

        if (generation == _copyFeedbackGeneration)
        {
            IsCopied = false;
        }
    }

    public NodeParameterViewModel(NodeParameterDescriptor descriptor, object? value, NodeViewModel? nodeOwner = null, ILocalizationService? localizationService = null, IDialogService? dialogService = null, TimeProvider? timeProvider = null,
        IWindowService? windowService = null, IFileDialogService? fileDialogService = null, IPopupMenuService? popupMenuService = null)
        : this(descriptor.Key, value, descriptor.Options, nodeOwner, localizationService, dialogService, timeProvider, windowService, fileDialogService, popupMenuService)
    {
        Descriptor = descriptor;
    }

    public NodeParameterViewModel(string key, object? value, IEnumerable<string>? options = null, NodeViewModel? nodeOwner = null, ILocalizationService? localizationService = null, IDialogService? dialogService = null, TimeProvider? timeProvider = null,
        IWindowService? windowService = null, IFileDialogService? fileDialogService = null, IPopupMenuService? popupMenuService = null)
    {
        _loc = localizationService ?? LocalizationManager.Instance;
        // Los diálogos de ESTA fila salen por el servicio del HOST, resuelto igual que en la puerta de la
        // tarjeta del nodo (`CoreDialogHost`). Si aquí cayera el Nulo, la misma orden destructiva pediría la
        // confirmación a un servicio que responde «sí» sin preguntar y la puerta de la fila borraría en
        // silencio mientras la de la tarjeta no borra: dos comportamientos para una sola regla.
        _dialogService = dialogService ?? FileFlow.App.Core.CoreDialogHost.ResolveDialogService();
        _windows = windowService ?? Services.ServiceHolders.WindowService;
        _files = fileDialogService ?? Services.ServiceHolders.FileDialog;
        _menus = popupMenuService ?? Services.ServiceHolders.PopupMenu;
        _uiDispatcher = FileFlow.App.Core.HostUi.Dispatcher;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
            OnPropertyChanged(nameof(Help));
        };
        _loc.LanguageChanged += _languageChangedHandler;
    }

    private EditorViewModel? ResolveEditor()
    {
        if (NodeOwner?.ParentEditor != null)
        {
            return NodeOwner.ParentEditor;
        }
        return HostEditor;
    }

    [RelayCommand]
    public void OpenVariablePicker(object? targetObject)
    {
        var editor = ResolveEditor();

        if (editor != null)
        {
            RefreshAvailableVariables(editor);
        }
        else if (AvailableVariables.Count == 0)
        {
            AvailableVariables = Services.VariableDiscoveryService.Instance.GetAvailableVariables(NodeOwner, []);
        }

        // El menú es puro dato: el host lo pinta con sus controles nativos (PopupMenuService del host).
        var items = new List<PopupMenuItem>
        {
            new()
            {
                Header = _loc.GetString("VarPicker_OpenFullCatalog", "🔍 Abrir Catálogo Completo de Variables..."),
                IsBold = true,
                Command = OpenVariableCatalogCommand,
                CommandParameter = null,
            },
        };

        foreach (var group in AvailableVariables)
        {
            if (group.Variables.Count == 0) continue;

            items.Add(new PopupMenuItem
            {
                Header = $"{group.GroupName} ({group.Variables.Count})",
                IsBold = group.IsUpstream,
                Items = group.Variables
                    .Select(v => new PopupMenuItem
                    {
                        Header = $"{v.Token}  —  {v.Description}",
                        Command = InsertVariableTokenCommand,
                        CommandParameter = v.Token,
                        ToolTip = string.IsNullOrEmpty(v.SampleValue) ? null : $"Ejemplo: {v.SampleValue}",
                    })
                    .ToList(),
            });
        }

        _menus.ShowMenu(new PopupMenuDescriptor { Items = items }, targetObject);
    }

    [RelayCommand]
    public void OpenVariableCatalog(object? targetObject)
    {
        var editor = ResolveEditor();

        if (editor != null)
        {
            RefreshAvailableVariables(editor);
        }
        else if (AvailableVariables.Count == 0)
        {
            AvailableVariables = Services.VariableDiscoveryService.Instance.GetAvailableVariables(NodeOwner, []);
        }

        var previewContext = (editor?.VariableDiscoveryService ?? Services.VariableDiscoveryService.Instance).CreatePreviewItem(NodeOwner);
        _ = _windows.ShowDialogAsync(DialogKeys.VariablePicker, new VariablePickerRequest(AvailableVariables, NodeOwner, previewContext, _loc))
            .ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result is { Confirmed: true, Value: { } token })
                {
                    _uiDispatcher.Post(() => InsertVariableToken(token));
                }
            }, TaskScheduler.Default);
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
    public void BrowsePath()
    {
        var title = $"Seleccionar {(IsFolderPath ? "directorio" : "archivo")} para '{Key}'";
        var picked = IsFolderPath
            ? _files.ShowFolderBrowserDialog(title)
            : _files.ShowOpenFileDialog(title, "Todos los archivos|*.*");
        if (!string.IsNullOrEmpty(picked))
        {
            Value = picked;
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
        _ = _windows.ShowDialogAsync(DialogKeys.TextEditor, this)
            .ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result is { Confirmed: true, Value: { } text })
                {
                    _uiDispatcher.Post(() => Value = text);
                }
            }, TaskScheduler.Default);
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
