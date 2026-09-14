using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.App.ViewModels;

/// <summary>
/// ViewModel desacoplado para el diálogo de edición enriquecida de texto, plantillas dinámicas e IntelliSense.
/// </summary>
public partial class TextEditorDialogViewModel : ObservableObject
{
    private readonly NodeParameterViewModel? _parameter;
    private readonly List<VariableItem> _allVariables = [];
    private readonly IVariableDiscoveryService _variableDiscoveryService;
    private readonly ILocalizationService _loc;

    [ObservableProperty]
    private string _title = "Editor de Texto / Prompt";

    [ObservableProperty]
    private string _subtitle = "Edición de texto libre con soporte para variables";

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _charCountText = "🔤 0 car.";

    [ObservableProperty]
    private string _wordCountText = "📝 0 pal.";

    [ObservableProperty]
    private string _lineCountText = "📄 1 lín.";

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    private bool _isPreviewVisible;

    [ObservableProperty]
    private bool _isSidePanelVisible;

    [ObservableProperty]
    private string _sideSearchText = string.Empty;

    [ObservableProperty]
    private bool _hasVersionOptions;

    [ObservableProperty]
    private bool _isIntelliSenseOpen;

    [ObservableProperty]
    private VariableItem? _selectedIntelliSenseCandidate;

    [ObservableProperty]
    private string _resultText = string.Empty;

    public ObservableCollection<VariableItem> FilteredSideVariables { get; } = [];
    public ObservableCollection<VariableItem> IntelliSenseCandidates { get; } = [];
    public ObservableCollection<FileVersionOption> AvailableVersionOptions { get; } = [];
    public IReadOnlyList<VariableItem> AllVariables => _allVariables;
    public IReadOnlyList<VariableGroupItem> VariableGroups { get; private set; } = [];

    public TextEditorDialogViewModel(
        string? title = null,
        string? initialText = null,
        NodeParameterViewModel? parameter = null,
        IVariableDiscoveryService? variableDiscoveryService = null,
        ILocalizationService? localizationService = null)
    {
        _parameter = parameter;
        _variableDiscoveryService = variableDiscoveryService ?? VariableDiscoveryService.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;

        Title = string.IsNullOrWhiteSpace(title)
            ? _loc.GetString("TextEditor_Title", "Editor de Texto / Prompt")
            : title;

        if (_parameter != null)
        {
            string nodeTitle = _parameter.NodeOwner?.Title ?? "Personalizado";
            Subtitle = $"Parámetro: {_parameter.Key}  •  Nodo: {nodeTitle}";
        }
        else
        {
            Subtitle = _loc.GetString("TextEditor_Subtitle", "Edición de texto libre con soporte para variables");
        }

        Text = initialText ?? string.Empty;

        LoadVariables();
        LoadVersionChips();
        UpdateStats();
        UpdateLivePreview();
    }

    partial void OnTextChanged(string value)
    {
        UpdateStats();
        UpdateLivePreview();
    }

    partial void OnSideSearchTextChanged(string value)
    {
        FilterSideVariables();
    }

    private void LoadVersionChips()
    {
        if (_parameter == null) return;

        if (_parameter.IsFileVersionSelector)
        {
            _parameter.RefreshAvailableVersions();
        }

        var versions = _parameter.AvailableVersionOptions;
        if (versions != null && versions.Count > 0)
        {
            AvailableVersionOptions.Clear();
            foreach (var opt in versions)
            {
                AvailableVersionOptions.Add(opt);
            }
            HasVersionOptions = true;
        }
    }

