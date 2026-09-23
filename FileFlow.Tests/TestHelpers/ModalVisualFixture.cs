using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.App.Views.Components;
using FileFlow.Plugin.AI.Management;
using FileFlow.Plugin.AI.UI;
using FileFlow.Plugin.AI.ViewModels;
using FileFlow.Plugin.Archives.UI.Views;
using FileFlow.Plugin.FileSystem.UI.Views;
using FileFlow.Plugin.Integrations.UI.Views;
using FileFlow.Sdk.Services;

namespace FileFlow.Tests.TestHelpers;

/// <summary>Superficie de una ventana modal que se puede capturar.</summary>
public enum ModalSurface
{
    /// <summary>Acerca de.</summary>
    About,

    /// <summary>Selector de variables con grupos de ejemplo.</summary>
    VariablePicker,

    /// <summary>Gestor de modelos de IA (lista y resumen de instalación).</summary>
    AiModelManager,

    /// <summary>Diálogo de URLs personalizadas de un modelo.</summary>
    AiModelUrls,

    /// <summary>Ajustes de flujo con dobles de todos sus puertos (se abre en la primera pestaña: almacenamiento).</summary>
    WorkflowSettings,

    /// <summary>Ajustes de flujo en la pestaña de apariencia e interfaz.</summary>
    WorkflowSettingsAppearance,

    /// <summary>Ajustes de flujo en la pestaña de rendimiento y ejecución.</summary>
    WorkflowSettingsPerformance,

    /// <summary>Ajustes de flujo en la pestaña de herramientas externas.</summary>
    WorkflowSettingsExternalTools,

    /// <summary>Ajustes de flujo en la pestaña de modelos de IA.</summary>
    WorkflowSettingsAiModels,

    /// <summary>Configuración del nodo VLM multimodal (plugin de IA).</summary>
    MultimodalVlm,

    /// <summary>Gestor de contraseñas de archivos (plugin de archivos).</summary>
    PasswordManager,

    /// <summary>Ayuda de expresiones regulares (plugin de sistema de ficheros).</summary>
    RegexHelper,

    /// <summary>Diseñador de conjuntos de datos sintéticos (plugin de sistema de ficheros).</summary>
    SyntheticDataSetDesigner,

    /// <summary>Gestor de presets de media (plugin de integraciones).</summary>
    MediaPresetManager,

    /// <summary>Ventana de error de arranque (la que hace visible un fallo de inicio).</summary>
    StartupError,

    /// <summary>Pantalla de carga en su primer fotograma quieto (el barrido no arranca: ver <c>StartShimmer</c>).</summary>
    Splash
}

