using System;
using System.IO;
using System.Linq;
using FileFlow.App.Core;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Platform;
using FileFlow.Sdk.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FileFlow.App.Uno;

/// <summary>
/// Composition root del host Uno Platform. Registra el núcleo portable completo (motor, plugins,
/// servicios de infraestructura y ViewModels de <c>FileFlow.App.Core</c>) y añade los tres
/// adaptadores de plataforma Uno/WinUI (diálogo, portapapeles, despachado). Sin ninguna referencia
/// a Avalonia: el núcleo y los ViewModels ya viven en FileFlow.App.Core (rebanada 2).
/// </summary>
public partial class App : Application
{
    /// <summary>Código de salida cuando el arranque falla: el proceso termina diciendo que no arrancó.</summary>
    public const int StartupFailureExitCode = 1;

    private static Window? s_mainWindow;
    private static IServiceProvider? s_services;

    /// <summary>Ventana principal; los adaptadores la usan para anclar diálogos.</summary>
    public static Window? MainWindow => s_mainWindow;

    public static IServiceProvider Services => s_services ?? throw new InvalidOperationException("La aplicación aún no ha arrancado.");

    public App()
    {
        InitializeComponent();

        // El sondeo en runtime debe poder decir la verdad: una excepción stowed de WinRT (0xC000027B)
        // mata el proceso sin rastro; aquí queda escrita en fichero antes de decidir el veredicto.
        UnhandledException += (_, e) =>
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(AppContext.BaseDirectory, "selfcheck-crash.txt"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " " + e.Message + Environment.NewLine
                    + (e.Exception?.StackTrace ?? "<sin pila>") + Environment.NewLine + Environment.NewLine);
            }
            catch
            {
            }

            e.Handled = true; // el sondeo reintenta y fallará por su cuenta si el árbol quedó roto
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Los TEXTOS del host (hito 255): el diccionario propio del host Uno — las claves Uno_* en los dos
        // idiomas — es lo que convierte el fallback incrustado de cada GetString en texto de verdad
        // traducible. Sin él, elegir English re-culturaba el proceso y todo seguía en español (el fallback
        // argumento), que es exactamente lo que la superficie de ajustes mide.
        LocalizationManager.Instance.RegisterResourceManager(
            new System.Resources.ResourceManager("FileFlow.App.Uno.Resources.Strings", typeof(App).Assembly));

        // Puente de temas (fase 3.5): ANTES de aplicar cualquier tema — el ThemeManager del núcleo
        // notifica por ThemeHostBridge y este host responde republicando los tokens Canvas* en
        // Application.Resources (los pinceles de WinUI no se re-evalúan solos). La ventana llega por
        // proveedor diferido: WinUI 3 no expone la lista de ventanas en Application.
        Platform.UnoThemeHost.Install(() => s_mainWindow);
        var services = new ServiceCollection();
        ConfigureServices(services);
        s_services = services.BuildServiceProvider();

        // Bordes del host hacia el núcleo portable: sin estas instalaciones el núcleo cae a sus
        // no-ops seguros (portapapeles descartado, vista previa sin ventana, temas sin publicar).
        HostUi.Install(
            dispatcher: s_services.GetRequiredService<IUiDispatcher>(),
            clipboard: s_services.GetRequiredService<IClipboardService>(),
            mainWindowOwner: null, // la ventana aún no existe; el dialog service la resuelve él mismo
            // El CATÁLOGO DE VENTANAS del host: lo consulta el núcleo para cumplir las superficies que DECLARAN
            // los nodos (la tarjeta de un nodo lanza la suya por aquí). Sin esta instalación, esa puerta cae al
            // nulo declarado y el botón de la tarjeta no abre nada.
            windowService: s_services.GetRequiredService<IWindowService>());

        // El contenedor para los ViewModels del núcleo que se construyen sin él (los que el host crea con
        // «new»): sin esto sus avisos caen al nulo aunque el host tenga diálogos de verdad.
        CoreDialogHost.Services = s_services;

