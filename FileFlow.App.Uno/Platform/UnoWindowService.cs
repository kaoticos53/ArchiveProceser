using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.App.Services;
using FileFlow.App.Uno.Controls;
using FileFlow.App.ViewModels;
using FileFlow.Plugin.Archives.UI.ViewModels;
using FileFlow.Plugin.FileSystem.UI.ViewModels;
using FileFlow.Plugin.Integrations.UI.ViewModels;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.VirtualFileSystem;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// El catálogo de VENTANAS Y DIÁLOGOS del host Uno: la implementación de <see cref="IWindowService"/>
/// que los ViewModels del núcleo consultan pidiendo un diálogo por su clave (<see cref="DialogKeys"/>).
/// Es el hermano Uno de <c>AvaloniaWindowService</c> y la pieza que faltaba para que el botón «{x}» de
/// un parámetro y el editor enriquecido de un texto dejasen de caer en el Nulo declarado.
///
/// <para><b>Qué implementa y con qué.</b> Los tres diálogos que un usuario puede alcanzar en este host
/// —el EDITOR DE TEXTO (payload: el <c>NodeParameterViewModel</c> que se está editando), el SELECTOR
/// DE VARIABLES (payload: el <c>VariablePickerRequest</c> del núcleo) y la ventana ACERCA DE (hito 258:
/// la pide el comando canónico del menú por <see cref="ShowWindow"/>)— se sirven con el modal nativo
/// (<see cref="ContentDialog"/>, con su foco, su Escape y su accesibilidad hechos); las dos primeras
/// sobre los view models PORTABLES del núcleo y la tercera sobre la superficie del host, porque el
/// contenido de «Acerca de» no lo dicta ningún view model.</para>
///
/// <para><b>Qué NO implementa, y por qué se declara.</b> Todo lo demás de <see cref="DialogKeys"/> son
/// ventanas que este host no tiene (Estudio de temas, Métricas, VFS, actualización) o
/// caminos del núcleo que ninguna vista del host dibuja (los ajustes tienen aquí su propia superficie
/// desde el hito 255, y abrirla por este camino sería una SEGUNDA copia de lo mismo). Un diálogo que se
/// pide y no existe no puede contestar «cancelado» en silencio: se registra en
/// <see cref="DeclinedDialogs"/> y se escribe en la consola de la aplicación, y la tabla
/// <see cref="DeclaredPendingDialogs"/> obliga a que la lista esté completa —la guardia la compara con
/// las claves de <see cref="DialogKeys"/>, así que una clave nueva sin destino cae allí—.</para>
/// </summary>
public sealed class UnoWindowService : IWindowService
{
    /// <summary>
    /// Los diálogos que este host SIRVE, con la vista que los sirve. Es la mitad positiva de la tabla:
    /// la guardia exige que implementados + declarados cubran todas las claves de <see cref="DialogKeys"/>.
    /// </summary>
    public static readonly (string Key, string View)[] ImplementedDialogs =
    [
        (DialogKeys.TextEditor, nameof(TextEditorDialogBody)),
        (DialogKeys.VariablePicker, nameof(VariablePickerDialogBody)),
        (DialogKeys.About, nameof(AboutDialogBody)),
        (DialogKeys.UpdateDialog, nameof(UpdateDialogBody)),
        (DialogKeys.VirtualFileSystemExplorer, nameof(VirtualFileSystemExplorerBody)),
        (DialogKeys.WorkflowMetricsDashboard, nameof(MetricsDashboardBody)),
        (DialogKeys.ThemeCustomizer, nameof(ThemeCustomizerBody)),
        (DialogKeys.DataSetDesigner, nameof(DataSetDesignerBody)),
        (DialogKeys.AiModelUrlsConfig, nameof(AiModelUrlsConfigBody)),
        (DialogKeys.MediaPresetManager, nameof(MediaPresetManagerBody)),
        (DialogKeys.PasswordManager, nameof(PasswordManagerBody)),
    ];

    /// <summary>
    /// Los diálogos que este host NO sirve, cada uno con su razón. Es el otro lado del censo: lo que
    /// no llega queda declarado, nunca fingido — un «cancelado» mudo se leería como un error del
    /// usuario.
    /// </summary>
    public static readonly (string Key, string Reason)[] DeclaredPendingDialogs =
    [
        (DialogKeys.WorkflowSettings, "la superficie de ajustes de este host (hito 255) tiene su punto de entrada en la barra y el cajón; abrirla por este camino sería una SEGUNDA copia de lo mismo"),
    ];

    /// <summary>Los diálogos que se han pedido y este host no sirve, en orden (la traza de lo declarado).</summary>
    public static IReadOnlyList<string> DeclinedDialogs => _declined;

    private static readonly List<string> _declined = [];
    private static readonly Lock _lock = new();

    /// <summary>El diálogo abierto AHORA, si lo hay (lo lee la sonda: es la prueba de que abrió).</summary>
    internal static ContentDialog? ActiveDialog { get; private set; }

    /// <summary>El cuerpo del editor abierto (null si el diálogo abierto no es ese).</summary>
    internal static TextEditorDialogBody? ActiveEditor => Body<TextEditorDialogBody>();

    /// <summary>El cuerpo del selector abierto (null si el diálogo abierto no es ese).</summary>
    internal static VariablePickerDialogBody? ActivePicker => Body<VariablePickerDialogBody>();

