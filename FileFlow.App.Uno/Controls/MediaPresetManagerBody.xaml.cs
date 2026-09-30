using System;
using FileFlow.Plugin.Integrations.UI.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El GESTOR DE PRESETS DE MEDIOS del host Uno: la vista del
/// <see cref="MediaPresetManagerViewModel"/> portable del plugin de integraciones.
///
/// <para><b>Qué es y qué no.</b> El view model —con la lista de presets, el formulario del elegido y las
/// cuatro órdenes que los tocan— vive en el plugin y lo comparten los dos hosts; lo que cambia es quién lo
/// pinta. El host original montaba la ventana del plugin (<c>MediaPresetManagerWindow</c>); este host
/// pinta esta vista sobre el MISMO view model, que escribe en el MISMO almacén (el que lee el motor de
/// transcodificación). No hay una segunda versión del gestor: hay dos vistas de él.</para>
///
/// <para><b>Lo que la vista sí decide</b>, porque es del host: el cierre del modal, que se le pide a
/// <see cref="Platform.UnoWindowService"/> —esta vista vive dentro de su diálogo y no es dueña de él—. Ni un
/// cuadro de esta vista escribe en el almacén de presets: eso lo hace el view model.</para>
/// </summary>
public sealed partial class MediaPresetManagerBody : UserControl
{
    private readonly MediaPresetManagerViewModel _vm;

    public MediaPresetManagerBody(MediaPresetManagerViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        DataContext = _vm;

        RefreshLocalization();
    }

    /// <summary>El view model portable en uso (la sonda lee de aquí el catálogo y el preset elegido).</summary>
    internal MediaPresetManagerViewModel Vm => _vm;

    /// <summary>El catálogo de presets (lo que el canal externo lee como filas).</summary>
    internal ListView PresetRows => PresetList;

    /// <summary>El cuadro del nombre del preset elegido (el texto que la vista enseña).</summary>
    internal TextBox NameEditor => NameBox;

    /// <summary>El cuadro de la extensión de salida (donde se mide la normalización del punto).</summary>
    internal TextBox ExtensionEditor => ExtensionBox;

    /// <summary>El cuadro de la descripción: donde la sonda escribe y comprueba que llegó al almacén.</summary>
    internal TextBox DescriptionEditor => DescriptionBox;

    /// <summary>El botón de guardar del formulario (el que la sonda pulsa como lo pulsaría el usuario).</summary>
    internal Button SaveAction => SaveButton;

    /// <summary>El botón de alta (la sonda da de alta un preset para poder preguntar por su borrado).</summary>
    internal Button NewAction => NewButton;

    /// <summary>El botón de borrado: la orden DESTRUCTIVA cuya confirmación mide la sonda.</summary>
    internal Button DeleteAction => DeleteButton;

    /// <summary>El botón de restablecer: la otra orden destructiva —vacía el catálogo del usuario— y la
    /// segunda que tiene que preguntar.</summary>
    internal Button ResetAction => ResetButton;

    /// <summary>
    /// Cierra la superficie. El modal lo posee el servicio de ventanas —esta vista vive dentro de él—, así que
    /// el pie le pide que lo retire, igual que los pies de las demás ventanas del host.
    /// </summary>
    private void OnCloseClicked(object sender, RoutedEventArgs e) => Platform.UnoWindowService.CloseActiveWindow();

    /// <summary>Los rótulos del host (se reescriben en caliente al cambiar de idioma).</summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        HeaderTitle.Text = loc.GetString("PresetManager_HeaderTitle", "Gestor de Presets de Medios");
        HeaderDesc.Text = loc.GetString("PresetManager_HeaderSubtitle", "");
        ResetLabel.Text = loc.GetString("PresetManager_ResetBtn", "Restablecer");

        NewLabel.Text = loc.GetString("PresetManager_NewBtn", "Nuevo");
        DeleteLabel.Text = loc.GetString("PresetManager_DeleteBtn", "Eliminar");

        FormTitle.Text = loc.GetString("PresetManager_FormTitle", "Detalles del Preset");
        NameLabel.Text = loc.GetString("PresetManager_PresetName", "Nombre del Preset");
        CategoryLabel.Text = loc.GetString("PresetManager_Category", "Categoría");
        ExtensionLabel.Text = loc.GetString("PresetManager_OutputExt", "Extensión de Salida");
        DescriptionLabel.Text = loc.GetString("PresetManager_Description", "Descripción");
        FfmpegLabel.Text = loc.GetString("PresetManager_FfmpegArgs", "Argumentos CLI de FFmpeg");
        SaveLabel.Text = loc.GetString("PresetManager_SavePreset", "Guardar Preset");

        CloseLabel.Text = loc.GetString("Common_Close", "Cerrar");
    }
}
