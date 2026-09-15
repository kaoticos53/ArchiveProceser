using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using FileFlow.Plugin.Scripting.UI.ViewModels;
using FileFlow.Sdk.Services;

namespace FileFlow.Plugin.Scripting.UI.Views;

public partial class ScriptStudioWindow : Window
{
    private readonly ScriptStudioViewModel _viewModel;
    private readonly IDialogService _dialogService;
    private bool _isUpdatingTextFromCode;

    private TextEditor? CodeEditor => this.FindControl<TextEditor>("CodeEditor");

    public string SelectedLanguage => _viewModel.SelectedLanguage;
    public string ScriptCode => _viewModel.ScriptCode;
    public string InputPortsString => string.Join(", ", _viewModel.InputPorts);
    public string OutputPortsString => string.Join(", ", _viewModel.OutputPorts);

    public ScriptStudioWindow(string initialLanguage, string initialCode, string initialInputs, string initialOutputs, IDialogService? dialogService = null)
    {
        _dialogService = dialogService ?? NullDialogService.Instance;

        _viewModel = new ScriptStudioViewModel(initialLanguage, initialCode, initialInputs, initialOutputs);
        DataContext = _viewModel;

        if (CodeEditor != null)
        {
            UpdateSyntaxHighlighting(_viewModel.SelectedLanguage);
            _isUpdatingTextFromCode = true;
            CodeEditor.Text = _viewModel.ScriptCode ?? string.Empty;
            _isUpdatingTextFromCode = false;
            CodeEditor.TextChanged += CodeEditor_TextChanged;
        }

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ScriptStudioViewModel.ScriptCode) && !_isUpdatingTextFromCode)
            {
                _isUpdatingTextFromCode = true;
                if (CodeEditor != null)
                {
                    CodeEditor.Text = _viewModel.ScriptCode ?? string.Empty;
                }
                _isUpdatingTextFromCode = false;
            }
            else if (e.PropertyName == nameof(ScriptStudioViewModel.SelectedLanguage))
            {
                UpdateSyntaxHighlighting(_viewModel.SelectedLanguage);
            }
        };
    }

    private void UpdateSyntaxHighlighting(string language)
    {
        if (CodeEditor == null) return;
        string syntaxName = language.Equals("JavaScript", StringComparison.OrdinalIgnoreCase) ? "JavaScript" : "C#";
        CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(syntaxName);
    }

    private void Language_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CodeEditor != null && _viewModel != null)
        {
            UpdateSyntaxHighlighting(_viewModel.SelectedLanguage);
        }
    }

    private void CodeEditor_TextChanged(object? sender, EventArgs e)
    {
        if (!_isUpdatingTextFromCode && _viewModel != null && CodeEditor != null)
        {
            _isUpdatingTextFromCode = true;
            _viewModel.ScriptCode = CodeEditor.Text;
            _isUpdatingTextFromCode = false;
        }
    }

    private void ApplyAndClose_Click(object? sender, RoutedEventArgs e)
    {
        if (CodeEditor != null)
        {
            _viewModel.ScriptCode = CodeEditor.Text;
        }
        Close(true);
    }

    private void OpenManual_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            bool isEnglish = FileFlow.Sdk.Localization.LocalizationManager.Instance.CurrentLanguage.Equals("en", StringComparison.OrdinalIgnoreCase);

            string[] candidates = isEnglish
                ? [
                    Path.Combine(baseDir, "Docs", "scripting_node_manual.pdf"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "docs", "scripting_node_manual.pdf"),
                    Path.Combine(baseDir, "Docs", "manual_nodo_scripting.pdf"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "docs", "manual_nodo_scripting.pdf"),
                    Path.Combine(baseDir, "docs", "scripting_node_manual.md"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "docs", "scripting_node_manual.md")
                ]
                : [
                    Path.Combine(baseDir, "Docs", "manual_nodo_scripting.pdf"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "docs", "manual_nodo_scripting.pdf"),
                    Path.Combine(baseDir, "Docs", "scripting_node_manual.pdf"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "docs", "scripting_node_manual.pdf"),
                    Path.Combine(baseDir, "manual_nodo_scripting.pdf"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "docs", "manual_nodo_scripting.md")
                ];

            string? found = candidates.FirstOrDefault(File.Exists);
            if (found != null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(found) { UseShellExecute = true });
            }
            else
            {
                string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("ScriptStudio_ManualTitle", "Manual de Scripting");
                string notFoundMsg = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString(
                    "ScriptStudio_ManualNotFound",
                    "El archivo del manual no se encuentra en la ruta esperada. Puedes consultarlo en la carpeta 'docs/' del proyecto.");
                _dialogService.ShowInformation(notFoundMsg, title);
            }
        }
        catch (Exception ex)
        {
            string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("ScriptStudio_ManualTitle", "Manual de Scripting");
            _dialogService.ShowWarning($"Error: {ex.Message}", title);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
