using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using FileFlow.Plugin.FileSystem.UI.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;

namespace FileFlow.Plugin.FileSystem.UI.Views;

public partial class AdvancedRenamerEditorWindow : Window
{
    private readonly AdvancedRenamerEditorViewModel _viewModel;

    private Control? PanelNewName => this.FindControl<Control>("PanelNewName");
    private Control? PanelSearchReplace => this.FindControl<Control>("PanelSearchReplace");
    private Control? PanelInsert => this.FindControl<Control>("PanelInsert");
    private Control? PanelRemove => this.FindControl<Control>("PanelRemove");
    private Control? PanelCaseConversion => this.FindControl<Control>("PanelCaseConversion");
    private Control? PanelNumbering => this.FindControl<Control>("PanelNumbering");
    private Control? PanelReplaceList => this.FindControl<Control>("PanelReplaceList");
    private Control? PanelTrimClean => this.FindControl<Control>("PanelTrimClean");
    private Control? PanelNormalizeNumbers => this.FindControl<Control>("PanelNormalizeNumbers");

    private TextBox? TxtSearchPattern => this.FindControl<TextBox>("TxtSearchPattern");
    private TextBox? TxtReplacePattern => this.FindControl<TextBox>("TxtReplacePattern");
    private TextBox? TxtNumberRegexPattern => this.FindControl<TextBox>("TxtNumberRegexPattern");

    public AdvancedRenamerEditorWindow(IFlowNode node)
    {
        _viewModel = new AdvancedRenamerEditorViewModel(node);
        DataContext = _viewModel;
        UpdateVisibleFormPanels();
    }

    private void StepsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateVisibleFormPanels();
        _viewModel.GenerateLivePreview();
    }

    private void UpdateVisibleFormPanels()
    {
        if (PanelNewName == null) return;

        PanelNewName.IsVisible = false;
        if (PanelSearchReplace != null) PanelSearchReplace.IsVisible = false;
        if (PanelInsert != null) PanelInsert.IsVisible = false;
        if (PanelRemove != null) PanelRemove.IsVisible = false;
        if (PanelCaseConversion != null) PanelCaseConversion.IsVisible = false;
        if (PanelNumbering != null) PanelNumbering.IsVisible = false;
        if (PanelReplaceList != null) PanelReplaceList.IsVisible = false;
        if (PanelTrimClean != null) PanelTrimClean.IsVisible = false;
        if (PanelNormalizeNumbers != null) PanelNormalizeNumbers.IsVisible = false;

        if (_viewModel.SelectedStep == null) return;

        switch (_viewModel.SelectedStep.MethodType)
        {
            case RenameMethodType.NewName:
                PanelNewName.IsVisible = true;
                break;
            case RenameMethodType.SearchReplace:
                if (PanelSearchReplace != null) PanelSearchReplace.IsVisible = true;
                break;
            case RenameMethodType.Insert:
                if (PanelInsert != null) PanelInsert.IsVisible = true;
                break;
            case RenameMethodType.Remove:
                if (PanelRemove != null) PanelRemove.IsVisible = true;
                break;
            case RenameMethodType.CaseConversion:
                if (PanelCaseConversion != null) PanelCaseConversion.IsVisible = true;
                break;
            case RenameMethodType.Numbering:
                if (PanelNumbering != null) PanelNumbering.IsVisible = true;
                break;
            case RenameMethodType.ReplaceList:
                if (PanelReplaceList != null) PanelReplaceList.IsVisible = true;
                break;
            case RenameMethodType.TrimClean:
                if (PanelTrimClean != null) PanelTrimClean.IsVisible = true;
                break;
            case RenameMethodType.NormalizeNumbers:
                if (PanelNormalizeNumbers != null) PanelNormalizeNumbers.IsVisible = true;
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
        _viewModel.AddStepCommand.Execute(type);
        UpdateVisibleFormPanels();
    }

    private void PresetComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.SelectedPreset != null)
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
        if (sender is Control button)
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
            else if (button.Tag is string elementName && this.FindControl<TextBox>(elementName) is TextBox foundTb)
            {
                targetTextBox = foundTb;
            }

            var cm = new ContextMenu();
            var categories = _viewModel.AvailableTags.GroupBy(t => t.Category);

            foreach (var group in categories)
            {
                var categoryItem = new MenuItem
                {
                    Header = group.Key,
                    FontWeight = FontWeight.SemiBold
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

                cm.Items.Add(categoryItem);
            }

            cm.Placement = PlacementMode.Bottom;
            cm.Open(button);
        }
    }

    private void OpenRegexHelper_SearchReplace_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedStep == null) return;
        string initialPattern = _viewModel.SelectedStep.SearchText ?? string.Empty;
        string initialReplacement = _viewModel.SelectedStep.ReplaceText ?? string.Empty;
        string sampleText = "video_temporada_01_capitulo_05_1080p.mkv\nDocumento_Final (v2) [2026].pdf\n[Fansub] Anime 01.mp4";

        var regexWindow = new RegexHelperWindow(initialPattern, initialReplacement, sampleText);
        _ = regexWindow.ShowDialog<bool>(this).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && t.Result)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _viewModel.SelectedStep.SearchText = regexWindow.ResultPattern;
                    _viewModel.SelectedStep.ReplaceText = regexWindow.ResultReplacement;
                    _viewModel.SelectedStep.UseRegex = true;
                    if (TxtSearchPattern != null) TxtSearchPattern.Text = regexWindow.ResultPattern;
                    if (TxtReplacePattern != null) TxtReplacePattern.Text = regexWindow.ResultReplacement;
                    _viewModel.GenerateLivePreview();
                });
            }
        }, TaskScheduler.Default);
    }

    private void OpenRegexHelper_NormalizeNumbers_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedStep == null) return;
        string initialPattern = _viewModel.SelectedStep.NumberRegexPattern ?? string.Empty;
        string sampleText = "serie_1x2_720p.mkv\ncapitulo_5.mp4\ntrack 3 - cancion.mp3";

        var regexWindow = new RegexHelperWindow(initialPattern, string.Empty, sampleText);
        _ = regexWindow.ShowDialog<bool>(this).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && t.Result)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _viewModel.SelectedStep.NumberRegexPattern = regexWindow.ResultPattern;
                    if (TxtNumberRegexPattern != null) TxtNumberRegexPattern.Text = regexWindow.ResultPattern;
                    _viewModel.GenerateLivePreview();
                });
            }
        }, TaskScheduler.Default);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.SaveAndCloseCommand.Execute(this);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