    /// <summary>El cuerpo del gestor de presets abierto (null si el diálogo abierto no es ese).</summary>
    internal static MediaPresetManagerBody? ActivePresetManager => Body<MediaPresetManagerBody>();

    /// <summary>El cuerpo del gestor de contraseñas abierto (null si el diálogo abierto no es ese).</summary>
    internal static PasswordManagerBody? ActivePasswordManager => Body<PasswordManagerBody>();

    /// <summary>
    /// El cuerpo del tipo pedido, dentro del modal abierto. Mientras hay una PREGUNTA en pantalla el cuerpo vive
    /// envuelto en su capa (<see cref="AskInsideActiveDialogAsync"/>), así que se busca también ahí: el cuerpo no
    /// desaparece porque el host pregunte, y una sonda que no lo encontrara durante la pregunta mediría el hueco
    /// en vez del producto.
    /// </summary>
    private static T? Body<T>() where T : class
    {
        if (ActiveDialog?.Content is T direct)
        {
            return direct;
        }

        return ActiveDialog?.Content is Panel wrapper ? wrapper.Children.OfType<T>().FirstOrDefault() : null;
    }

    /// <summary>
    /// La CLAVE del catálogo de la superficie abierta AHORA (null si no hay ninguna). Es lo que la sonda lee
    /// para saber qué VENTANA se abrió —una lectura por clave, no por tipo de cuerpo—, y lo que deja medir
    /// cada ventana nueva sin añadir una propiedad por cada una.
    /// </summary>
    internal static string? ActiveWindowKey { get; private set; }

    public object? MainWindowOwner => App.MainWindow;

    public async Task<DialogResultPayload?> ShowDialogAsync(string dialogKey, object? payload = null)
    {
        XamlRoot? root = (App.MainWindow?.Content as FrameworkElement)?.XamlRoot;
        if (root is null)
        {
            Decline(dialogKey, "no hay raíz visual todavía (la ventana no está montada)");
            return new DialogResultPayload { Confirmed = false };
        }

        switch (dialogKey)
        {
            case DialogKeys.TextEditor when payload is NodeParameterViewModel parameter:
                return await ShowTextEditorAsync(parameter, root);

            case DialogKeys.VariablePicker:
                return await ShowVariablePickerAsync(payload as VariablePickerRequest, root);

            case DialogKeys.UpdateDialog when payload is UpdateDialogViewModel update:
                return await ShowUpdateDialogAsync(update, root);

            case DialogKeys.UpdateDialog:
                Decline(dialogKey, "esperaba el view model del aviso de actualización (UpdateDialogViewModel) como carga útil y llegó "
                    + (payload?.GetType().Name ?? "null"));
                return new DialogResultPayload { Confirmed = false };

            // El GESTOR DE PRESETS (hito 263): la orden de la fila del preset y la acción personalizada de la
            // tarjeta del nodo lo piden por AQUÍ —esperan la vuelta para resincronizar los parámetros— con el
            // view model PORTABLE que declara el nodo de transcodificación como carga útil.
            case DialogKeys.MediaPresetManager when payload is MediaPresetManagerViewModel presetManager:
                return await ShowMediaPresetManagerAsync(presetManager, root);

            case DialogKeys.MediaPresetManager:
                Decline(dialogKey, "el gestor de presets necesita el view model portable que declara el nodo "
                    + "(MediaPresetManagerViewModel) como carga útil y llegó "
                    + (payload?.GetType().Name ?? "null"));
                return new DialogResultPayload { Confirmed = false };

            // El GESTOR DE CONTRASEÑAS: la orden de la fila del parámetro y la acción personalizada de la
            // tarjeta del nodo lo piden por AQUÍ —esperan la vuelta para resincronizar los parámetros— con el
            // view model PORTABLE que declaran los nodos de descompresión del plugin de archivos.
            case DialogKeys.PasswordManager when payload is PasswordManagerViewModel passwordManager:
                return await ShowPasswordManagerAsync(passwordManager, root);

            case DialogKeys.PasswordManager:
                Decline(dialogKey, "el gestor de contraseñas necesita el view model portable que declara el nodo "
                    + "(PasswordManagerViewModel) como carga útil y llegó "
                    + (payload?.GetType().Name ?? "null"));
                return new DialogResultPayload { Confirmed = false };

            // El DISEÑADOR DE DATASETS por el canal que espera respuesta: lo pide el BOTÓN DE LA TARJETA del nodo
            // de datos sintéticos (su acción personalizada). El cajón llega por ShowWindow; las dos puertas
            // sirven el mismo cuerpo sobre el mismo view model.
            case DialogKeys.DataSetDesigner when payload is SyntheticDataSetDesignerViewModel declaredDesigner:
                return await ShowDeclaredDesignerAsync(declaredDesigner, root);

            // El editor de URLs por modelo (hito 262): la orden canónica del gestor de modelos lo pide por
            // AQUÍ —espera respuesta— con el id del modelo como carga útil.
            case DialogKeys.AiModelUrlsConfig when payload is string modelId:
                return await ShowAiModelUrlsAsync(modelId, root);

            case DialogKeys.AiModelUrlsConfig:
                Decline(dialogKey, "la edición de URLs por modelo necesita el id del modelo como carga útil y llegó "
                    + (payload?.GetType().Name ?? "null"));
                return new DialogResultPayload { Confirmed = false };

            default:
                Decline(dialogKey, "no servido por este host");
                return new DialogResultPayload { Confirmed = false };
        }
    }

