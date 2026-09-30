using System;
using FileFlow.App.Models;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El cuerpo del diálogo de edición de texto del host Uno: la vista de
/// <see cref="TextEditorDialogViewModel"/> (el view model PORTABLE, el mismo que envuelve la ventana
/// de la versión anterior). Aquí no se decide nada del producto: la caja es el <c>Text</c> del view model en
/// dos sentidos, el panel de variables es su <c>IsSidePanelVisible</c> con su catálogo, y la inserción
/// llama a su <see cref="TextEditorDialogViewModel.InsertTokenAt"/> — el método que la versión anterior usa
/// para lo mismo, que inserta en el punto del cursor y devuelve dónde queda.
///
/// <para><b>Por qué el panel va DENTRO del mismo diálogo</b> y no en una ventana anidada: WinUI no
/// admite dos <c>ContentDialog</c> abiertos a la vez sobre la misma raíz (la segunda abre con
/// excepción), así que el camino de la versión anterior —abrir el selector encima del editor— se sirve aquí
/// con el panel lateral que el propio view model ya trae para esta pantalla.</para>
/// </summary>
public sealed partial class TextEditorDialogBody : UserControl
{
    public TextEditorDialogBody()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RefreshLocalization();
        RefreshLocalization();
    }

    /// <summary>El view model portable que esta vista sirve.</summary>
    public TextEditorDialogViewModel? Vm => DataContext as TextEditorDialogViewModel;

    /// <summary>La caja de texto (la que un lector de pantalla o el driver externo encuentran).</summary>
    internal TextBox Editor => EditorBox;

    /// <summary>El botón que despliega el panel de variables.</summary>
    internal Button InsertVariable => InsertVariableButton;

    /// <summary>El botón de limpiar (el comando del view model).</summary>
    internal Button Clear => ClearButton;

    /// <summary>La lista de variables del panel, con el catálogo que el view model filtró.</summary>
    internal ListView Variables => VariableList;

    /// <summary>El panel de variables, tal y como está ahora (para la sonda: desplegado o no).</summary>
    internal bool IsVariablePanelOpen => VariablePanelBorder.Visibility == Visibility.Visible;

    /// <summary>El panel de variables (su visibilidad la manda el view model).</summary>
    internal Border VariablePanel => VariablePanelBorder;

    /// <summary>
    /// El idioma vigente en los textos del diálogo. Se resuelven con las claves del diccionario del
    /// host (con el texto de la versión anterior como fallback), así que cambiar de idioma y volver a abrir
    /// el diálogo lo enseña en el idioma nuevo.
    /// </summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        InsertVariableButton.Content = loc.GetString("Uno_Dialog_TextEditor_InsertVar", "Insertar Variable ({x})");
        ClearButton.Content = loc.GetString("Uno_Dialog_TextEditor_Clear", "Limpiar");
        VariablesHeader.Text = loc.GetString("Uno_Dialog_TextEditor_VariablesHeader",
            "🔍 Variables y Metadatos del Contexto:");
        PreviewLabel.Text = loc.GetString("Uno_Dialog_TextEditor_Preview",
            "⚡ Vista previa evaluada en tiempo real:");

        ToolTipService.SetToolTip(InsertVariableButton,
            loc.GetString("Uno_Dialog_TextEditor_InsertVar", "Insertar Variable ({x})"));
        ToolTipService.SetToolTip(ClearButton,
            loc.GetString("Uno_Dialog_TextEditor_Clear", "Limpiar"));
    }

    /// <summary>
    /// «Insertar Variable»: despliega o recoge el panel de variables del view model — el mismo
    /// comando (<c>ToggleSidePanelCommand</c>) que la versión anterior.
    /// </summary>
    private void OnInsertVariableClicked(object sender, RoutedEventArgs e)
    {
        Vm?.ToggleSidePanelCommand.Execute(null);

        if (Vm?.IsSidePanelVisible == true)
        {
            VariableSearchBox.Focus(FocusState.Programmatic);
        }
    }

    /// <summary>«Limpiar»: el comando del view model (el estado vive en él, no en la vista).</summary>
    private void OnClearClicked(object sender, RoutedEventArgs e)
    {
        Vm?.ClearTextCommand.Execute(null);
        EditorBox.Text = Vm?.Text ?? string.Empty;
        EditorBox.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Elegir una variable la escribe DONDE ESTÁ EL CURSOR: la inserción es la del view model
    /// (<see cref="TextEditorDialogViewModel.InsertTokenAt"/>), que devuelve el texto nuevo y dónde
    /// queda el cursor, y la vista sólo los aplica.
    /// </summary>
    private void OnVariableItemClicked(object sender, ItemClickEventArgs e)
    {
        if (Vm is not { } vm || e.ClickedItem is not VariableItem item || string.IsNullOrEmpty(item.Token))
        {
            return;
        }

        var (text, caret) = vm.InsertTokenAt(EditorBox.SelectionStart, item.Token);
        EditorBox.Text = text;
        EditorBox.SelectionStart = Math.Max(0, Math.Min(caret, EditorBox.Text.Length));
        EditorBox.Focus(FocusState.Programmatic);
    }
}
