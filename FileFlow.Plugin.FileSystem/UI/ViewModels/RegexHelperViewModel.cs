using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Plugin.FileSystem.UI.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;
using FileFlow.Sdk.TemplateEngine;

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
    private string _regexErrorMessage = string.Empty;

    [ObservableProperty]
    private int _matchCount = 0;

    [ObservableProperty]
    private string _replacementResult = string.Empty;

    [ObservableProperty]
    private RegexPatternItem? _selectedLibraryItem;

    [ObservableProperty]
    private string _newPatternName = string.Empty;

    [ObservableProperty]
    private string _newPatternCategory = "Personalizados";

    [ObservableProperty]
    private string _newPatternDescription = string.Empty;

    public ObservableCollection<RegexMatchItemViewModel> Matches { get; } = [];
    public ObservableCollection<RegexPatternItem> BuiltInPatterns { get; } = [];
    public ObservableCollection<RegexPatternItem> UserPatterns { get; } = [];
    public ObservableCollection<string> Categories { get; } = [];

    public RegexHelperViewModel(string initialPattern = "", string initialReplacement = "", string initialTestInput = "", RegexLibraryService? libraryService = null)
    {
        _libraryService = libraryService ?? RegexLibraryService.Instance;

        Pattern = initialPattern;
        Replacement = initialReplacement;
        TestInput = string.IsNullOrWhiteSpace(initialTestInput) ? GetDefaultSampleInput() : initialTestInput;

        LoadLibrary();
        EvaluateRegex();
    }

    partial void OnPatternChanged(string value) => EvaluateRegex();
    partial void OnReplacementChanged(string value) => EvaluateRegex();
    partial void OnTestInputChanged(string value) => EvaluateRegex();
    partial void OnIgnoreCaseChanged(bool value) => EvaluateRegex();
    partial void OnMultilineChanged(bool value) => EvaluateRegex();
    partial void OnSinglelineChanged(bool value) => EvaluateRegex();
    partial void OnIgnorePatternWhitespaceChanged(bool value) => EvaluateRegex();

    public void EvaluateRegex()
    {
        Matches.Clear();
        MatchCount = 0;
        ReplacementResult = string.Empty;

        if (string.IsNullOrEmpty(Pattern))
        {
            IsValidRegex = true;
            RegexErrorMessage = "Introduce una expresión regular para comenzar a probar.";
            ReplacementResult = TestInput;
            return;
        }

        try
        {
            var options = RegexOptions.None;
            if (IgnoreCase) options |= RegexOptions.IgnoreCase;
            if (Multiline) options |= RegexOptions.Multiline;
            if (Singleline) options |= RegexOptions.Singleline;
            if (IgnorePatternWhitespace) options |= RegexOptions.IgnorePatternWhitespace;

            var regex = new Regex(Pattern, options, RegexTimeout);
            IsValidRegex = true;
            RegexErrorMessage = "✓ Expresión regular válida y compilada correctamente.";

            if (string.IsNullOrEmpty(TestInput))
            {
                return;
            }

            var matchCollection = regex.Matches(TestInput);
            MatchCount = matchCollection.Count;

            int matchIndex = 1;
            foreach (Match match in matchCollection)
            {
                var matchVm = new RegexMatchItemViewModel
                {
                    MatchNumber = matchIndex++,
                    Value = match.Value,
                    Index = match.Index,
                    Length = match.Length
                };

                for (int i = 1; i < match.Groups.Count; i++)
                {
                    var grp = match.Groups[i];
                    string groupName = regex.GroupNameFromNumber(i);
                    matchVm.Groups.Add(new RegexGroupItemViewModel
                    {
                        GroupNumber = i,
                        GroupName = groupName,
                        Value = grp.Value,
                        Index = grp.Index,
                        Length = grp.Length
                    });
                }

                Matches.Add(matchVm);
            }

            if (!string.IsNullOrEmpty(Replacement))
            {
                var sampleContext = new FileItemContext(TestInput);
                ReplacementResult = VariableTemplateResolver.ApplyRegexReplacement(regex, TestInput, Replacement, sampleContext, replaceAll: true);
            }
            else
            {
                ReplacementResult = regex.Replace(TestInput, string.Empty);
            }
        }
        catch (ArgumentException ex)
        {
            IsValidRegex = false;
            RegexErrorMessage = $"Error sintáctico: {ex.Message}";
            ReplacementResult = TestInput;
        }
        catch (RegexMatchTimeoutException)
        {
            IsValidRegex = false;
            RegexErrorMessage = "Error: La evaluación superó el tiempo límite (1 segundo).";
            ReplacementResult = TestInput;
        }
    }

    [RelayCommand]
    public void LoadSampleInput()
    {
        TestInput = GetDefaultSampleInput();
    }

    [RelayCommand]
    public void ApplyLibraryPattern(RegexPatternItem item)
    {
        if (item == null) return;
        Pattern = item.Pattern;
        if (!string.IsNullOrEmpty(item.Replacement))
        {
            Replacement = item.Replacement;
        }
        if (!string.IsNullOrEmpty(item.SampleInput))
        {
            TestInput = item.SampleInput;
        }
    }

    [RelayCommand]
    public void SaveCurrentPattern()
    {
        if (string.IsNullOrWhiteSpace(Pattern)) return;

        string name = string.IsNullOrWhiteSpace(NewPatternName) ? $"Patrón {DateTime.Now:yyyy-MM-dd HH:mm}" : NewPatternName.Trim();
        string category = string.IsNullOrWhiteSpace(NewPatternCategory) ? "Personalizados" : NewPatternCategory.Trim();

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

        _libraryService.SaveUserPattern(item);
        LoadLibrary();

        NewPatternName = string.Empty;
        NewPatternDescription = string.Empty;
    }

    [RelayCommand]
    public void DeleteUserPattern(RegexPatternItem item)
    {
        if (item == null || item.IsBuiltIn) return;
        _libraryService.DeleteUserPattern(item.Id);
        LoadLibrary();
    }

    [RelayCommand]
    public void ApplyAndClose(Window window)
    {
        window.DialogResult = true;
        window.Close();
    }

    private void LoadLibrary()
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

        Categories.Clear();
        var allCats = BuiltInPatterns.Concat(UserPatterns).Select(p => p.Category).Distinct();
        foreach (var cat in allCats)
        {
            Categories.Add(cat);
        }
    }

    private static string GetDefaultSampleInput()
    {
        return string.Join(Environment.NewLine, [
            "serie_guapa_1x02_hdtv.mov",
            "Breaking.Bad.S01E02.Pilot.1080p.mkv",
            "1 - pepe.jpg",
            "10 - kilo.jpg",
            "informe_2026_09_01_borrador_v1.docx",
            "Cancion Fabulosa (Official Video) [Audio 5.1].mp3",
            "Factura #998! @ClienteAlfa?.pdf"
        ]);
    }
}