    /// <summary>
    /// Las ventanas que este host sirve como superficie modal dentro de su propia ventana, más lo que
    /// todavía no tiene.
    ///
    /// <para><b>Por qué «Acerca de» es una ventana servida y no una declarada</b>: la orden canónica del
    /// menú del escritorio (<c>OpenAboutDialogCommand</c>) la pide por aquí, y no lleva payload —el
    /// contenido lo pone el host—, así que portar la entrada <i>es</i> servir esta clave. El host no abre
    /// una segunda ventana de Windows: la sirve como superficie modal dentro de la suya, que es la misma
    /// información en el mismo sitio donde el usuario ya está mirando. <b>Diferencia declarada</b>: el
    /// escritorio la abre como ventana NO modal y aquí es modal.</para>
    ///
    /// <para>Las demás (Estudio de temas, Métricas, Explorador VFS, aviso de actualización —hito 260— y
    /// Diseñador de Datasets —hito 261—) tienen ya su vista sobre el view model portable, así que se sirven
    /// aquí igual. Lo que no tiene a dónde ir se declina <b>con su motivo</b>, que queda escrito —nunca en
    /// silencio—.</para>
    /// </summary>
    public void ShowWindow(string dialogKey, object? payload = null)
    {
        switch (dialogKey)
        {
            case DialogKeys.About:
                _ = ShowAboutAsync();
                return;

            case DialogKeys.WorkflowMetricsDashboard when payload is WorkflowMetricsDashboardViewModel metrics:
                _ = ShowSurfaceAsync(dialogKey, metrics, () => new MetricsDashboardBody(metrics),
                    "MetricsDashboard_Title", "Métricas y Rendimiento", "MetricsDashboard", width: 980);
                return;

            case DialogKeys.ThemeCustomizer when payload is ThemeCustomizerViewModel studio:
                _ = ShowSurfaceAsync(dialogKey, studio, () => new ThemeCustomizerBody(studio),
                    "ThemeCustomizer_WindowTitle", "Estudio de Personalización de Temas", "ThemeStudio", width: 940);
                return;

            case DialogKeys.VirtualFileSystemExplorer when payload is IVirtualFileSystemStore store:
                _ = ShowSurfaceAsync(dialogKey, store, () => new VirtualFileSystemExplorerBody(store),
                    "VfsExplorer_Title", "Explorador de Archivos Virtual", "VfsExplorer", width: 880);
                return;

            case DialogKeys.DataSetDesigner when payload is SyntheticDataSetDesignerViewModel designer:
                _ = ShowSurfaceAsync(dialogKey, designer, () => new DataSetDesignerBody(designer),
                    "DataSetDesigner_WindowTitle", "Diseñador de Conjuntos de Datos Sintéticos", "DataSetDesigner",
                    width: 1010);
                return;

            case DialogKeys.DataSetDesigner:
                Decline(dialogKey, "esperaba el view model del diseñador de datasets (SyntheticDataSetDesignerViewModel) "
                    + "como carga útil y llegó " + (payload?.GetType().Name ?? "null"));
                return;

            // El camino del núcleo que pide una ventana que este host no dibuja —el explorador de temas por
            // este canal, o cualquier clave futura—: se declina con su motivo, nunca en silencio.
            default:
                Decline(dialogKey, "este host no tiene ventanas de ese tipo, o la carga útil no es la que esa ventana espera (llegó "
                    + (payload?.GetType().Name ?? "null") + ")");
                return;
        }
    }