        // Los titulares de servicios que el VM de parámetros consulta por su estático (ventana para
        // diálogos de nodo, menú de variables). El CATÁLOGO DE DIÁLOGOS y el de FICHEROS ya son los del
        // host; el MENÚ rápido de variables sigue en su Nulo declarado (el selector completo se sirve
        // por su ventana, y la entrada del menú rápido queda declarada).
        FileFlow.App.Services.ServiceHolders.FileDialog = s_services.GetRequiredService<FileFlow.App.Services.IFileDialogService>();

        // El ancla del CATÁLOGO DE DIÁLOGOS (rebanada 5.3): los ViewModels del núcleo que ya existían
        // —NodeParameterViewModel, entre ellos— la leen de aquí cuando nadie les pasa el servicio por
        // constructor, que es el caso de las filas de parámetros que el inspector construye. Sin este
        // anclaje, los diálogos vuelven al Nulo aunque el contenedor tenga el real.
        FileFlow.App.Services.ServiceHolders.WindowService = s_services.GetRequiredService<IWindowService>();

        // Las preferencias GUARDADAS del usuario se aplican con la ventana YA CREADA y ANTES de activarla.
        //
        // <para>El ORDEN no es un detalle y se midió en la sesión del playtest: aplicándolo antes de crear
        // la ventana, el gestor de temas quedaba con el tema guardado y el lienzo seguía pintando el de por
        // defecto (marco oscuro con 'light_studio' guardado, medido en píxeles y por la sonda), porque la
        // publicación del tema muta los pinceles y la variante A TRAVÉS de la ventana y sin ella se pierde
        // sin ruido. Crear la ventana primero no la enseña antes de tiempo: se activa después.</para>
        s_mainWindow = new MainWindow();
        ApplySavedPreferences(s_services);
        s_mainWindow.Activate();

        // Sondeo UIA externo (--selfcheck-uia): la app vive, un hijo externo la observa por UIA
        // con las anclas del 238 y el veredicto llega por su código de salida (hilo de fondo: el
        // hilo de UI sigue bombeando mensajes, que es por donde UIA responde).
        if (Environment.GetCommandLineArgs().Contains("--selfcheck-uia", StringComparer.Ordinal))
        {
            // La escena del inspector (hito 245) se monta y asienta ANTES de lanzar al hijo: el
            // observador (cliente UIA) llega a escena quieta — la materialización del contenido
            // con Expander dispara una tormenta de eventos UIA que, con cliente conectado, tumba
            // el proceso (medido: exit 127 sin WER ni excepción).
            SelfCheckUia.Run(s_mainWindow);
        }

        // Sondeo de la superficie de AJUSTES (--selfcheck-settings): modo propio porque su medición cambia
        // tema e idioma —estado global de la aplicación— y no convive con los sondeos del lienzo, que miden
        // un árbol que no tolera esa mudanza a mitad. Es la superficie nueva (hito 255) y su veredicto sale
        // por selfcheck-settings-report.txt.
        if (Environment.GetCommandLineArgs().Contains("--selfcheck-settings", StringComparer.Ordinal))
        {
            SelfCheckSettings.Run(
                s_mainWindow,
                s_services.GetRequiredService<IUiDispatcher>() is UnoUiDispatcher d
                    ? d.Queue
                    : DispatcherQueue.GetForCurrentThread());
        }

        // Sondeo del MENÚ PRINCIPAL (--selfcheck-controlbar): la barra de control y su cajón, ejercidos por
        // los peers de automatización de sus entradas. Modo propio como el de ajustes: su ciclo de ejecución
        // mueve el documento (snapshots y diff) y las sondas del lienzo miden una escena que no tolera esa
        // mudanza a mitad. El veredicto sale por selfcheck-controlbar-report.txt.
        if (Environment.GetCommandLineArgs().Contains("--selfcheck-controlbar", StringComparer.Ordinal))
        {
            SelfCheckControlBar.Run(
                s_mainWindow,
                s_services.GetRequiredService<IUiDispatcher>() is UnoUiDispatcher c
                    ? c.Queue
                    : DispatcherQueue.GetForCurrentThread());
        }