/// <summary>
/// Ventanas modales de la aplicación y de los plugins, montadas de forma determinista para sus capturas.
///
/// Principios (los mismos que <see cref="AppVisualFixture"/>):
/// <list type="button">
///   <item><b>Dobles de puertos</b>: el almacenamiento de configuraciones VLM es un directorio temporal,
///   el gestor de modelos muestra IDs falsos (nunca instalados en la máquina del test) y las ventanas de
///   plugins se construyen por sus constructores de pruebas. Nada escribe en el perfil del usuario.</item>
///   <item><b>Valores fijos</b>: tamaño de ventana y contenidos sembrados declarados a mano, de modo que
///   la imagen comparada sea reproducible.</item>
///   <item><b>Limpieza</b>: la captura desmonta la ventana (purga de bindings) y libera los view models
///   que se suscribieron a singletons del proceso.</item>
/// </list>
/// </summary>
public static class ModalVisualFixture
{
    /// <summary>
    /// Construye la ventana pedida. Debe llamarse en el hilo de UI (las ventanas tienen afinidad de hilo).
    /// </summary>
    public static Window Build(ModalSurface surface)
    {
        AvaloniaTestHelper.RequireUIThread($"{nameof(ModalVisualFixture)}.{nameof(Build)}({surface})");

        EnsurePluginStringsRegistered();

        return surface switch
        {
            ModalSurface.About => new FileFlow.App.Views.AboutDialogWindow(),
            ModalSurface.VariablePicker => BuildVariablePicker(),
            ModalSurface.AiModelManager => BuildAiModelManager(),
            ModalSurface.AiModelUrls => new AiModelUrlsConfigDialog(BuildAiModelUrlsViewModel()),
            ModalSurface.WorkflowSettings => BuildWorkflowSettings(0),
            ModalSurface.WorkflowSettingsAppearance => BuildWorkflowSettings(1),
            ModalSurface.WorkflowSettingsPerformance => BuildWorkflowSettings(2),
            ModalSurface.WorkflowSettingsExternalTools => BuildWorkflowSettings(3),
            ModalSurface.WorkflowSettingsAiModels => BuildWorkflowSettings(4),
            ModalSurface.MultimodalVlm => BuildMultimodalVlm(),
            ModalSurface.PasswordManager => BuildPasswordManager(),
            ModalSurface.RegexHelper => BuildRegexHelper(),
            ModalSurface.SyntheticDataSetDesigner => BuildSyntheticDataSetDesigner(),
            ModalSurface.MediaPresetManager => BuildMediaPresetManager(),
            ModalSurface.StartupError => BuildStartupError(),
            ModalSurface.Splash => BuildSplash(),
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "Superficie no soportada.")
        };
    }

    /// <summary>Tamaños de captura por superficie (los mismos que declaran las ventanas).</summary>
    public static (int Width, int Height) SizeOf(ModalSurface surface) => surface switch
    {
        ModalSurface.About => (520, 300),
        ModalSurface.VariablePicker => (620, 560),
        ModalSurface.AiModelManager => (900, 660),
        ModalSurface.AiModelUrls => (680, 420),
        ModalSurface.WorkflowSettings => (880, 640),
        ModalSurface.WorkflowSettingsAppearance => (880, 640),
        ModalSurface.WorkflowSettingsPerformance => (880, 640),
        ModalSurface.WorkflowSettingsExternalTools => (880, 640),
        ModalSurface.WorkflowSettingsAiModels => (880, 640),
        ModalSurface.MultimodalVlm => (1060, 720),
        ModalSurface.PasswordManager => (560, 460),
        ModalSurface.RegexHelper => (860, 580),
        ModalSurface.SyntheticDataSetDesigner => (1240, 820),
        ModalSurface.MediaPresetManager => (760, 520),
        ModalSurface.StartupError => (720, 430),
        ModalSurface.Splash => (540, 350),
        _ => (600, 400)
    };

    /// <summary>
    /// Captura la superficie mostrando la ventana real (su XAML raíz, tamaño y bindings de ventana) con el
    /// tema pedido. <see cref="VisualSnapshot.CaptureWindow"/> se encarga de mostrar, purgar y cerrar la
    /// ventana en el hilo de UI; el <c>finally</c> es sólo la red de seguridad si la captura lanza antes
    /// de llegar a su limpieza.
    /// </summary>
    public static byte[] Capture(ModalSurface surface, string themeId)
    {
        Window? window = null;

        try
        {
            // La fábrica la construye CaptureWindow dentro de su despacho: construir en uno y mostrar en
            // otro deja la ventana sin fotograma en la sesión headless (ver CaptureWindow).
            return VisualSnapshot.CaptureWindow(() => window = Build(surface), themeId);
        }
        finally
        {
            if (window is { } built)
            {
                // Si CaptureWindow llegó a su limpieza, la ventana ya está cerrada y purgada: repeterlas
                // es inofensivo. Si la captura lanzó antes, esto evita dejarla viva con sus bindings.
                AvaloniaTestHelper.RunOnUI(() =>
                {
                    VisualSnapshot.DetachTree(built);
                    built.Close();
                });
            }
        }
    }

    /// <summary>
    /// Registra los recursos de localización de los plugins cuyas ventanas se capturan, igual que hace
    /// producción al arrancar (el host registra todos los plugins en el arranque).
    ///
    /// Sin esto, una captura aislada (p. ej. al regenerar líneas base con un filtro de tests) renderiza los
    /// <c>FallbackValue</c> del XAML —el plugin aún no publicó sus cadenas— y la línea base congela un
    /// estado que no es el de producción: en la suite completa otro registro ya ocurrió y la imagen difiere.
    /// El registro es idempotente y aditivo (ver <c>LocalizationManager.RegisterResourceManager</c>), así
    /// que llamarlo en cada construcción es barato y no altera el estado del proceso.
    /// </summary>
    private static void EnsurePluginStringsRegistered()
    {
        RegisterPluginStrings(typeof(FileFlow.Plugin.AI.MultimodalVisionLlmNode).Assembly);
        RegisterPluginStrings(typeof(FileFlow.Plugin.FileSystem.AdvancedRenamerNode).Assembly);
        RegisterPluginStrings(typeof(FileFlow.Plugin.Archives.SmartUnpackNode).Assembly);
        RegisterPluginStrings(typeof(FileFlow.Plugin.Integrations.CliExecutionNode).Assembly);
    }

    /// <summary>
    /// Mismo camino que <c>PluginLoader.RegisterPluginResources</c>, con sus dos ramas: la clase de
    /// recursos generada (plugins con Designer) y los recursos embebidos del manifiesto (plugins como
    /// FileFlow.Plugin.AI, que sólo publican <c>Strings.resx</c>/<c>Strings.es.resx</c> sin clase).
    /// </summary>
    private static void RegisterPluginStrings(System.Reflection.Assembly pluginAssembly)
    {
        foreach (var type in pluginAssembly.GetTypes())
        {
            if (!type.Name.Equals("Strings", StringComparison.OrdinalIgnoreCase) &&
                !type.Name.EndsWith("Resources", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (type.GetProperty("ResourceManager",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static)?.GetValue(null) is
                System.Resources.ResourceManager resourceManager)
            {
                FileFlow.Sdk.Localization.LocalizationManager.Instance.RegisterResourceManager(resourceManager);
            }
        }

        foreach (string manifestName in pluginAssembly.GetManifestResourceNames())
        {
            if (!manifestName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) ||
                manifestName.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string baseName = manifestName[..^10]; // sin el sufijo ".resources"

            if (baseName.EndsWith(".es", StringComparison.OrdinalIgnoreCase))
            {
                continue; // el satélite se resuelve vía CurrentUICulture desde el recurso neutro
            }

            FileFlow.Sdk.Localization.LocalizationManager.Instance.RegisterResourceManager(
                new System.Resources.ResourceManager(baseName, pluginAssembly));
        }
    }

    /// <summary>Selector de variables con grupos de ejemplo fijos (el real se llena desde el editor).</summary>
    private static Window BuildVariablePicker()
    {
        // Variables es get-only: el inicializador de colección llama a Variables.Add(...).
        var groups = new List<VariableGroupItem>
        {
            new("Nodo: Origen de carpeta")
            {
                Variables =
                {
                    new VariableItem("source.FileName", "{{source.FileName}}", "Nombre del fichero de origen"),
                    new VariableItem("source.FileSizeBytes", "{{source.FileSizeBytes}}", "Tamaño en bytes"),
                    new VariableItem("source.Extension", "{{source.Extension}}", "Extensión con punto")
                }
            },
            new("Nodo: Optimizador de imágenes")
            {
                Variables =
                {
                    new VariableItem("optimizer.Width", "{{optimizer.Width}}", "Anchura resultante"),
                    new VariableItem("optimizer.Height", "{{optimizer.Height}}", "Altura resultante")
                }
            }
        };

        var window = new FileFlow.App.Views.Components.VariablePickerWindow(groups);
        window.Width = 620;
        window.Height = 560;
        return window;
    }

    /// <summary>
    /// Gestor de modelos con IDs falsos: el <c>RefreshState</c> de cada item consulta el disco real, así
    /// que IDs falsos garantizan «no instalado» y un resumen 0/3 — igual en cualquier máquina.
    ///
    /// Lo usan tanto el asistente de descarga como la pestaña «Modelos de IA» de los ajustes: leer el catálogo
    /// real haría que la línea base de esa pestaña dependiera de qué modelos tenga descargados quien la genera.
    /// </summary>
    private static AiModelManagerViewModel BuildDeterministicAiModelManager()
    {
        var vm = new AiModelManagerViewModel();
        vm.Models.Clear();

        foreach (var (id, name, category) in new[]
                 {
                     ("fake-whisper-tiny", "Whisper Tiny (muestra)", "Transcripción"),
                     ("fake-rmbg-1.4", "RMBG 1.4 (muestra)", "Segmentación"),
                     ("fake-yolo-world", "YOLO-World (muestra)", "Detección de objetos")
                 })
        {
            vm.Models.Add(new AiModelItemViewModel
            {
                ModelId = id,
                Name = name,
                Category = category,
                Description = "Entrada determinista para la captura: nunca existe en disco.",
                FileName = id + ".onnx",
                ExpectedSizeLabel = "12,0 MB",
                StatusText = "No instalado"
            });
        }

        vm.ModelsDirectory = "/workflow/models";
        vm.RefreshStatus();
        return vm;
    }

    /// <summary>Asistente de descarga de modelos (la misma pieza que la pestaña de ajustes, en ventana).</summary>
    private static Window BuildAiModelManager() =>
        new AiModelDownloadDialog { DataContext = BuildDeterministicAiModelManager(), Width = 900, Height = 660 };

    /// <summary>URLs de descarga de un modelo falso (el VM no toca la red en el arranque).</summary>
    private static AiModelUrlsConfigViewModel BuildAiModelUrlsViewModel() =>
        new("fake-model-id");


    /// <summary>
    /// Ajustes de flujo con dobles de todos sus puertos (nada del perfil del usuario) y en la pestaña pedida, de
    /// modo que cada cuerpo del TabControl tenga su propia captura: sin esto, las cuatro pestañas nuevas serían
    /// UI sin línea base.
    /// </summary>
    private static Window BuildWorkflowSettings(int selectedTab)
    {
        // Sin LogViewModel: crearlo sólo para no usarlo deja un suscriptor eterno en
        // LocalizationManager.Instance.LanguageChanged (su Dispose no se llama nunca) — y ese suscriptor
        // delata 'thread cannot access' en SetCulture desde el hilo runner para toda la suite.
        var vm = new WorkflowSettingsViewModel(
            new InMemoryUserPreferencesService(),
            new InMemoryExternalToolsService(),
            ThemeManager.Instance,
            FileFlow.Sdk.Localization.LocalizationManager.Instance,
            new NullFileDialogService(),
            new AvaloniaDialogService(),
            BuildDeterministicAiModelManager());

        var window = new FileFlow.App.Views.Components.WorkflowSettingsWindow(string.Empty, vm);
        window.Width = 880;
        window.Height = 640;

        // La pestaña se elige por el nombre del control: el XAML no expone el TabControl y el árbol lógico
        // no sirve aquí (la ventana aún no está mostrada, pero el ámbito de nombres ya existe).
        window.FindControl<TabControl>("SettingsTabs")!.SelectedIndex = selectedTab;

        return window;
    }

    /// <summary>Configuración VLM con almacenamiento temporal (no lee ni escribe la instancia real).</summary>
    private static Window BuildMultimodalVlm() =>
        new MultimodalVlmConfigWindow(new MultimodalVlmConfigViewModel(
            targetNode: null,
            storageService: new VlmConfigurationStorageService(Path.Combine(Path.GetTempPath(), "FileFlow_VlmVisual_" + Guid.NewGuid().ToString("N"))),
            customHttpClient: new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(6) },
            uiDispatcher: NullUiDispatcher.Instance))
    {
        Width = 1060,
        Height = 720
    };

    /// <summary>Gestor de contraseñas con contenido sembrado (constructor de pruebas del plugin).</summary>
    private static Window BuildPasswordManager() =>
        new PasswordManagerWindow("{\"archivos.zip\": [\"abcd-1234-ef56\"]}")
        {
            Width = 560,
            Height = 460
        };

    /// <summary>Regex Helper con expresión y texto de muestra fijos.</summary>
    private static Window BuildRegexHelper() =>
        new RegexHelperWindow(
            initialPattern: "(\\d{4})-(\\d{2})-(\\d{2})",
            initialReplacement: "$3/$2/$1",
            initialSampleText: "Fechas: 2026-01-15 y 2025-12-31")
        {
            Width = 860,
            Height = 580
        };

    /// <summary>
    /// Diseñador de datos sintéticos con almacenamiento temporal: los datasets oficiales se derivan de las
    /// muestras embebidas (deterministas) y ningún dataset del usuario entra en la captura.
    /// </summary>
    private static Window BuildSyntheticDataSetDesigner() =>
        new FileFlow.Plugin.FileSystem.UI.Views.SyntheticDataSetDesignerWindow(
            new FileFlow.Plugin.FileSystem.UI.ViewModels.SyntheticDataSetDesignerViewModel(
                new FileFlow.Plugin.FileSystem.Services.SyntheticDataSetStorageService(
                    Path.Combine(Path.GetTempPath(), "FileFlow_SyntheticDataVisual_" + Guid.NewGuid().ToString("N"))),
                new NullDialogService()))
        {
            Width = 1240,
            Height = 820
        };

    /// <summary>Gestor de presets de media sin diálogo (los avisos van a un NullDialogService).</summary>
    private static Window BuildMediaPresetManager() =>
        new MediaPresetManagerWindow(new NullDialogService())
        {
            Width = 760,
            Height = 520
        };

    /// <summary>
    /// Ventana de error de arranque con un informe fijo. El entorno va anclado a un texto determinista y la
    /// ruta del registro es ficticia: la línea base no puede depender de la máquina que la generó.
    /// </summary>
    /// <summary>
    /// Pantalla de carga en el estado que congelan sus capturas: <b>el barrido no arranca</b>
    /// (<c>StartShimmer</c> es exclusivo de la aplicación real), de modo que la imagen es el primer fotograma
    /// quieto y no depende de lo que tarde el renderizador.
    ///
    /// <para>El progreso y la insignia de nodos se siembran con la misma API que usa el arranque, para que la
    /// línea base muestre la pantalla de verdad —texto por clave localizada, barra a media carga y cuenta de
    /// nodos— y no su estado en blanco, donde un contenido que desapareciera no se notaría.</para>
    /// </summary>
    private static Window BuildSplash()
    {
        var splash = new FileFlow.App.Views.SplashScreenWindow();

        splash.UpdateStatus(
            FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString(
                "Splash_StatusPlugins",
                "Descubriendo módulos y plugins..."),
            70);

        // Cuenta de nodos fija: la real la descubre el cargador de plugins en el arranque, y una captura no
        // puede depender de qué plugins estén en el perfil de la máquina.
        splash.SetNodeCount(24);

        return splash;
    }

    private static Window BuildStartupError() =>
        FileFlow.App.Views.StartupErrorWindow.Create(new FileFlow.App.Services.StartupFailureReport(
            FileFlow.App.Services.StartupPhase.Services,
            FileFlow.App.Services.StartupPhaseDescriptions.Describe(FileFlow.App.Services.StartupPhase.Services),
            new InvalidOperationException(
                "A circular dependency was detected for the service of type 'FileFlow.App.ViewModels.EditorViewModel'."),
            "/workflow/logs/crash.log",
            new DateTime(2026, 9, 20, 19, 0, 0, DateTimeKind.Utc),
            EnvironmentOverride: "Windows 11 · .NET 10.0.0 · FileFlow.App 1.0.0"));
}
