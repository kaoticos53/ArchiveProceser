using System;
using System.ComponentModel;
using System.Linq;
using FileFlow.Plugin.FileSystem.UI.ViewModels;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Renaming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El ESTUDIO DE RENOMBRADO AVANZADO (Pipeline de Métodos) del host Uno: la vista del
/// <see cref="AdvancedRenamerEditorViewModel"/> portable del plugin de sistema de archivos.
/// </summary>
public sealed partial class AdvancedRenamerBody : UserControl
{
    private readonly AdvancedRenamerEditorViewModel _vm;
    private bool _updatingEditor;

    public AdvancedRenamerBody(AdvancedRenamerEditorViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        DataContext = _vm;

        StepApplyToCombo.ItemsSource = Enum.GetValues<ApplyToTarget>();
        InsertPositionCombo.ItemsSource = Enum.GetValues<CharacterPosition>();
        RemovePositionCombo.ItemsSource = Enum.GetValues<CharacterPosition>();
        CaseTypeCombo.ItemsSource = Enum.GetValues<CaseTransformType>();
        NumberingResetCombo.ItemsSource = Enum.GetValues<NumberingResetOn>();
        NormNumbersTargetCombo.ItemsSource = Enum.GetValues<NumberPaddingTarget>();

        _vm.PropertyChanged += OnVmPropertyChanged;
        Unloaded += (_, _) => _vm.PropertyChanged -= OnVmPropertyChanged;

        RefreshLocalization();
        UpdateStepEditor();
    }