    /// <summary>
    /// Muestra una de las VENTANAS del host como superficie dentro de la suya (el mismo criterio de «Acerca
    /// de»): un <see cref="ContentDialog"/> con su foco, su Escape y su accesibilidad hechos, el cuerpo que se
    /// le pasa y la clave del catálogo como ancla de automatización —así el canal externo y la sonda ven QUÉ
    /// ventana se abrió y no sólo que hay un modal—.
    ///
    /// <para>Si ya hay un diálogo abierto (WinUI sólo admite uno a la vez) o el árbol no está montado, se
    /// declina con su motivo en vez de caer: es la misma frontera declarada del resto del servicio.</para>
    /// </summary>
    private static async Task ShowSurfaceAsync(
        string dialogKey,
        object? payload,
        Func<FrameworkElement> bodyFactory,
        string titleKey,
        string titleFallback,
        string automationId,
        double width)
    {
        _ = payload; // la carga útil ya la consumió la fábrica del cuerpo; aquí sólo se comprueba su TIPO antes

        XamlRoot? root = (App.MainWindow?.Content as FrameworkElement)?.XamlRoot;
        if (root is null)
        {
            Decline(dialogKey, "no hay raíz visual todavía (la ventana no está montada)");
            return;
        }

        if (ActiveDialog is not null)
        {
            Decline(dialogKey, "ya hay un diálogo abierto en este host (WinUI sólo admite uno)");
            return;
        }

        FrameworkElement body = bodyFactory();
        body.Width = width;

        var dialog = new ContentDialog
        {
            Title = LocalizationManager.Instance.GetString(titleKey, titleFallback),
            Content = body,
            CloseButtonText = LocalizationManager.Instance.GetString("Common_Close", "Cerrar"),
            XamlRoot = root,
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(dialog, automationId);

        await RunAsync(dialog, dialogKey);
    }

    /// <summary>
    /// El aviso de actualización: el único de los cuatro que el núcleo pide por <see cref="ShowDialogAsync"/>,
    /// porque su view model espera una respuesta (la instalación manda cerrar). Sus tres órdenes son las del
    /// view model; el cierre lo pide él por <c>RequestClose</c> y aquí se traduce en retirar el modal.
    /// </summary>
    private static async Task<DialogResultPayload?> ShowUpdateDialogAsync(UpdateDialogViewModel update, XamlRoot root)
    {
        bool accepted = false;
        var body = new UpdateDialogBody(update);
        var dialog = new ContentDialog
        {
            Title = LocalizationManager.Instance.GetString("Update_WindowTitle", "Actualización Disponible"),
            Content = body,
            CloseButtonText = LocalizationManager.Instance.GetString("Common_Close", "Cerrar"),
            XamlRoot = root,
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(dialog, "UpdateDialog");

        void OnRequestClose(bool confirm)
        {
            accepted = confirm;
            dialog.Hide();
        }

        update.RequestClose += OnRequestClose;
        try
        {
            await RunAsync(dialog, DialogKeys.UpdateDialog);
            return new DialogResultPayload { Confirmed = accepted };
        }
        finally
        {
            update.RequestClose -= OnRequestClose;
        }
    }

    /// <summary>
    /// El editor de URLs de descarga de un modelo (hito 262): la vista del
    /// <see cref="AiModelUrlsConfigViewModel"/> portable —la MISMA que envuelve el
    /// <c>AiModelUrlsConfigDialog</c> del escritorio—, pedida por la MISMA orden canónica del gestor
    /// (<c>AiModelManagerViewModel.ConfigureUrlsCommand</c>, la del botón de la fila) y con la MISMA carga
    /// útil (el id del modelo).
    ///
    /// <para><b>Dónde queda escrito el cambio</b>: en el mismo almacén que en el escritorio, porque lo
    /// escribe el view model portable —su <c>Save()</c> llama a <c>AiModelManager.SetCustomUrls(modelId, urls)</c>,
    /// que es lo que lee el motor de descargas—. El botón primario es ese <c>Save()</c>: si el view model
    /// <b>rechaza</b> lo escrito (ninguna URL válida), el diálogo NO se cierra y el cuerpo dice por qué (la
    /// petición de aviso del view model cae en la frontera ya medida de WinUI —un solo <c>ContentDialog</c>—
    /// así que el host la repite dentro del editor); si lo acepta, se cierra confirmado y el gestor refresca
    /// el estado de la fila con esa respuesta, como el escritorio.</para>
    /// </summary>
    private static async Task<DialogResultPayload?> ShowAiModelUrlsAsync(string modelId, XamlRoot root)
    {
        var loc = LocalizationManager.Instance;
        var body = new AiModelUrlsConfigBody(
            new AiModelUrlsConfigViewModel(modelId, new UnoDialogService(), loc));

        var dialog = new ContentDialog
        {
            Title = loc.GetString("AiModelUrls_Title", "Configurar URLs de Descarga"),
            Content = body,
            PrimaryButtonText = loc.GetString("AiModelUrls_SaveBtn", "💾 Guardar URLs"),
            CloseButtonText = loc.GetString("AiModelUrls_CancelBtn", "Cancelar"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(dialog, DialogKeys.AiModelUrlsConfig);

        dialog.PrimaryButtonClick += (_, args) =>
        {
            // Guardar SÓLO cierra si el view model aceptó lo escrito: cerrar sobre algo rechazado dejaría
            // al usuario creyendo que su edición quedó guardada.
            if (!body.TryCommit())
            {
                args.Cancel = true;
            }
        };

        ContentDialogResult result = await RunAsync(dialog, DialogKeys.AiModelUrlsConfig);
        return new DialogResultPayload { Confirmed = result == ContentDialogResult.Primary };
    }

    /// <summary>
    /// El GESTOR DE PRESETS DE MEDIOS (hito 263): la vista del <see cref="MediaPresetManagerViewModel"/> portable
    /// —el MISMO view model con el que el escritorio monta su ventana—, pedida por la MISMA acción que el
    /// botón «🎬» de la fila del preset y el de la tarjeta del nodo (<c>ManageMediaPresets</c>).
    ///
    /// <para><b>Dónde queda escrito el cambio</b>: en el mismo almacén que en el escritorio, porque lo escribe
    /// el view model portable —sus órdenes llaman a <c>MediaPresetManagerService</c>, el que lee el motor de
    /// transcodificación—. Esta vista no toca el almacén: sólo enseña lo que el view model publica.</para>
    ///
    /// <para><b>Qué cierra el modal</b>: el pie «Cerrar» del cuerpo (el usuario cierra cuando termina) y el
    /// botón de cierre del propio diálogo. Guardar NO cierra: los avisos del gestor son del view model y los
    /// sirve el mismo servicio de diálogos del host, así que la superficie se queda donde el usuario está.
    /// </para>
    /// </summary>
    private static async Task<DialogResultPayload?> ShowMediaPresetManagerAsync(MediaPresetManagerViewModel presetManager, XamlRoot root)
    {
        var loc = LocalizationManager.Instance;
        var body = new MediaPresetManagerBody(presetManager);

        var dialog = new ContentDialog
        {
            Title = loc.GetString("PresetManager_WindowTitle", "Gestor de Presets de Medios"),
            Content = body,
            CloseButtonText = loc.GetString("Common_Close", "Cerrar"),
            XamlRoot = root,
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(dialog, DialogKeys.MediaPresetManager);

        await RunAsync(dialog, DialogKeys.MediaPresetManager);
        return new DialogResultPayload { Confirmed = true };
    }

    /// <summary>
    /// El GESTOR DE CONTRASEÑAS: la vista del <see cref="PasswordManagerViewModel"/> portable —el MISMO view model
    /// con el que el escritorio monta su ventana—, pedida por la MISMA acción que el botón de la fila del
    /// parámetro y el de la tarjeta del nodo (<c>ManagePasswords</c>).
    ///
    /// <para><b>Dónde queda escrito el cambio</b>: en el parámetro <c>PasswordList</c> del nodo, porque lo escribe
    /// el view model portable —su vuelta llama al nodo que lo declaró—. Esta vista no toca el nodo: sólo enseña lo
    /// que el view model publica.</para>
    ///
    /// <para><b>Qué cierra el modal</b>: su botón primario guarda la lista y el de cerrar (o Escape) descarta, que
    /// son las dos salidas del escritorio; el guardado es del view model y no de esta vista.</para>
    /// </summary>
    private static async Task<DialogResultPayload?> ShowPasswordManagerAsync(PasswordManagerViewModel manager, XamlRoot root)
    {
        var loc = LocalizationManager.Instance;
        var body = new PasswordManagerBody(manager);

        var dialog = new ContentDialog
        {
            Title = loc.GetString("PasswordManager_WindowTitle", "Gestor de Claves y Contraseñas"),
            Content = body,
            PrimaryButtonText = loc.GetString("PasswordManager_SaveKeys", "✅ Guardar Claves"),
            CloseButtonText = loc.GetString("Common_Cancel", "✕ Cancelar"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(dialog, DialogKeys.PasswordManager);

        ContentDialogResult result = await RunAsync(dialog, DialogKeys.PasswordManager);
        if (result != ContentDialogResult.Primary)
        {
            return new DialogResultPayload { Confirmed = false };
        }

        manager.Save();
        return new DialogResultPayload { Confirmed = true, Value = manager.PasswordsText };
    }

    /// <summary>
    /// El DISEÑADOR DE DATASETS por el canal que espera respuesta: es el que usa el BOTÓN DE LA TARJETA del nodo
    /// de datos sintéticos (su acción personalizada), que necesita saber cuándo se cerró para resincronizar sus
    /// parámetros. El cuerpo es el mismo que sirve el cajón (<see cref="DataSetDesignerBody"/> sobre el mismo
    /// view model portable): no hay dos diseñadores.
    /// </summary>
    private static async Task<DialogResultPayload?> ShowDeclaredDesignerAsync(SyntheticDataSetDesignerViewModel designer, XamlRoot root)
    {
        _ = root;
        await ShowSurfaceAsync(DialogKeys.DataSetDesigner, designer, () => new DataSetDesignerBody(designer),
            "DataSetDesigner_WindowTitle", "Diseñador de Conjuntos de Datos Sintéticos", "DataSetDesigner", width: 1010);

        return new DialogResultPayload { Confirmed = true };
    }

    /// <summary>
    /// La superficie «Acerca de»: el mismo contenido que la ventana del escritorio, con la versión de la
    /// misma fuente que el pie del cajón. Si no hay raíz visual, o si ya hay un diálogo abierto (WinUI no
    /// admite dos a la vez), se declina con su motivo en vez de caer.
    /// </summary>
    private static Task ShowAboutAsync() =>
        ShowSurfaceAsync(DialogKeys.About, null, () => new AboutDialogBody(),
            "Uno_About_Title", "Acerca de FileFlow Studio", "AboutDialog", width: 520);

    // ───────────────────────────────────────────────────────────────────────────────
    // Los dos diálogos servidos
    // ───────────────────────────────────────────────────────────────────────────────

    private static async Task<DialogResultPayload?> ShowTextEditorAsync(NodeParameterViewModel parameter, XamlRoot root)
    {
        var loc = LocalizationManager.Instance;
        var vm = new TextEditorDialogViewModel(
            parameter.DisplayName,
            parameter.Value?.ToString() ?? string.Empty,
            parameter,
            VariableDiscoveryService.Instance,
            loc);

        var body = new TextEditorDialogBody { DataContext = vm };
        var dialog = new ContentDialog
        {
            Title = loc.GetString("Uno_Dialog_TextEditor_Title", "Editor de Texto y Prompts"),
            Content = body,
            PrimaryButtonText = loc.GetString("Uno_Dialog_TextEditor_Save", "✓ Guardar y Aplicar"),
            CloseButtonText = loc.GetString("Uno_Dialog_Cancel", "✕ Cancelar"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };

        ContentDialogResult result = await RunAsync(dialog, DialogKeys.TextEditor);

        if (result != ContentDialogResult.Primary)
        {
            return new DialogResultPayload { Confirmed = false };
        }

        // El texto confirmado es el del view model, por su propio método (`SaveResult`): así lo lee
        // también el escritorio (`TextEditorDialogWindow.ResultText`).
        vm.SaveResult();
        return new DialogResultPayload { Confirmed = true, Value = vm.ResultText };
    }

    private static async Task<DialogResultPayload?> ShowVariablePickerAsync(VariablePickerRequest? request, XamlRoot root)
    {
        var loc = LocalizationManager.Instance;
        var vm = new VariablePickerViewModel(
            request?.Groups,
            request?.TargetNode,
            request?.PreviewContext,
            request?.Localization ?? loc);

        var body = new VariablePickerDialogBody { DataContext = vm };
        var dialog = new ContentDialog
        {
            Title = loc.GetString("Uno_Dialog_VarPicker_Title", "Catálogo de Variables y Expresiones"),
            Content = body,
            PrimaryButtonText = loc.GetString("Uno_Dialog_VarPicker_Insert", "Insertar Variable"),
            CloseButtonText = loc.GetString("Uno_Dialog_Cancel", "✕ Cancelar"),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = vm.HasSelectedVariable,
            XamlRoot = root,
        };

        // El botón sólo está vivo cuando hay algo que insertar (el mismo criterio que el escritorio,
        // que deshabilita el suyo con el aviso «selecciona una variable»).
        void OnVmPropertyChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VariablePickerViewModel.HasSelectedVariable))
            {
                dialog.IsPrimaryButtonEnabled = vm.HasSelectedVariable;
            }
        }

        // El doble clic del catálogo es el `InsertSelectedCommand` del view model: cierra aceptando.
        bool acceptedByRows = false;
        void OnRequestClose(object? _, bool accepted)
        {
            if (accepted)
            {
                acceptedByRows = true;
                dialog.Hide();
            }
        }

        vm.PropertyChanged += OnVmPropertyChanged;
        vm.RequestClose += OnRequestClose;
        try
        {
            ContentDialogResult result = await RunAsync(dialog, DialogKeys.VariablePicker);
            if (result != ContentDialogResult.Primary && !acceptedByRows)
            {
                return new DialogResultPayload { Confirmed = false };
            }

            string token = vm.SelectedToken;
            if (string.IsNullOrEmpty(token))
            {
                return new DialogResultPayload { Confirmed = false };
            }

            return new DialogResultPayload { Confirmed = true, Value = token };
        }
        finally
        {
            vm.PropertyChanged -= OnVmPropertyChanged;
            vm.RequestClose -= OnRequestClose;
        }
    }

    /// <summary>
    /// Muestra el diálogo y deja constancia de CUÁL está abierto mientras lo está: es el estado que la
    /// sonda lee para probar que la orden abrió de verdad un diálogo (y para pulsar su botón primario,
    /// el control real y no su efecto).
    /// </summary>
    private static async Task<ContentDialogResult> RunAsync(ContentDialog dialog, string dialogKey)
    {
        ActiveWindowKey = dialogKey;
        return await ShowOwnedModalAsync(dialog);
    }

    /// <summary>
    /// Muestra y PUBLICA un aviso del host que no es una ventana del catálogo (el aviso de la frontera del
    /// escritorio, hito 270): deja el mismo estado de «hay un modal abierto» que <see cref="RunAsync"/>, y sin
    /// él el host no veía el aviso. Dos consecuencias medidas, las dos mudas: un segundo
    /// <see cref="ContentDialog"/> (WinUI admite uno) quedaba detectado contra un estado que nadie escribía
    /// —<c>UnoDialogService.ShowCoreAsync</c> consulta <see cref="ActiveDialog"/> antes de abrir— y una sonda
    /// no podía distinguir «el aviso se mostró» de «no pasó nada», que es exactamente el defecto que este
    /// tramo cierra. La clave del catálogo queda nula: un aviso no es una ventana servida.
    /// </summary>
    internal static Task<ContentDialogResult> RunOwnedAsync(ContentDialog dialog)
        => ShowOwnedModalAsync(dialog);

    /// <summary>
    /// El sobre de TODO modal del host —una ventana del catálogo (<see cref="RunAsync"/>) y un aviso que no lo
    /// es (<see cref="RunOwnedAsync"/>), para que publicar y retirar no se escriba dos veces—: publica CUÁL
    /// está abierto mientras lo está y lo retira al irse, con la pregunta que pudiera quedarle dentro
    /// contestada «no».
    /// </summary>
    private static async Task<ContentDialogResult> ShowOwnedModalAsync(ContentDialog dialog)
    {
        ActiveDialog = dialog;
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            // Un modal puede irse con la pregunta en pantalla (Escape, o su botón de cerrar): nadie la
            // contesta, así que se retira aquí. Sin esto la pregunta quedaba viva para siempre —el host
            // rechazaba TODA pregunta posterior, incluidas las de borrado— y la orden que la esperaba no
            // terminaba nunca (su botón se quedaba deshabilitado para el resto de la sesión).
            TearDownInlineQuestion(false);
            ActiveDialog = null;
            ActiveWindowKey = null;
        }
    }

    /// <summary>
    /// Retira la superficie abierta (lo que hace el usuario con su botón de cerrar o con Escape). Lo piden
    /// los pies de las ventanas del host, que no son dueñas del modal que las contiene.
    /// </summary>
    internal static void CloseActiveWindow() => ActiveDialog?.Hide();

    // ───────────────────────────────────────────────────────────────────────────────
    // La PREGUNTA dentro del modal abierto (hito 263)
    // ───────────────────────────────────────────────────────────────────────────────

    private static TaskCompletionSource<bool>? s_inlineAnswer;
    private static Button? s_inlineAccept;
    private static Button? s_inlineCancel;
    private static Panel? s_inlineHost;
    private static Grid? s_inlineLayer;

    /// <summary>¿Hay una pregunta del host en pantalla ahora mismo?</summary>
    internal static bool IsAskingInline => s_inlineAnswer is not null;

    /// <summary>El botón de aceptar de la pregunta en pantalla (la sonda la contesta como el usuario).</summary>
    internal static Button? ActiveConfirmationAccept => s_inlineAccept;

    /// <summary>El botón de cancelar de la pregunta (nulo si la pregunta sólo se acepta).</summary>
    internal static Button? ActiveConfirmationCancel => s_inlineCancel;

    /// <summary>
    /// Pregunta al usuario DENTRO del modal que ya está abierto, y devuelve su respuesta REAL sin bloquear el
    /// hilo de UI.
    ///
    /// <para><b>Por qué dentro y no en otro modal.</b> WinUI sólo admite un <see cref="ContentDialog"/> por
    /// raíz: cuando el gestor de presets —que ES un modal del host— pide confirmar un borrado, un segundo
    /// <c>ContentDialog</c> no se puede mostrar, y una confirmación que no se puede mostrar es una orden que
    /// no borra y tampoco avisa (o, con un servicio que responde «sí» sin preguntar, un borrado en silencio).
    /// Aquí la pregunta se monta como una CAPA sobre el contenido del modal abierto: el usuario contesta donde
    /// está mirando, con botones de verdad, y lo que contesta es lo que devuelve esta llamada. El cuerpo vuelve
    /// a su sitio en cuanto responde.</para>
    /// </summary>
    /// <param name="cancelText">
    /// El texto del botón de cancelar. Nulo para los AVISOS (una sola salida, la de aceptar), que es lo que
    /// distingue «se pregunta» de «se informa».
    /// </param>
    internal static Task<bool> AskInsideActiveDialogAsync(
        string message,
        string title = "FileFlow Studio",
        string? cancelText = null)
    {
        if (ActiveDialog?.Content is not FrameworkElement body)
        {
            Console.Error.WriteLine("[UnoWindowService] pregunta no mostrada: no hay un modal abierto donde hacerla");
            return Task.FromResult(false);
        }

        if (s_inlineAnswer is not null)
        {
            // Ya hay una pregunta (o un AVISO, que es una pregunta sin salida de cancelar) en pantalla: la nueva
            // la SUSTITUYE en vez de declinarse. Declinarse dejaba la orden sin efecto Y sin aviso —el usuario
            // pulsa «Eliminar» y no pasa nada, sin saber por qué— porque la capa anterior seguía tapando el
            // cuerpo. La anterior se contesta «no», que es el lado seguro. Con el velo por delante, el ratón no
            // llega al cuerpo, pero el teclado sí: la sustitución es lo que hace que preguntar SIEMPRE pregunte.
            TearDownInlineQuestion(false);
        }

        // La pregunta se monta DENTRO del cuerpo del modal, no sustituyéndolo: WinUI no deja colgar un elemento
        // de dos padres, así que sacar el cuerpo de su diálogo para volver a colgarlo de una capa fallaba
        // (medido: COMException al añadirlo). El cuerpo sigue siendo el contenido del diálogo, y la capa es un
        // hijo suyo más —el último, encima de todo y con su velo, que es lo que deja el fondo inerte mientras
        // se pregunta—.
        if (HostPanel(body) is not { } host)
        {
            Console.Error.WriteLine("[UnoWindowService] pregunta no mostrada: el cuerpo del modal ('"
                + body.GetType().Name + "') no admite una capa dentro");
            return Task.FromResult(false);
        }

        var loc = LocalizationManager.Instance;
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var card = new Border
        {
            MaxWidth = 460,
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(8),
            Background = ThemeBrush("CanvasSurfaceBrush", Windows.UI.Color.FromArgb(255, 32, 36, 44)),
            BorderBrush = ThemeBrush("CanvasBorderBrush", Windows.UI.Color.FromArgb(255, 70, 76, 88)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var text = ThemeBrush("CanvasTextBrush", Microsoft.UI.Colors.White);
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = text,
        });
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = text });

        var accept = new Button
        {
            Content = loc.GetString("Common_Accept", "Aceptar"),
            Padding = new Thickness(14, 6, 14, 6),
        };
        AutomationProperties.SetAutomationId(accept, "HostConfirmationAccept");

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        Button? cancel = null;
        if (!string.IsNullOrEmpty(cancelText))
        {
            cancel = new Button { Content = cancelText, Padding = new Thickness(14, 6, 14, 6) };
            AutomationProperties.SetAutomationId(cancel, "HostConfirmationCancel");
            buttons.Children.Add(cancel);
        }

        buttons.Children.Add(accept);
        stack.Children.Add(buttons);
        card.Child = stack;

        var layer = new Grid();
        AutomationProperties.SetAutomationId(layer, "HostConfirmationDialog");
        layer.Children.Add(new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(160, 0, 0, 0)) });
        layer.Children.Add(card);
        host.Children.Add(layer);

        s_inlineAnswer = answer;
        s_inlineAccept = accept;
        s_inlineCancel = cancel;
        s_inlineHost = host;
        s_inlineLayer = layer;

        // La capa se retira del cuerpo y el cuerpo se queda donde estaba: nada que devolver a su sitio.
        void Finish(bool confirmed) => TearDownInlineQuestion(confirmed);

        accept.Click += (_, _) => Finish(true);
        if (cancel is not null)
        {
            cancel.Click += (_, _) => Finish(false);
        }

        return answer.Task;
    }

