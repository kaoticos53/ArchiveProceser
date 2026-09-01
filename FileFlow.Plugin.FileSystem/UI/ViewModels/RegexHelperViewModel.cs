using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Plugin.FileSystem.UI.Services;
using FileFlow.Sdk.Renaming;

namespace FileFlow.Plugin.FileSystem.UI.ViewModels;

public sealed class RegexGroupItemViewModel
{
    public int GroupNumber { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Index { get; set; }
    public int Length { get; set; }
    public string Token => $"${GroupNumber}";
}

public sealed class RegexMatchItemViewModel
{
    public int MatchNumber { get; set; }
    public string Value { get; set; } = string.Empty;
    public int Index { get; set; }
    public int Length { get; set; }
    public List<RegexGroupItemViewModel> Groups { get; set; } = [];
}

public partial class RegexHelperViewModel : ObservableObject
{
    private readonly RegexLibraryService _libraryService;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    [ObservableProperty]
    private string _pattern = string.Empty;

    [ObservableProperty]
    private string _replacement = string.Empty;

    [ObservableProperty]
    private string _testInput = string.Empty;

    [ObservableProperty]
    private bool _ignoreCase = true;

    [ObservableProperty]
    private bool _multiline = false;

    [ObservableProperty]
    private bool _singleline = false;

    [ObservableProperty]
    private bool _ignorePatternWhitespace = false;

    [ObservableProperty]
    private bool _isValidRegex = true;

    [ObservableProperty]
    private string _regexErrorMessage = "✓ Expresión regular válida";

    [ObservableProperty]
    private string _replacementResult = string.Empty;

    [ObservableProperty]
    private int _matchCount = 0;

    public ObservableCollection<RegexMatchItemViewModel> Matches { get; } = [];
    public ObservableCollection<RegexPatternItem> BuiltInPatterns { get; } = [];
    public ObservableCollection<RegexPatternItem> UserPatterns { get; } = [];

    [ObservableProperty]
    private RegexPatternItem? _selectedLibraryItem;

    [ObservableProperty]
    private string _newPatternName = string.Empty;

    [ObservableProperty]
    private string _newPatternCategory = "General";

    [ObservableProperty]
    private string _newPatternDescription = string.Empty;

    public RegexHelperViewModel(string initialPattern = "", string initialReplacement = "", string initialTestInput = "", RegexLibraryService? libraryService = null)
    {
        _libraryService = libraryService ?? RegexLibraryService.Instance;
        _pattern = initialPattern;
        _replacement = initialReplacement;
        _testInput = string.IsNullOrWhiteSpace(initialTestInput) 
            ? "documento_v1_2024.pdf\nserie_s01e02_1080p.mkv\n[Fansub] Anime 01.mp4\nfoto-vacaciones-01-2023.jpg" 
            : initialTestInput;

        LoadLibraryPatterns();
        EvaluateRegex();
    }

    private void LoadLibraryPatterns()
    {
        BuiltInPatterns.Clear();
        foreach (var p in _libraryService.GetBuiltInPatterns())
        {
            BuiltInPatterns.Add(p);
        }

        UserPatterns.Clear();
        foreach (var p in _libraryService.GetUserPatterns())
        {
            UserPatterns.Add(p);
        }
    }

    partial void OnPatternChanged(string value) => EvaluateRegex();
    partial void OnReplacementChanged(string value) => EvaluateRegex();
    partial void OnTestInputChanged(string value) => EvaluateRegex();
    partial void OnIgnoreCaseChanged(bool value) => EvaluateRegex();
    partial void OnMultilineChanged(bool value) => EvaluateRegex();
    partial void OnSinglelineChanged(bool value) => EvaluateRegex();
    partial void OnIgnorePatternWhitespaceChanged(bool value) => EvaluateRegex();

    partial void OnSelectedLibraryItemChanged(RegexPatternItem? value)
    {
        if (value != null)
        {
            ApplyLibraryPattern(value);
        }
    }