    internal AdvancedRenamerEditorViewModel Vm => _vm;

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AdvancedRenamerEditorViewModel.SelectedStep))
        {
            UpdateStepEditor();
        }
        else if (e.PropertyName is nameof(AdvancedRenamerEditorViewModel.PipelineName))
        {
            if (PipelineNameBox.Text != (_vm.PipelineName ?? string.Empty))
            {
                PipelineNameBox.Text = _vm.PipelineName ?? string.Empty;
            }
        }
    }

    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;
        HeaderTitle.Text = loc.GetString("AdvancedRenamer_WindowTitle", "Estudio de Renombrado Avanzado (Pipeline de Métodos)");
        MethodsBadge.Text = loc.GetString("AdvancedRenamer_MethodsBadge", "9 Métodos Acumulativos");
        HeaderDesc.Text = loc.GetString("AdvancedRenamer_HeaderSubtitle", "Aplica secuencialmente métodos de renombrado evaluando variables del flujo, metadatos, funciones y plantillas.");
        PresetLabel.Text = loc.GetString("AdvancedRenamer_PresetLabel", "Preset:");
        SavePresetLabel.Text = loc.GetString("AdvancedRenamer_SaveBtn", "💾 Guardar");
        OpenPresetLabel.Text = loc.GetString("AdvancedRenamer_OpenBtn", "📂 Abrir");
        PipelineNameLabel.Text = "Pipeline:";
        CollisionLabel.Text = loc.GetString("AdvancedRenamer_CollisionLabel", "Colisión:");
        AddStepLabel.Text = loc.GetString("AdvancedRenamer_AddMethod", "➕ Añadir Método");
        LivePreviewTitle.Text = loc.GetString("AdvancedRenamer_LivePreviewTitle", "👁️ Vista Previa en Vivo (Live Preview):");
        EmptyStepMessage.Text = loc.GetString("AdvancedRenamer_EmptyStateStep", "Selecciona o añade un método del panel izquierdo para configurar sus propiedades.");

        ToolTipService.SetToolTip(SavePresetButton, loc.GetString("AdvancedRenamer_SavePresetToolTip", "Guardar pipeline actual como preset"));
        ToolTipService.SetToolTip(OpenPresetButton, loc.GetString("AdvancedRenamer_OpenPresetToolTip", "Cargar archivo .ffren o .json"));
    }

    private void UpdateStepEditor()
    {
        var step = _vm.SelectedStep;
        if (step is null)
        {
            EmptyStepMessage.Visibility = Visibility.Visible;
            StepEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyStepMessage.Visibility = Visibility.Collapsed;
        StepEditorPanel.Visibility = Visibility.Visible;

        _updatingEditor = true;
        try
        {
            StepNameBox.Text = step.Name ?? string.Empty;
            StepEnabledCheck.IsChecked = step.IsEnabled;
            StepApplyToCombo.SelectedItem = step.ApplyTo;

            // Ocultar todos los subpaneles
            PaneNewName.Visibility = Visibility.Collapsed;
            PaneSearchReplace.Visibility = Visibility.Collapsed;
            PaneInsert.Visibility = Visibility.Collapsed;
            PaneRemove.Visibility = Visibility.Collapsed;
            PaneCaseConversion.Visibility = Visibility.Collapsed;
            PaneNumbering.Visibility = Visibility.Collapsed;
            PaneReplaceList.Visibility = Visibility.Collapsed;
            PaneTrimClean.Visibility = Visibility.Collapsed;
            PaneNormalizeNumbers.Visibility = Visibility.Collapsed;

            // Mostrar y poblar el panel correspondiente
            switch (step.MethodType)
            {
                case RenameMethodType.NewName:
                    PaneNewName.Visibility = Visibility.Visible;
                    NewNamePatternBox.Text = step.Pattern ?? string.Empty;
                    break;

                case RenameMethodType.SearchReplace:
                    PaneSearchReplace.Visibility = Visibility.Visible;
                    SearchTextBox.Text = step.SearchText ?? string.Empty;
                    ReplaceTextBox.Text = step.ReplaceText ?? string.Empty;
                    UseRegexCheck.IsChecked = step.UseRegex;
                    MatchCaseCheck.IsChecked = step.MatchCase;
                    ReplaceAllCheck.IsChecked = step.ReplaceAll;
                    break;

                case RenameMethodType.Insert:
                    PaneInsert.Visibility = Visibility.Visible;
                    InsertTextBox.Text = step.SearchText ?? step.Pattern ?? string.Empty;
                    InsertPositionCombo.SelectedItem = step.Position;
                    InsertIndexBox.Text = step.PositionIndex.ToString();
                    break;

                case RenameMethodType.Remove:
                    PaneRemove.Visibility = Visibility.Visible;
                    RemoveCountBox.Text = step.CharacterCount.ToString();
                    RemovePositionCombo.SelectedItem = step.Position;
                    RemoveIndexBox.Text = step.PositionIndex.ToString();
                    break;

                case RenameMethodType.CaseConversion:
                    PaneCaseConversion.Visibility = Visibility.Visible;
                    CaseTypeCombo.SelectedItem = step.CaseType;
                    break;

                case RenameMethodType.Numbering:
                    PaneNumbering.Visibility = Visibility.Visible;
                    NumberingStartBox.Text = step.StartNumber.ToString();
                    NumberingStepBox.Text = step.Increment.ToString();
                    NumberingPaddingBox.Text = step.PaddingZeroes.ToString();
                    NumberingResetCombo.SelectedItem = step.ResetOn;
                    break;

                case RenameMethodType.ReplaceList:
                    PaneReplaceList.Visibility = Visibility.Visible;
                    ReplaceListView.ItemsSource = step.ReplaceList;
                    break;

                case RenameMethodType.TrimClean:
                    PaneTrimClean.Visibility = Visibility.Visible;
                    TrimWhitespaceCheck.IsChecked = step.TrimWhitespace;
                    CollapseSpacesCheck.IsChecked = step.CollapseSpaces;
                    SanitizeInvalidCheck.IsChecked = step.SanitizeInvalidChars;
                    break;

                case RenameMethodType.NormalizeNumbers:
                    PaneNormalizeNumbers.Visibility = Visibility.Visible;
                    NormNumbersPaddingBox.Text = step.NumberPaddingDigits.ToString();
                    NormNumbersTargetCombo.SelectedItem = step.NumberTarget;
                    PadSeasonEpisodeCheck.IsChecked = step.PadSeasonAndEpisode;
                    break;
            }
        }
        finally
        {
            _updatingEditor = false;
        }
    }

    #region Handlers de adición de pasos
    private void OnAddStepNewNameClicked(object sender, RoutedEventArgs e) => _vm.AddStep(RenameMethodType.NewName);
    private void OnAddStepSearchReplaceClicked(object sender, RoutedEventArgs e) => _vm.AddStep(RenameMethodType.SearchReplace);
    private void OnAddStepInsertClicked(object sender, RoutedEventArgs e) => _vm.AddStep(RenameMethodType.Insert);
    private void OnAddStepRemoveClicked(object sender, RoutedEventArgs e) => _vm.AddStep(RenameMethodType.Remove);
    private void OnAddStepCaseClicked(object sender, RoutedEventArgs e) => _vm.AddStep(RenameMethodType.CaseConversion);
    private void OnAddStepNumberingClicked(object sender, RoutedEventArgs e) => _vm.AddStep(RenameMethodType.Numbering);
    private void OnAddStepReplaceListClicked(object sender, RoutedEventArgs e) => _vm.AddStep(RenameMethodType.ReplaceList);
    private void OnAddStepTrimCleanClicked(object sender, RoutedEventArgs e) => _vm.AddStep(RenameMethodType.TrimClean);
    private void OnAddStepNormalizeNumbersClicked(object sender, RoutedEventArgs e) => _vm.AddStep(RenameMethodType.NormalizeNumbers);
    #endregion

    #region Handlers de sincronización de campos
    private void OnStepFieldValueChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        _vm.SelectedStep.Name = StepNameBox.Text;
    }

    private void OnStepFieldCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        _vm.SelectedStep.IsEnabled = StepEnabledCheck.IsChecked == true;
        _vm.GenerateLivePreview();
    }

    private void OnStepApplyToChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        if (StepApplyToCombo.SelectedItem is ApplyToTarget target)
        {
            _vm.SelectedStep.ApplyTo = target;
            _vm.GenerateLivePreview();
        }
    }

    private void OnNewNamePatternChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        _vm.SelectedStep.Pattern = NewNamePatternBox.Text;
        _vm.GenerateLivePreview();
    }

    private void OnInsertTagClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string tag)
        {
            _vm.InsertTagIntoSelectedStep(tag);
            NewNamePatternBox.Text = _vm.SelectedStep?.Pattern ?? string.Empty;
        }
    }

    private void OnSearchReplaceChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        _vm.SelectedStep.SearchText = SearchTextBox.Text;
        _vm.SelectedStep.ReplaceText = ReplaceTextBox.Text;
        _vm.GenerateLivePreview();
    }

    private void OnSearchReplaceOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        _vm.SelectedStep.UseRegex = UseRegexCheck.IsChecked == true;
        _vm.SelectedStep.MatchCase = MatchCaseCheck.IsChecked == true;
        _vm.SelectedStep.ReplaceAll = ReplaceAllCheck.IsChecked == true;
        _vm.GenerateLivePreview();
    }

    private void OnInsertChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        _vm.SelectedStep.SearchText = InsertTextBox.Text;
        _vm.SelectedStep.Pattern = InsertTextBox.Text;
        if (int.TryParse(InsertIndexBox.Text, out int idx))
        {
            _vm.SelectedStep.PositionIndex = idx;
        }
        _vm.GenerateLivePreview();
    }

    private void OnInsertPositionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        if (InsertPositionCombo.SelectedItem is CharacterPosition pos)
        {
            _vm.SelectedStep.Position = pos;
            _vm.GenerateLivePreview();
        }
    }

    private void OnRemoveChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        if (int.TryParse(RemoveCountBox.Text, out int cnt))
        {
            _vm.SelectedStep.CharacterCount = cnt;
        }
        if (int.TryParse(RemoveIndexBox.Text, out int idx))
        {
            _vm.SelectedStep.PositionIndex = idx;
        }
        _vm.GenerateLivePreview();
    }

    private void OnRemovePositionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        if (RemovePositionCombo.SelectedItem is CharacterPosition pos)
        {
            _vm.SelectedStep.Position = pos;
            _vm.GenerateLivePreview();
        }
    }

    private void OnCaseTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        if (CaseTypeCombo.SelectedItem is CaseTransformType ct)
        {
            _vm.SelectedStep.CaseType = ct;
            _vm.GenerateLivePreview();
        }
    }

    private void OnNumberingChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        if (int.TryParse(NumberingStartBox.Text, out int start)) _vm.SelectedStep.StartNumber = start;
        if (int.TryParse(NumberingStepBox.Text, out int step)) _vm.SelectedStep.Increment = step;
        if (int.TryParse(NumberingPaddingBox.Text, out int pad)) _vm.SelectedStep.PaddingZeroes = pad;
        _vm.GenerateLivePreview();
    }

    private void OnNumberingResetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        if (NumberingResetCombo.SelectedItem is NumberingResetOn rst)
        {
            _vm.SelectedStep.ResetOn = rst;
            _vm.GenerateLivePreview();
        }
    }

    private void OnRemoveReplaceListEntryClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ReplaceListEntry entry)
        {
            _vm.RemoveReplaceListEntry(entry);
        }
    }

    private void OnTrimCleanChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        _vm.SelectedStep.TrimWhitespace = TrimWhitespaceCheck.IsChecked == true;
        _vm.SelectedStep.CollapseSpaces = CollapseSpacesCheck.IsChecked == true;
        _vm.SelectedStep.SanitizeInvalidChars = SanitizeInvalidCheck.IsChecked == true;
        _vm.GenerateLivePreview();
    }

    private void OnNormalizeNumbersChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        if (int.TryParse(NormNumbersPaddingBox.Text, out int pad)) _vm.SelectedStep.NumberPaddingDigits = pad;
        _vm.SelectedStep.PadSeasonAndEpisode = PadSeasonEpisodeCheck.IsChecked == true;
        _vm.GenerateLivePreview();
    }

    private void OnNormalizeNumbersTargetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingEditor || _vm.SelectedStep is null) return;
        if (NormNumbersTargetCombo.SelectedItem is NumberPaddingTarget target)
        {
            _vm.SelectedStep.NumberTarget = target;
            _vm.GenerateLivePreview();
        }
    }
    #endregion

    #region Redimensionamiento y Maximizado Interactivo
    private bool _isMaximized;
    private double _prevWidth = 1020;
    private double _prevHeight = 640;

    private void OnMaximizeRestoreClicked(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null) return;

        if (!_isMaximized)
        {
            _prevWidth = RenamerRoot.ActualWidth > 0 ? RenamerRoot.ActualWidth : RenamerRoot.Width;
            _prevHeight = RenamerRoot.ActualHeight > 0 ? RenamerRoot.ActualHeight : RenamerRoot.Height;

            double targetWidth = Math.Max(920, XamlRoot.Size.Width - 60);
            double targetHeight = Math.Max(580, XamlRoot.Size.Height - 80);

            RenamerRoot.Width = targetWidth;
            RenamerRoot.Height = targetHeight;
            _isMaximized = true;

            MaximizeRestoreIcon.Text = "[-]";
            MaximizeRestoreLabel.Text = "Restaurar";
            ToolTipService.SetToolTip(MaximizeRestoreButton, "Restaurar tamaño original del diálogo");
        }
        else
        {
            RenamerRoot.Width = _prevWidth > 0 ? _prevWidth : 1020;
            RenamerRoot.Height = _prevHeight > 0 ? _prevHeight : 640;
            _isMaximized = false;

            MaximizeRestoreIcon.Text = "[+]";
            MaximizeRestoreLabel.Text = "Maximizar";
            ToolTipService.SetToolTip(MaximizeRestoreButton, "Maximizar tamaño del diálogo");
        }
    }

    private bool _isResizing;
    private Point _resizeStartPos;
    private double _startResizeWidth;
    private double _startResizeHeight;

    private void OnResizeGripPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var element = sender as UIElement;
        if (element != null && element.CapturePointer(e.Pointer))
        {
            _isResizing = true;
            _resizeStartPos = e.GetCurrentPoint(null).Position;
            _startResizeWidth = RenamerRoot.ActualWidth > 0 ? RenamerRoot.ActualWidth : (double.IsNaN(RenamerRoot.Width) ? 1020 : RenamerRoot.Width);
            _startResizeHeight = RenamerRoot.ActualHeight > 0 ? RenamerRoot.ActualHeight : (double.IsNaN(RenamerRoot.Height) ? 640 : RenamerRoot.Height);
            e.Handled = true;
        }
    }

    private void OnResizeGripPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizing) return;

        var currentPoint = e.GetCurrentPoint(null).Position;
        double deltaX = currentPoint.X - _resizeStartPos.X;
        double deltaY = currentPoint.Y - _resizeStartPos.Y;

        double maxW = 2200;
        double maxH = 1500;
        if (XamlRoot != null)
        {
            maxW = Math.Max(920, XamlRoot.Size.Width - 40);
            maxH = Math.Max(560, XamlRoot.Size.Height - 60);
        }

        double newWidth = Math.Clamp(_startResizeWidth + deltaX, 800, maxW);
        double newHeight = Math.Clamp(_startResizeHeight + deltaY, 520, maxH);

        RenamerRoot.Width = newWidth;
        RenamerRoot.Height = newHeight;
        _isMaximized = false;
        MaximizeRestoreIcon.Text = "[+]";
        MaximizeRestoreLabel.Text = "Maximizar";
        e.Handled = true;
    }

    private void OnResizeGripPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isResizing)
        {
            _isResizing = false;
            (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnResizeGripPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isResizing = false;
    }
    #endregion
}
