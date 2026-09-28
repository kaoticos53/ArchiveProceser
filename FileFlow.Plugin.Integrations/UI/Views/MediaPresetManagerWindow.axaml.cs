using System;
using FileFlow.Plugin.Integrations.UI.Services;
using FileFlow.Plugin.Integrations.UI.ViewModels;
using FileFlow.Sdk.Services;

namespace FileFlow.Plugin.Integrations.UI.Views;

/// <summary>
/// El GESTOR DE PRESETS DE MEDIOS del escritorio: la VISTA de
/// <see cref="MediaPresetManagerViewModel"/>, el view model portable del plugin.
///
/// <para><b>Qué es y qué no.</b> La lista de presets, el formulario del elegido, la normalización de la
/// extensión, la prohibición de borrar los presets del sistema y las confirmaciones viven en el view model,
/// que es el mismo que pinta el host Uno sobre su propio panel. Esta ventana sólo lo enseña y le pasa lo que
/// el usuario escribe: antes tenía esa lógica en su code-behind, y por eso ningún otro host podía ofrecer el
/// gestor sin reescribirla.</para>
///
/// <para><b>Lo que la vista sí decide</b>, porque es del host: cerrar la ventana (el botón «Cerrar»), que es
/// lo único que no puede pedir el view model —no es dueño de la ventana que lo contiene—.</para>
/// </summary>
public partial class MediaPresetManagerWindow : Avalonia.Controls.Window
{
    private readonly MediaPresetManagerViewModel _vm;

    public MediaPresetManagerWindow() : this(null)
    {
    }

    /// <summary>
    /// La ventana del gestor. Sin view model se construye uno con los dobles declarados (almacén de presets y
    /// avisos nulos): es el camino del diseñador de XAML y de las capturas visuales, no el del producto —el
    /// producto entra por la superficie que declara el nodo y le pasa su view model con sus diálogos—.
    /// </summary>
    public MediaPresetManagerWindow(MediaPresetManagerViewModel? viewModel = null)
    {
        _vm = viewModel ?? new MediaPresetManagerViewModel(
            MediaPresetManagerService.Instance,
            NullDialogService.Instance);

        InitializeComponent();
        DataContext = _vm;
    }

    /// <summary>El view model portable en uso (lo leen las capturas y las pruebas del plugin).</summary>
    public MediaPresetManagerViewModel ViewModel => _vm;

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }
}
