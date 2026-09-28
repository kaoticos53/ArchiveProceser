using FileFlow.Sdk.Localization;
using FileFlow.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
}
