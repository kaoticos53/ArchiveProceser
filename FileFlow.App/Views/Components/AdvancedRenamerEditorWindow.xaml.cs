using System.Windows;
using System.Windows.Controls;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Renaming;

namespace FileFlow.App.Views.Components;

public partial class AdvancedRenamerEditorWindow : Window
{
    private readonly AdvancedRenamerEditorViewModel _viewModel;

    public AdvancedRenamerEditorWindow(NodeViewModel nodeViewModel)
    {
        InitializeComponent();
        _viewModel = new AdvancedRenamerEditorViewModel(nodeViewModel);
        DataContext = _viewModel;
        UpdateVisibleFormPanels();
    }

    private void StepsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateVisibleFormPanels();
        _viewModel.GenerateLivePreview();
    }

    private void UpdateVisibleFormPanels()
    {
        if (PanelNewName == null) return;

        PanelNewName.Visibility = Visibility.Collapsed;
        PanelSearchReplace.Visibility = Visibility.Collapsed;
        PanelInsert.Visibility = Visibility.Collapsed;
        PanelRemove.Visibility = Visibility.Collapsed;
        PanelCaseConversion.Visibility = Visibility.Collapsed;
        PanelNumbering.Visibility = Visibility.Collapsed;
        PanelReplaceList.Visibility = Visibility.Collapsed;
        PanelTrimClean.Visibility = Visibility.Collapsed;
        PanelNormalizeNumbers.Visibility = Visibility.Collapsed;

        if (_viewModel.SelectedStep == null) return;

        switch (_viewModel.SelectedStep.MethodType)
        {
            case RenameMethodType.NewName:
                PanelNewName.Visibility = Visibility.Visible;
                break;
            case RenameMethodType.SearchReplace:
                PanelSearchReplace.Visibility = Visibility.Visible;
                break;
            case RenameMethodType.Insert:
                PanelInsert.Visibility = Visibility.Visible;
                break;
            case RenameMethodType.Remove:
                PanelRemove.Visibility = Visibility.Visible;
                break;
            case RenameMethodType.CaseConversion:
                PanelCaseConversion.Visibility = Visibility.Visible;
                break;
            case RenameMethodType.Numbering:
                PanelNumbering.Visibility = Visibility.Visible;
                break;
            case RenameMethodType.ReplaceList:
                PanelReplaceList.Visibility = Visibility.Visible;
                break;
            case RenameMethodType.TrimClean:
                PanelTrimClean.Visibility = Visibility.Visible;
                break;
            case RenameMethodType.NormalizeNumbers:
                PanelNormalizeNumbers.Visibility = Visibility.Visible;
                break;
        }
    }

    private void AddMethod_NewName_Click(object sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.NewName);
    private void AddMethod_SearchReplace_Click(object sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.SearchReplace);
    private void AddMethod_Insert_Click(object sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.Insert);
    private void AddMethod_Remove_Click(object sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.Remove);
    private void AddMethod_Case_Click(object sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.CaseConversion);
    private void AddMethod_Numbering_Click(object sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.Numbering);
    private void AddMethod_ReplaceList_Click(object sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.ReplaceList);
    private void AddMethod_TrimClean_Click(object sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.TrimClean);
    private void AddMethod_NormalizeNumbers_Click(object sender, RoutedEventArgs e) => AddAndSelect(RenameMethodType.NormalizeNumbers);

    private void AddAndSelect(RenameMethodType type)
    {
        _viewModel.AddStepCommand.Execute(type);
        UpdateVisibleFormPanels();
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.SelectedPreset != null)
        {
            _viewModel.ApplyPresetCommand.Execute(_viewModel.SelectedPreset);
            UpdateVisibleFormPanels();
        }
    }

    private void FormInput_Changed(object sender, TextChangedEventArgs e)
    {
        _viewModel?.GenerateLivePreview();
    }

    private void FormCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        _viewModel?.GenerateLivePreview();
    }

    private void StepCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel?.GenerateLivePreview();
    }

    private void OpenVariablesMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement button)
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
            else if (button.Tag is string elementName && FindName(elementName) is TextBox foundTb)
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
                    FontWeight = FontWeights.SemiBold
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

                            var binding = targetTextBox.GetBindingExpression(TextBox.TextProperty);
                            binding?.UpdateSource();
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

            cm.PlacementTarget = button;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            cm.IsOpen = true;
        }
    }

    private void OpenRegexHelper_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement button)
        {
            TextBox? targetTextBox = button.Tag as TextBox;
            bool isReplaceList = button.Tag is string str && str == "ReplaceList";

            string currentPattern = targetTextBox?.Text ?? string.Empty;
            string currentReplacement = (_viewModel.SelectedStep?.MethodType == RenameMethodType.SearchReplace) 
                ? (_viewModel.SelectedStep.ReplaceText ?? string.Empty) 
                : string.Empty;

            var dialog = new RegexHelperWindow(currentPattern, currentReplacement)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                if (targetTextBox != null)
                {
                    targetTextBox.Text = dialog.ResultPattern;
                    targetTextBox.Focus();
                    targetTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

                    if (_viewModel.SelectedStep?.MethodType == RenameMethodType.SearchReplace && targetTextBox == TxtSearchPattern)
                    {
                        _viewModel.SelectedStep.UseRegex = true;
                        if (!string.IsNullOrEmpty(dialog.ResultReplacement))
                        {
                            TxtReplacePattern.Text = dialog.ResultReplacement;
                            TxtReplacePattern.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                        }
                    }
                }
                else if (isReplaceList && _viewModel.SelectedStep != null)
                {
                    _viewModel.SelectedStep.ReplaceList.Add(new ReplaceListEntry
                    {
                        Find = dialog.ResultPattern,
                        ReplaceWith = dialog.ResultReplacement,
                        UseRegex = true
                    });
                }

                _viewModel.GenerateLivePreview();
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveAndCloseCommand.Execute(this);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