        // Sondeo de los PANELES DE NODO (--selfcheck-dialogs): el botón «✎» y el botón «{x}» de una fila
        // del inspector, el diálogo que abren y el valor que queda escrito en el parámetro del nodo. Modo
        // propio como los anteriores: abre y cierra modales sobre la misma raíz y escribe en el nodo
        // inspeccionado —la escena que las sondas del lienzo están midiendo—. El veredicto sale por
        // selfcheck-dialogs-report.txt.
        if (Environment.GetCommandLineArgs().Contains("--selfcheck-dialogs", StringComparer.Ordinal))
        {
            SelfCheckDialogs.Run(
                s_mainWindow,
                s_services.GetRequiredService<IUiDispatcher>() is UnoUiDispatcher dl
                    ? dl.Queue
                    : DispatcherQueue.GetForCurrentThread());
        }

        // Sondeo en runtime (--selfcheck en la línea de comandos): monta la app real y confirma el
        // árbol de la tarjeta sin interacción, terminando el proceso con el veredicto.
        if (Environment.GetCommandLineArgs().Contains("--selfcheck", StringComparer.Ordinal))
        {
            RuntimeSelfCheck.Run(
                s_mainWindow,
                s_services.GetRequiredService<IUiDispatcher>() is UnoUiDispatcher d
                    ? d.Queue
                    : DispatcherQueue.GetForCurrentThread());
        }

        // El AVISO DE ACTUALIZACIÓN (hito 259): la MISMA comprobación de arranque del escritorio
        // (`App.axaml.cs`) —en segundo plano, sin forzar y sólo si el usuario la tiene activada—. Si hay
        // novedad y no la ignoró, el distintivo de la barra aparece con su versión y su comando abre el
        // aviso por el catálogo de diálogos. Sin esta mitad, el aviso que el host ya sabe servir no lo
        // pediría nadie: un botón invisible para siempre.
        StartUpdateCheck(s_services);