    [RelayCommand]
    public void ApplyLibraryPattern(RegexPatternItem? item)
    {
        if (item == null) return;
        Pattern = item.Pattern;
        Replacement = item.Replacement;
        if (!string.IsNullOrWhiteSpace(item.SampleInput) && string.IsNullOrWhiteSpace(TestInput))
        {
            TestInput = item.SampleInput;
        }
    }

    [RelayCommand]
    public void SaveCurrentPattern()
    {
        if (string.IsNullOrWhiteSpace(Pattern))
        {
            MessageBox.Show("Escribe un patrón de expresión regular antes de guardar.", "FileFlow", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string name = string.IsNullOrWhiteSpace(NewPatternName) ? $"Patrón_{DateTime.Now:yyyyMMdd_HHmmss}" : NewPatternName.Trim();
        string category = string.IsNullOrWhiteSpace(NewPatternCategory) ? "General" : NewPatternCategory.Trim();

        var item = new RegexPatternItem
        {
            Name = name,
            Category = category,
            Pattern = Pattern,
            Replacement = Replacement,
            Description = NewPatternDescription.Trim(),
            SampleInput = TestInput,
            IsBuiltIn = false
        };

        _libraryService.AddUserPattern(item);
        LoadLibraryPatterns();
        NewPatternName = string.Empty;
        NewPatternDescription = string.Empty;
    }

    [RelayCommand]
    public void DeleteUserPattern(RegexPatternItem? item)
    {
        if (item == null || item.IsBuiltIn) return;
        _libraryService.DeleteUserPattern(item.Name);
        LoadLibraryPatterns();
    }

    [RelayCommand]
    public void LoadSampleInput()
    {
        TestInput = "documento_v1_2024.pdf\nserie_s01e02_1080p.mkv\n[Fansub] Anime 01.mp4\nfoto-vacaciones-01-2023.jpg";
    }

    private void EvaluateRegex()
    {
        Matches.Clear();

        if (string.IsNullOrEmpty(Pattern))
        {
            IsValidRegex = true;
            RegexErrorMessage = "Introduce una expresión regular para probarla";
            ReplacementResult = TestInput;
            MatchCount = 0;
            return;
        }

        var options = RegexOptions.None;
        if (IgnoreCase) options |= RegexOptions.IgnoreCase;
        if (Multiline) options |= RegexOptions.Multiline;
        if (Singleline) options |= RegexOptions.Singleline;
        if (IgnorePatternWhitespace) options |= RegexOptions.IgnorePatternWhitespace;

        try
        {
            var regex = new Regex(Pattern, options, RegexTimeout);
            IsValidRegex = true;
            RegexErrorMessage = "✓ Expresión regular sintácticamente correcta";

            var matchCollection = regex.Matches(TestInput);
            MatchCount = matchCollection.Count;

            int matchIndex = 1;
            foreach (Match m in matchCollection)
            {
                var matchVm = new RegexMatchItemViewModel
                {
                    MatchNumber = matchIndex++,
                    Value = m.Value,
                    Index = m.Index,
                    Length = m.Length
                };

                for (int i = 0; i < m.Groups.Count; i++)
                {
                    var g = m.Groups[i];
                    matchVm.Groups.Add(new RegexGroupItemViewModel
                    {
                        GroupNumber = i,
                        GroupName = regex.GroupNameFromNumber(i),
                        Value = g.Value,
                        Index = g.Index,
                        Length = g.Length
                    });
                }

                Matches.Add(matchVm);
            }

            ReplacementResult = regex.Replace(TestInput, Replacement ?? string.Empty);
        }
        catch (ArgumentException ex)
        {
            IsValidRegex = false;
            RegexErrorMessage = $"⚠ Error en la sintaxis de la expresión regular: {ex.Message}";
            ReplacementResult = TestInput;
            MatchCount = 0;
        }
        catch (RegexMatchTimeoutException)
        {
            IsValidRegex = false;
            RegexErrorMessage = "⚠ Timeout: la expresión regular tardó demasiado tiempo en evaluarse (posible backtracking catastrófico)";
            ReplacementResult = TestInput;
            MatchCount = 0;
        }
    }
}