    /// <summary>
    /// El panel del cuerpo de un modal donde montar la pregunta: el cuerpo mismo si ya es un panel, o el panel
    /// de su contenido (los cuerpos de este host son <see cref="UserControl"/> con un <see cref="Panel"/> de
    /// raíz). Devuelve nulo para un cuerpo que no admite hijos, y entonces la pregunta se declina en vez de
    /// caer: quien pregunte se queda sin borrar, que es el lado seguro.
    /// </summary>
    private static Panel? HostPanel(FrameworkElement body) => body switch
    {
        Panel panel => panel,
        UserControl { Content: Panel inUserControl } => inUserControl,
        ContentControl { Content: Panel inContentControl } => inContentControl,
        _ => null,
    };

    /// <summary>
    /// Retira la pregunta en pantalla —su capa vuelve a salir del cuerpo— y la da por contestada con la
    /// respuesta que se le pase (lo que el usuario pulsó, o «no» si nadie llegó a pulsar nada). Lo llaman los
    /// botones de la pregunta, el cierre del modal y la llegada de una pregunta nueva: una confirmación
    /// abandonada o sustituida no puede dejar el estado tomado.
    /// </summary>
    private static void TearDownInlineQuestion(bool confirmed)
    {
        TaskCompletionSource<bool>? answer = s_inlineAnswer;
        if (s_inlineHost is not null && s_inlineLayer is not null)
        {
            s_inlineHost.Children.Remove(s_inlineLayer);
        }

        s_inlineHost = null;
        s_inlineLayer = null;
        s_inlineAnswer = null;
        s_inlineAccept = null;
        s_inlineCancel = null;
        answer?.TrySetResult(confirmed);
    }