    private void LoadVariables()
    {
        var groups = _parameter?.AvailableVariables;
        if ((groups == null || groups.Count == 0) && _parameter?.NodeOwner != null)
        {
            if (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm)
            {
                _parameter.RefreshAvailableVariables(mainVm.Editor);
                groups = _parameter.AvailableVariables;
            }
        }

        if (groups == null || groups.Count == 0)
        {
            if (_parameter?.NodeOwner != null)
            {
                var editor = (Application.Current?.MainWindow?.DataContext as MainViewModel)?.Editor;
                if (editor != null)
                {
                    groups = _variableDiscoveryService.GetAvailableVariables(_parameter.NodeOwner, editor.Connections);
                }
            }
        }

        VariableGroups = groups ?? [];

        var seenTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (groups != null)
        {
            foreach (var g in groups)
            {
                foreach (var v in g.Variables)
                {
                    if (seenTokens.Add(v.Token))
                    {
                        _allVariables.Add(v);
                    }
                }
            }
        }

        if (_allVariables.Count == 0)
        {
            _allVariables.AddRange(
            [
                new("GlobalOutputDir", "{GlobalOutputDir}", "Carpeta de salida global", "Sistema", "C:\\Output"),
                new("FileName", "{FileName}", "Nombre del archivo", "Sistema", "documento.pdf"),
                new("FileNameWithoutExtension", "{FileNameWithoutExtension}", "Nombre sin extensión", "Sistema", "documento"),
                new("Extension", "{Extension}", "Extensión del archivo", "Sistema", ".pdf"),
                new("Date", "{Date}", "Fecha actual (yyyyMMdd)", "Fechas", DateTime.Now.ToString("yyyyMMdd")),
                new("Year", "{Year}", "Año actual (yyyy)", "Fechas", DateTime.Now.ToString("yyyy")),
                new("Month", "{Month}", "Mes actual (MM)", "Fechas", DateTime.Now.ToString("MM")),
                new("Day", "{Day}", "Día actual (dd)", "Fechas", DateTime.Now.ToString("dd")),
                new("Guid", "{Guid}", "Identificador GUID único", "Sistema", Guid.NewGuid().ToString("N")),
                new("TempDir", "{TempDir}", "Directorio temporal de trabajo", "Sistema", "C:\\Temp\\Flow_a1b2c3d4"),
                new("RandomId", "{RandomId}", "Identificador aleatorio seguro", "Sistema", "x8f2q1"),
                new("Random", "{Random:4}", "Dígitos aleatorios", "Funciones", "4829")
            ]);
        }

        FilterSideVariables();
    }

