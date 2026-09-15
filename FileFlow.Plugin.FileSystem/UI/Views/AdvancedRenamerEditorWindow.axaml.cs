using Avalonia.Controls;
using Avalonia.Interactivity;
using FileFlow.Plugin.FileSystem.UI.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;

namespace FileFlow.Plugin.FileSystem.UI.Views;

public partial class AdvancedRenamerEditorWindow : Window
{
    private readonly AdvancedRenamerEditorViewModel _viewModel;

    public AdvancedRenamerEditorWindow()
    {
        InitializeComponent();
        _viewModel = null!;
    }

    public AdvancedRenamerEditorWindow(IFlowNode node)
    {
        InitializeComponent();
        _viewModel = new AdvancedRenamerEditorViewModel(node);
        DataContext = _viewModel;
        UpdateVisibleFormPanels();
    }

    private void StepsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateVisibleFormPanels();
        _viewModel?.GenerateLivePreview();
    }

    private void UpdateVisibleFormPanels()
    {
        if (PanelNewName == null) return;

        PanelNewName.IsVisible = false;
        PanelSearchReplace.IsVisible = false;
        PanelInsert.IsVisible = false;
        PanelRemove.IsVisible = false;
        PanelCaseConversion.IsVisible = false;
        PanelNumbering.IsVisible = false;
        PanelReplaceList.IsVisible = false;
        PanelTrimClean.IsVisible = false;
        PanelNormalizeNumbers.IsVisible = false;

        if (_viewModel?.SelectedStep == null) return;

        switch (_viewModel.SelectedStep.MethodType)
        {
            case RenameMethodType.NewName:
                PanelNewName.IsVisible = true;
                break;
            case RenameMethodType.SearchReplace:
                PanelSearchReplace.IsVisible = true;
                break;
            case RenameMethodType.Insert:
                PanelInsert.IsVisible = true;
                break;
            case RenameMethodType.Remove:
                PanelRemove.IsVisible = true;
                break;
            case RenameMethodType.CaseConversion:
                PanelCaseConversion.IsVisible = true;
                break;
            case RenameMethodType.Numbering:
                PanelNumbering.IsVisible = true;
                break;
            case RenameMethodType.ReplaceList:
                PanelReplaceList.IsVisible = true;
                break;
            case RenameMethodType.TrimClean:
                PanelTrimClean.IsVisible = true;
                break;
            case RenameMethodType.NormalizeNumbers:
                PanelNormalizeNumbers.IsVisible = true;
                break;
        }
    }

    private void AddMethod_NewName_Click(object? sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.NewName);
    private void AddMethod_SearchReplace_Click(object? sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.SearchReplace);
    private void AddMethod_Insert_Click(object? sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.Insert);
    private void AddMethod_Remove_Click(object? sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.Remove);
    private void AddMethod_Case_Click(object? sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.CaseConversion);
    private void AddMethod_Numbering_Click(object? sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.Numbering);
    private void AddMethod_ReplaceList_Click(object? sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.ReplaceList);
    private void AddMethod_TrimClean_Click(object? sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.TrimClean);
    private void AddMethod_NormalizeNumbers_Click(object? sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.NormalizeNumbers);

    private void AddAndSelect(RenameMethodType type)
    {
        _viewModel?.AddStepCommand.Execute(type);
        UpdateVisibleFormPanels();
    }

    private void PresetComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel?.SelectedPreset != null)
        {
            UpdateVisibleFormPanels();
        }
    }

    private void FormInput_Changed(object? sender, TextChangedEventArgs e)
    {
        _viewModel?.GenerateLivePreview();
    }

    private void FormCombo_Changed(object? sender, SelectionChangedEventArgs e)
    {
        _viewModel?.GenerateLivePreview();
    }

    private void StepCheckbox_Changed(object? sender, RoutedEventArgs e)
    {
        _viewModel?.GenerateLivePreview();
    }

    private void OpenVariablesMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control button && _viewModel != null)
        {
            TextBox? targetTextBox = null;
            bool isReplaceListTarget = false;

            if (button.Tag is TextBox tb)
            {
                targetTextBox = tb;
            }
            else if (button.Tag is string str && str == "ReplaceList")
            {
                isReplaceListTarget = true;
            }

            var flyout = new MenuFlyout();
            var categories = _viewModel.AvailableTags.GroupBy(t => t.Category);

            foreach (var group in categories)
            {
                var categoryItem = new MenuItem
                {
                    Header = group.Key,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold
                };

                foreach (var tag in group)
                {
                    var item = new MenuItem
                    {
                        Header = $"{tag.Tag}  —  {tag.Description}",
                        Tag = tag.Tag
                    };
                    item.Click += (s, args) =>
                    {
                        if (targetTextBox != null)
                        {
                            int caret = targetTextBox.CaretIndex;
                            string current = targetTextBox.Text ?? string.Empty;
                            if (caret >= 0 && caret <= current.Length)
                            {
                                targetTextBox.Text = current.Insert(caret, tag.Tag);
                                targetTextBox.CaretIndex = caret + tag.Tag.Length;
                            }
                            else
                            {
                                targetTextBox.Text = current + tag.Tag;
                                targetTextBox.CaretIndex = targetTextBox.Text.Length;
                            }
                            targetTextBox.Focus();
                        }
                        else if (isReplaceListTarget && _viewModel.SelectedStep != null)
                        {
                            _viewModel.SelectedStep.ReplaceList.Add(new ReplaceListEntry
                            {
                                Find = tag.Tag,
                                ReplaceWith = string.Empty
                            });
                        }
                        else
                        {
                            _viewModel.InsertTagIntoSelectedStepCommand.Execute(tag.Tag);
                        }
                        _viewModel.GenerateLivePreview();
                    };
                    categoryItem.Items.Add(item);
                }

                flyout.Items.Add(categoryItem);
            }

            flyout.ShowAt(button);
        }
    }

    private async void OpenRegexHelper_SearchReplace_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedStep == null) return;
        string initialPattern = _viewModel.SelectedStep.SearchText ?? string.Empty;
        string initialReplacement = _viewModel.SelectedStep.ReplaceText ?? string.Empty;
        string sampleText = "video_temporada_01_capitulo_05_1080p.mkv\nDocumento_Final (v2) [2026].pdf\n[Fansub] Anime 01.mp4";

        var regexWindow = new RegexHelperWindow(initialPattern, initialReplacement, sampleText);
        var result = await regexWindow.ShowDialog<bool>(this);
        if (result)
        {
            _viewModel.SelectedStep.SearchText = regexWindow.ResultPattern;
            _viewModel.SelectedStep.ReplaceText = regexWindow.ResultReplacement;
            _viewModel.SelectedStep.UseRegex = true;
            if (TxtSearchPattern != null) TxtSearchPattern.Text = regexWindow.ResultPattern;
            if (TxtReplacePattern != null) TxtReplacePattern.Text = regexWindow.ResultReplacement;
            _viewModel.GenerateLivePreview();
        }
    }

    private async void OpenRegexHelper_NormalizeNumbers_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedStep == null) return;
        string initialPattern = _viewModel.SelectedStep.NumberRegexPattern ?? string.Empty;
        string sampleText = "serie_1x2_720p.mkv\ncapitulo_5.mp4\ntrack 3 - cancion.mp3";

        var regexWindow = new RegexHelperWindow(initialPattern, string.Empty, sampleText);
        var result = await regexWindow.ShowDialog<bool>(this);
        if (result)
        {
            _viewModel.SelectedStep.NumberRegexPattern = regexWindow.ResultPattern;
            if (TxtNumberRegexPattern != null) TxtNumberRegexPattern.Text = regexWindow.ResultPattern;
            _viewModel.GenerateLivePreview();
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SaveAndCloseCommand.Execute(this);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