    /// <summary>El pincel de un recurso del tema activo, o uno plano si el tema no lo declara.</summary>
    private static Brush ThemeBrush(string key, Windows.UI.Color fallback)
        => Application.Current.Resources.TryGetValue(key, out object? value) && value is Brush brush
            ? brush
            : new SolidColorBrush(fallback);

    // ───────────────────────────────────────────────────────────────────────────────
    // La sonda: lo que un lector de pantalla también puede hacer
    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>El botón primario del diálogo abierto (el «Guardar y Aplicar» / «Insertar Variable»).</summary>
    internal static Button? ActivePrimaryButton => FindByName<Button>(ActiveDialog, "PrimaryButton");

    /// <summary>El botón de cierre del diálogo abierto (el «Cancelar»).</summary>
    internal static Button? ActiveCloseButton => FindByName<Button>(ActiveDialog, "CloseButton");

    /// <summary>
    /// PULSA un control como lo pulsa el usuario: por el peer de automatización del propio control (el
    /// Invoke de un botón), que es el MISMO canal por el que llega un lector de pantalla.
    /// </summary>
    internal static bool Press(Control? control)
    {
        if (control is null)
        {
            return false;
        }

        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(control);
        if (peer is ButtonAutomationPeer button)
        {
            button.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Un elemento del árbol visual por su nombre de plantilla (los botones del modal). Es también la vía
    /// de la sonda para pulsar el botón de una confirmación del host, que vive en el diálogo de
    /// <c>UnoDialogService</c> y no en el de este servicio: la búsqueda es del host, no de un diálogo.
    /// </summary>
    internal static T? FindByName<T>(DependencyObject? root, string name) where T : FrameworkElement
    {
        if (root is null)
        {
            return null;
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed && string.Equals(typed.Name, name, StringComparison.Ordinal))
            {
                return typed;
            }

            T? found = FindByName<T>(child, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Un diálogo que este host no sirve: se deja anotado y se escribe en la consola de la aplicación. No es
    /// un «cancelado» silencioso —eso se leería como un fallo del usuario— sino una frontera declarada. Es
    /// <c>internal</c> porque la ventana también declina lo que no llega a este servicio —una orden cuya
    /// superficie no declara ningún nodo— y tiene que dejar la misma traza, no callarse.
    /// </summary>
    internal static void Decline(string dialogKey, string why)
    {
        lock (_lock)
        {
            _declined.Add(dialogKey + ": " + why);
        }

        Console.Error.WriteLine($"[UnoWindowService] diálogo '{dialogKey}' no servido por este host: {why}");

        try
        {
            MainViewModel? main = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<MainViewModel>(App.Services);
            main?.LogConsole.AddLog(FileFlow.Sdk.LogLevel.Warning,
                $"Diálogo «{dialogKey}» no disponible en este host: {why}");
        }
        catch
        {
            // Sin contenedor (o sin consola) el aviso se queda en el registro estático y en stderr.
        }
    }

    /// <summary>Limpia la traza de declinados (lo usa la sonda entre pasos).</summary>
    internal static void ClearDeclined()
    {
        lock (_lock)
        {
            _declined.Clear();
        }
    }

    /// <summary>Todas las claves de <see cref="DialogKeys"/>, por reflexión (la guardia las exige cubiertas).</summary>
    internal static IReadOnlyList<string> AllDialogKeys() =>
        typeof(DialogKeys)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
}