        // Las dos superficies que el usuario sólo alcanza con estado que ESTA máquina no tiene —el aviso de
        // actualización (hace falta una release nueva de verdad) y el explorador virtual (hace falta una
        // ejecución que dejara archivos virtuales)— se pueden alimentar con una semilla de QA por variable de
        // entorno, para poder ejercerlas en la aplicación abierta como las usa un usuario. Fuera de la
        // sesión de medida no se enciende ninguna: sin la variable, esto no hace nada.
        ApplyMeasurementSeeds(s_services);
    }

    /// <summary>
    /// Las SEMILLAS de la sesión de medida: con <c>FILEFLOW_QA_UPDATE</c> (una versión nueva) y
    /// <c>FILEFLOW_QA_VFS</c> (cuántos archivos virtuales), el host arranca con el distintivo de
    /// actualización y con el almacén virtual ya puestos, que es el estado que en la máquina de un usuario
    /// sólo produce la red o una ejecución real.
    ///
    /// <para><b>Por qué existe y qué NO cambia</b>: sin esto, las dos superficies quedan medidas sólo en
    /// proceso (la sonda) y no en la aplicación abierta, que es donde se ven los gestos y los píxeles. La
    /// semilla pone EL DATO por el mismo canal del producto —<c>ControlBarViewModel.SetPendingUpdate</c> para
    /// el aviso y el almacén del núcleo para el explorador—; la entrada, la superficie y el cierre son los de
    /// siempre. No hay ninguna rama de producto que dependa de estas variables: fuera de la sesión, cero.</para>
    /// </summary>
    private static void ApplyMeasurementSeeds(IServiceProvider services)
    {
        var mainVm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetService<MainViewModel>(services);
        if (mainVm is null)
        {
            return;
        }

        string? announcedVersion = Environment.GetEnvironmentVariable("FILEFLOW_QA_UPDATE");
        if (!string.IsNullOrWhiteSpace(announcedVersion))
        {
            mainVm.ControlBar.SetPendingUpdate(new AppUpdateInfo(
                announcedVersion, SemVersion.Parse(announcedVersion), "FileFlow Studio " + announcedVersion,
                "Versión sembrada por la sesión de medida (FILEFLOW_QA_UPDATE).", DateTime.UtcNow, false, null,
                "https://example.invalid/fileflow"));
        }

        string? virtualFiles = Environment.GetEnvironmentVariable("FILEFLOW_QA_VFS");
        if (int.TryParse(virtualFiles, out int count) && count > 0)
        {
            // El almacén que se siembra es el MISMO tipo que llena el motor al ejecutar, y se llena por su
            // API real: la ventana no ve un doble, ve un almacén virtual con sus entradas.
            var store = new FileFlow.Core.Engine.VirtualFileSystemStore();
            for (int i = 1; i <= count; i++)
            {
                string name = $"Sembrado {i:D2}.mkv";
                store.AddOrUpdateFile(new FileFlow.Sdk.VirtualFileSystem.VirtualFileEntry(
                    "Semilla/" + name, @"C:\semilla\" + name, name, ".mkv", "Semilla", 4096L * i,
                    FileFlow.Sdk.VirtualFileSystem.VirtualOperationType.Original, "SemillaQa", "n" + i,
                    Role: FileFlow.Sdk.VirtualFileSystem.VirtualFileRole.Source));
            }

            mainVm.ControlBar.SetVirtualFileSystem(store);
        }
    }

    /// <summary>
    /// La comprobación de actualizaciones del arranque, en segundo plano y tolerante a fallos, como la del
    /// escritorio.
    ///
    /// <para><b>Los modos de sondeo no consultan la red</b>: su veredicto tiene que ser hermético y
    /// reproducible, y una novedad real abriría un aviso en mitad de la medición (y en el modo de la barra,
    /// justo encima de la sonda que cuenta entradas). La comprobación se salta ENTERA —no se lanza y se
    /// ignora— para no dejar tráfico ni una tarea viva detrás del proceso.</para>
    /// </summary>
    private static void StartUpdateCheck(IServiceProvider services)
    {
        if (Environment.GetCommandLineArgs().Any(argument =>
                argument.StartsWith("--selfcheck", StringComparison.Ordinal)))
        {
            return;
        }

        var preferences = services.GetRequiredService<IUserPreferencesService>();
        if (!preferences.Preferences.AutoCheckForUpdates)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3000); // el mismo margen del escritorio: que la UI esté montada

                var channel = string.Equals(preferences.Preferences.UpdateChannel, "Beta",
                    StringComparison.OrdinalIgnoreCase)
                    ? UpdateChannel.Beta
                    : UpdateChannel.Stable;

                var result = await AppUpdateService.Instance.CheckForUpdatesAsync(
                    channel, force: false, CancellationToken.None);

                if (!result.UpdateAvailable || result.UpdateInfo is null)
                {
                    return;
                }

                if (string.Equals(preferences.Preferences.IgnoredUpdateVersion,
                        result.UpdateInfo.VersionTag, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (s_mainWindow is MainWindow window
                    && services.GetRequiredService<IUiDispatcher>() is UnoUiDispatcher dispatcher)
                {
                    dispatcher.Queue.TryEnqueue(() => window.ApplyPendingUpdate(result.UpdateInfo));
                }
            }
            catch
            {
                // Comprobación en segundo plano tolerante a fallos, como la del escritorio.
            }
        });
    }

    /// <summary>
    /// Aplica las preferencias GUARDADAS del usuario al arrancar: es el mismo arranque del escritorio
    /// (<c>LoadPreferences</c> + <c>ApplySavedTheme</c>) y lo que hace que el host se vea como el usuario lo
    /// dejó.
    ///
    /// <para><b>Sin esta etapa</b> el host arranca con los valores por defecto del núcleo y el usuario ve,
    /// cada vez que abre la aplicación, un tema y un idioma que no eligió aunque su preferencia esté escrita:
    /// el ajuste se GUARDA y no se APLICA. Es la mitad del defecto que la superficie de ajustes mide, y la
    /// única que no se puede ver desde la propia superficie (dentro de ella el tema se aplica al guardar): la
    /// sonda del modo <c>--selfcheck-settings</c> la mide en el renglón de arranque, comparando lo APLICADO
    /// con lo GUARDADO antes de tocar nada.</para>
    ///
    /// <para><b>Se llama con la ventana creada y sin activar</b>: el repintado del tema pasa por la ventana
    /// (pinceles y variante) y aplicarlo antes de crearla dejaba el marco con el tema de por defecto — la
    /// medición que obligó a mover la llamada.</para>
    /// </summary>
    private static void ApplySavedPreferences(IServiceProvider services)
    {
        var preferences = services.GetRequiredService<IUserPreferencesService>();
        preferences.Load();

        // Idioma: el guardado se normaliza a uno de los que ofrece la aplicación (un valor ausente o heredado
        // dejaría el desplegable de idioma sin nada que mostrar) y se reescribe una sola vez.
        string savedLanguage = preferences.Preferences.Language;
        string language = LanguageCatalog.Resolve(savedLanguage)?.Code ?? LanguageCatalog.All[0].Code;
        LocalizationManager.Instance.SetCulture(language);
        if (!string.Equals(language, savedLanguage, StringComparison.Ordinal))
        {
            preferences.Preferences.Language = language;
            preferences.Save();
        }

        // Tema: la preferencia puede traer el identificador heredado ('Dark', 'Light') y el catálogo usa los
        // suyos ('dark_fluent', 'light_studio'); se traduce y, si cambió, se reescribe para no arrastrarlo.
        string savedTheme = preferences.Preferences.ActiveTheme;
        string themeId = ThemeManager.ResolveThemeId(savedTheme) ?? ThemeManager.DefaultThemeId;
        services.GetRequiredService<FileFlow.App.Services.IThemeService>().SetThemeById(themeId);
        if (!string.Equals(themeId, savedTheme, StringComparison.Ordinal))
        {
            preferences.Preferences.ActiveTheme = themeId;
            preferences.Save();
        }
    }

    /// <summary>
    /// Registro de servicios idéntico en intención al del host Avalonia: la base portable
    /// (<c>AddFileFlowCoreServices</c>) más los adaptadores Uno/WinUI. Los ViewModels llegan del
    /// núcleo; aquí sólo se declara lo acoplado a la UI de este host.
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // 1. Núcleo portable: motor, plugins, servicios de infraestructura y ViewModels
        services.AddFileFlowCoreServices();

        // 2. Adaptadores de plataforma implementados con Uno (los únicos puntos acoplados a la UI)
        services.AddSingleton<IDialogService, UnoDialogService>();
        services.AddSingleton<IClipboardService, UnoClipboardService>();
        services.AddSingleton<IUiDispatcher, UnoUiDispatcher>();

        // Rebanada 4: los pickers de Windows para el explorar de rutas del inspector (el registro
        // portátil trae el nulo; sin uno real, el botón «explorar» de la ficha no haría nada).
        services.AddSingleton<FileFlow.App.Services.IFileDialogService, UnoFileDialogService>();

        // Rebanada 5.3: el CATÁLOGO DE DIÁLOGOS del host (el editor de texto de un parámetro y el
        // selector de variables). Sin él, los dos comandos del núcleo que los piden caían en el Nulo
        // declarado y el usuario veía un botón que no hacía nada.
        services.AddSingleton<IWindowService, UnoWindowService>();
    }
}
