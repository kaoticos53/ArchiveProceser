using System;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El editor de URLs de descarga de un modelo de IA del host Uno: la vista del
/// <see cref="AiModelUrlsConfigViewModel"/> portable —el MISMO view model que envuelve la ventana
/// <c>AiModelUrlsConfigDialog</c> de la versión anterior, y al que se llega por la MISMA clave del catálogo
/// (<c>DialogKeys.AiModelUrlsConfig</c>) desde la MISMA orden canónica del gestor
/// (<c>AiModelManagerViewModel.ConfigureUrlsCommand</c>, que dispara la acción de la fila)—.
///
/// <para><b>Dónde queda escrito el cambio</b>: en el mismo sitio que en la versión anterior, porque lo escribe el
/// propio view model portable —su método <c>Save()</c> llama a <c>AiModelManager.SetCustomUrls(modelId, urls)</c>,
/// que es el almacén que el motor de descargas lee—. Esta vista no guarda nada por su cuenta: pinta el
/// <c>UrlsText</c> en dos sentidos, ofrece las tres órdenes del view model (probar, restablecer y guardar) y
/// devuelve el resultado de guardar al servicio de ventanas, que es quien cierra el modal.</para>
///
/// <para><b>Por qué las órdenes de probar y restablecer son botones de este cuerpo y guardar no</b>: probar y
/// restablecer no cierran nada, así que son los comandos del view model en su sitio; <i>guardar</i> sí cierra
/// —y sólo si el view model acepta las URLs—, así que vive en el botón primario del diálogo y su resultado lo
/// lee el servicio (ver <c>UnoWindowService.ShowAiModelUrlsAsync</c>).</para>
/// </summary>
public sealed partial class AiModelUrlsConfigBody : UserControl
{
    private readonly AiModelUrlsConfigViewModel _vm;

    public AiModelUrlsConfigBody(AiModelUrlsConfigViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        DataContext = _vm;
        RefreshLocalization();
    }

    /// <summary>El view model portable que esta vista sirve (la sonda lee de aquí lo que enseña).</summary>
    internal AiModelUrlsConfigViewModel Vm => _vm;

    /// <summary>La caja de URLs (lo que un lector de pantalla o el driver externo encuentran y editan).</summary>
    internal TextBox UrlsEditor => UrlsBox;

    /// <summary>El recuento de URLs que el view model calcula de lo que hay escrito.</summary>
    internal string CountText => UrlsCountText.Text;

    /// <summary>El distintivo de estado: personalizado u oficial (la palabra la pone el view model).</summary>
    internal string BadgeText => StatusBadgeText.Text;

    /// <summary>El nombre del modelo cuya configuración se está editando.</summary>
    internal string ModelNameShown => ModelNameText.Text;

    /// <summary>El aviso que el editor enseña cuando no acepta lo escrito (vacío hasta que se intente).</summary>
    internal string RefusalNote => RequiredNote.Visibility == Visibility.Visible ? RequiredNote.Text : string.Empty;

    /// <summary>
    /// Pide al view model que escriba lo escrito donde lo escribe la versión anterior y devuelve si lo aceptó:
    /// es lo que el servicio usa para cerrar (o no) el modal. La lógica es del view model; aquí sólo se
    /// llama a la misma operación que su ventana de la versión anterior.
    ///
    /// <para><b>Por qué el aviso se repite aquí</b>: el view model pide su aviso por <see cref="IDialogService"/>
    /// —la misma vía que la versión anterior—, y en este host esa petición cae en la frontera ya medida de WinUI:
    /// no se puede abrir un segundo <c>ContentDialog</c> encima del editor, así que la petición queda escrita
    /// en la consola pero no se ve. Sin esta nota, rechazar lo escrito dejaría la ventana abierta sin decir
    /// por qué: un no-op mudo. El texto es la MISMA frase que el view model manda al servicio de diálogos,
    /// copiada al diccionario del host; no hay una segunda validación ni una segunda redacción.</para>
    /// </summary>
    internal bool TryCommit()
    {
        bool accepted = _vm.Save();
        if (!accepted)
        {
            RequiredNote.Text = LocalizationManager.Instance.GetString("Uno_AiModelUrls_RequiredWarning",
                "Debe especificar al menos una URL de descarga válida para este modelo.");
            RequiredNote.Visibility = Visibility.Visible;
        }

        return accepted;
    }

    /// <summary>Los rótulos del host, con las claves del diccionario de la versión anterior (los mismos textos).</summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        SubtitleText.Text = loc.GetString("AiModelUrls_Subtitle",
            "Configure una o varias URLs de descarga para este modelo.");
        UrlsLabel.Text = loc.GetString("AiModelUrls_UrlsLabel", "URLs de descarga (una por línea, en orden de prioridad):");
        TestLabel.Text = loc.GetString("AiModelUrls_TestBtn", "🔍 Probar Conexión");
        ResetLabel.Text = loc.GetString("AiModelUrls_ResetBtn", "🔄 Restablecer Predeterminadas");
    }
}