    public void FilterSideVariables()
    {
        string query = SideSearchText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(query)
            ? _allVariables
            : _allVariables.Where(v =>
                v.Token.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                v.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                v.Category.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        FilteredSideVariables.Clear();
        foreach (var item in filtered)
        {
            FilteredSideVariables.Add(item);
        }
    }

    [RelayCommand]
    public void ToggleSidePanel()
    {
        IsSidePanelVisible = !IsSidePanelVisible;
    }

    [RelayCommand]
    public void CloseSidePanel()
    {
        IsSidePanelVisible = false;
    }

    [RelayCommand]
    public void CopyText()
    {
        if (!string.IsNullOrEmpty(Text))
        {
            LogViewModel.SafeSetClipboardText(Text);
        }
    }

    [RelayCommand]
    public void ClearText()
    {
        Text = string.Empty;
    }

    public void UpdateStats(int customLineCount = 0)
    {
        string current = Text ?? string.Empty;
        int chars = current.Length;
        int words = string.IsNullOrWhiteSpace(current) ? 0 : current.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        int lines = customLineCount > 0 ? customLineCount : (string.IsNullOrWhiteSpace(current) ? 1 : current.Split('\n').Length);

        CharCountText = $"🔤 {chars} car.";
        WordCountText = $"📝 {words} pal.";
        LineCountText = $"📄 {lines} lín.";
    }

    public void UpdateLivePreview()
    {
        string current = Text ?? string.Empty;
        bool hasVars = (current.Contains('{') && current.Contains('}')) || (current.Contains('<') && current.Contains('>'));

        if (!hasVars || string.IsNullOrWhiteSpace(current))
        {
            IsPreviewVisible = false;
            PreviewText = string.Empty;
            return;
        }

        try
        {
            var previewContext = _variableDiscoveryService.CreatePreviewItem(_parameter?.NodeOwner);
            PreviewText = VariableTemplateResolver.Resolve(current, previewContext);
            IsPreviewVisible = true;
        }
        catch
        {
            IsPreviewVisible = false;
            PreviewText = string.Empty;
        }
    }

    /// <summary>
    /// Evalúa si el cursor se encuentra en una secuencia de autocompletado {query y actualiza la lista de candidatos.
    /// Retorna la posición de la llave abierta o -1 si no debe mostrarse.
    /// </summary>
    public int EvaluateIntelliSense(int caretIndex)
    {
        string current = Text ?? string.Empty;

        if (caretIndex > 0 && caretIndex <= current.Length)
        {
            int lastOpenBrace = current.LastIndexOf('{', caretIndex - 1);
            if (lastOpenBrace >= 0)
            {
                int closeBrace = current.IndexOf('}', lastOpenBrace);
                if (closeBrace < 0 || closeBrace >= caretIndex)
                {
                    string query = current.Substring(lastOpenBrace + 1, caretIndex - (lastOpenBrace + 1));
                    if (query.Contains('\n') || query.Contains('\r'))
                    {
                        IsIntelliSenseOpen = false;
                        return -1;
                    }

                    var matches = _allVariables.Where(v =>
                    {
                        string tokenInner = v.Token.Trim('{', '}');
                        return tokenInner.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               v.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
                    }).Take(12).ToList();

                    if (matches.Count > 0)
                    {
                        IntelliSenseCandidates.Clear();
                        foreach (var m in matches)
                        {
                            IntelliSenseCandidates.Add(m);
                        }
                        SelectedIntelliSenseCandidate = IntelliSenseCandidates[0];
                        IsIntelliSenseOpen = true;
                        return lastOpenBrace;
                    }
                }
            }
        }

        IsIntelliSenseOpen = false;
        return -1;
    }

    /// <summary>
    /// Aplica la selección de IntelliSense reemplazando la llave abierta y query por el token completo.
    /// </summary>
    public (string NewText, int NewCaretIndex) ApplyIntelliSenseSelection(int caretIndex, VariableItem? candidate = null)
    {
        var selected = candidate ?? SelectedIntelliSenseCandidate;
        if (selected == null) return (Text, caretIndex);

        string current = Text ?? string.Empty;
        int openBrace = (caretIndex > 0 && caretIndex <= current.Length) ? current.LastIndexOf('{', caretIndex - 1) : -1;

        if (openBrace >= 0)
        {
            int replaceLen = caretIndex - openBrace;
            if (caretIndex < current.Length && current[caretIndex] == '}')
            {
                replaceLen++;
            }
            string newText = current.Remove(openBrace, replaceLen).Insert(openBrace, selected.Token);
            Text = newText;
            IsIntelliSenseOpen = false;
            return (newText, openBrace + selected.Token.Length);
        }

        return InsertTokenAt(caretIndex, selected.Token);
    }

    /// <summary>
    /// Inserta un token en la posición indicada del cursor.
    /// </summary>
    public (string NewText, int NewCaretIndex) InsertTokenAt(int caretIndex, string token)
    {
        if (string.IsNullOrEmpty(token)) return (Text, caretIndex);

        string current = Text ?? string.Empty;
        if (caretIndex < 0 || caretIndex > current.Length) caretIndex = current.Length;

        string newText = current.Insert(caretIndex, token);
        Text = newText;
        IsIntelliSenseOpen = false;
        return (newText, caretIndex + token.Length);
    }

    /// <summary>
    /// Inserta o reemplaza una versión de archivo según la configuración del parámetro.
    /// </summary>
    public (string NewText, int NewCaretIndex) SelectVersionToken(int caretIndex, string token)
    {
        if (_parameter?.IsFileVersionSelector == true && (string.IsNullOrWhiteSpace(Text) || (Text.StartsWith("{") && Text.EndsWith("}"))))
        {
            Text = token;
            return (token, token.Length);
        }

        return InsertTokenAt(caretIndex, token);
    }

    public void SaveResult()
    {
        ResultText = Text ?? string.Empty;
    }
}
