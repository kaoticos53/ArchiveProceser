using System;
using FileFlow.Sdk.Localization;
using FileFlow.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El cuerpo del selector de variables del host Uno: la vista de
/// <see cref="VariablePickerViewModel"/> (el view model PORTABLE, el mismo que envuelve la ventana del
/// escritorio). El catálogo, el filtro, el detalle y el token elegido son suyos; esta vista sólo los
/// enseña y aplica el idioma del host.
///
/// <para>La elección viaja por el <see cref="VariablePickerViewModel.SelectedToken"/> del view model,
/// que el <c>UnoWindowService</c> devuelve como valor del diálogo: el mismo camino por el que el
/// escritorio lee <c>picker.SelectedToken</c> al cerrar.</para>
/// </summary>
public sealed partial class VariablePickerDialogBody : UserControl
{
    public VariablePickerDialogBody()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RefreshLocalization();
        RefreshLocalization();
    }

    /// <summary>El view model portable que esta vista sirve.</summary>
    public VariablePickerViewModel? Vm => DataContext as VariablePickerViewModel;

    /// <summary>La caja de búsqueda del catálogo.</summary>
    internal TextBox Search => SearchBox;

    /// <summary>La lista de variables filtradas.</summary>
    internal ListView Variables => VariablesList;

    /// <summary>El texto del detalle: el token elegido, tal y como lo ve el usuario.</summary>
    internal string DetailTokenText => DetailTokenBlock.Text;

    /// <summary>El idioma vigente en los rótulos del diálogo (claves del diccionario del host).</summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        SearchBox.PlaceholderText = loc.GetString("Uno_Dialog_VarPicker_SearchPlaceholder",
            "Buscar por nombre de variable, descripción o categoría...");
        DetailTitle.Text = loc.GetString("Uno_Dialog_VarPicker_DetailTitle", "Detalle de la Variable");
        TokenLabel.Text = loc.GetString("Uno_Dialog_VarPicker_TokenLabel", "Token / Sintaxis:");
        DescriptionLabel.Text = loc.GetString("Uno_Dialog_VarPicker_DescriptionLabel", "Descripción:");
        CategoryLabel.Text = loc.GetString("Uno_Dialog_VarPicker_CategoryLabel", "Categoría:");
        SourceLabel.Text = loc.GetString("Uno_Dialog_VarPicker_SourceLabel", "Origen del Nodo:");
        SampleLabel.Text = loc.GetString("Uno_Dialog_VarPicker_SampleLabel", "Vista Previa Evaluada (Ejemplo):");
        NoResultsText.Text = loc.GetString("Uno_Dialog_VarPicker_NoResults",
            "No se encontraron variables coincidentes");
        CopyTokenButton.Content = loc.GetString("Uno_Dialog_VarPicker_CopyToken", "Copiar Token");
    }

    /// <summary>Copiar el token: el comando del view model (el portapapeles lo pone el núcleo).</summary>
    private void OnCopyTokenClicked(object sender, RoutedEventArgs e)
    {
        Vm?.CopyTokenCommand.Execute(null);
    }

    #region Redimensionamiento y Maximizado Interactivo
    private bool _isMaximized;
    private double _prevWidth = 820;
    private double _prevHeight = 520;

    private void OnMaximizeRestoreClicked(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null) return;

        if (!_isMaximized)
        {
            _prevWidth = PickerRoot.ActualWidth > 0 ? PickerRoot.ActualWidth : PickerRoot.Width;
            _prevHeight = PickerRoot.ActualHeight > 0 ? PickerRoot.ActualHeight : PickerRoot.Height;

            double targetWidth = Math.Max(760, XamlRoot.Size.Width - 60);
            double targetHeight = Math.Max(480, XamlRoot.Size.Height - 80);

            PickerRoot.Width = targetWidth;
            PickerRoot.Height = targetHeight;
            _isMaximized = true;

            MaximizeRestoreIcon.Text = "[-]";
            MaximizeRestoreLabel.Text = "Restaurar";
            ToolTipService.SetToolTip(MaximizeRestoreButton, "Restaurar tamaño original del catálogo");
        }
        else
        {
            PickerRoot.Width = _prevWidth > 0 ? _prevWidth : 820;
            PickerRoot.Height = _prevHeight > 0 ? _prevHeight : 520;
            _isMaximized = false;

            MaximizeRestoreIcon.Text = "[+]";
            MaximizeRestoreLabel.Text = "Maximizar";
            ToolTipService.SetToolTip(MaximizeRestoreButton, "Maximizar tamaño del catálogo");
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
            _startResizeWidth = PickerRoot.ActualWidth > 0 ? PickerRoot.ActualWidth : (double.IsNaN(PickerRoot.Width) ? 820 : PickerRoot.Width);
            _startResizeHeight = PickerRoot.ActualHeight > 0 ? PickerRoot.ActualHeight : (double.IsNaN(PickerRoot.Height) ? 520 : PickerRoot.Height);
            e.Handled = true;
        }
    }

    private void OnResizeGripPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizing) return;

        var currentPoint = e.GetCurrentPoint(null).Position;
        double deltaX = currentPoint.X - _resizeStartPos.X;
        double deltaY = currentPoint.Y - _resizeStartPos.Y;

        double maxW = 2000;
        double maxH = 1400;
        if (XamlRoot != null)
        {
            maxW = Math.Max(760, XamlRoot.Size.Width - 40);
            maxH = Math.Max(480, XamlRoot.Size.Height - 60);
        }

        double newWidth = Math.Clamp(_startResizeWidth + deltaX, 620, maxW);
        double newHeight = Math.Clamp(_startResizeHeight + deltaY, 400, maxH);

        PickerRoot.Width = newWidth;
        PickerRoot.Height = newHeight;
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
