using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La guardia de la superficie de <b>AJUSTES</b> del host Uno (hito 255): la vista es del host, pero lo que
/// se edita, se valida y se persiste es del <see cref="FileFlow.App.ViewModels.WorkflowSettingsViewModel"/>
/// portable — el mismo que alimenta la ventana de ajustes del escritorio.
///
/// <para><b>Qué protege</b>: (1) que la persistencia siga siendo de los comandos canónicos del view model
/// (<c>SaveSettingsCommand</c> → <c>UpdatePreferences</c> + <c>SetCulture</c> + <c>SetThemeById</c>) y no de
/// una copia del host; (2) que cada control de la superficie esté atado a una propiedad del view model —el
/// censo es la tabla, y una fila nueva entra por ella—; (3) que el <b>diccionario del host</b> exista en los
/// dos idiomas y que ninguna clave <c>Uno_*</c> citada en el código se quede sin entrada, porque sin esa
/// entrada el selector de idioma re-culturaría el proceso y todo seguiría en el fallback incrustado (que es
/// exactamente lo que el sondeo mide); (4) que la medición viva en su propio modo
/// (<c>--selfcheck-settings</c>) y no dentro del sondeo del lienzo, que no tolera que le cambien el tema y el
/// idioma a mitad; y (5) que la restauración devuelva lo que el usuario tenía, no una constante.</para>
/// </summary>
public class UnoSettingsSurfaceGuardTests
{
    private const string HostRoot = "FileFlow.App.Uno";
    private const string WindowXaml = "FileFlow.App.Uno/MainWindow.xaml";
    private const string WindowCode = "FileFlow.App.Uno/MainWindow.xaml.cs";
    private const string PanelXaml = "FileFlow.App.Uno/Controls/SettingsPanel.xaml";
    private const string PanelCode = "FileFlow.App.Uno/Controls/SettingsPanel.xaml.cs";
    private const string WindowService = "FileFlow.App.Uno/Platform/UnoWindowService.cs";
    private const string BodyCode = "FileFlow.App.Uno/Controls/AiModelUrlsConfigBody.xaml.cs";
    private const string SelfCheckCode = "FileFlow.App.Uno/SelfCheckSettings.cs";
    private const string AppCode = "FileFlow.App.Uno/App.xaml.cs";
    private const string StringsEnglish = "FileFlow.App.Uno/Resources/Strings.resx";
    private const string StringsSpanish = "FileFlow.App.Uno/Resources/Strings.es.resx";

    private static string Code(string relativePath) => SourceText.CodeWithoutComments(relativePath);

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath));

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. La superficie es una VISTA del view model portable
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheHostSurface_ShouldBeWiredToThePortableSettingsViewModel()
    {
        Read(WindowXaml).Should().Contain("<controls:SettingsPanel",
            "la superficie de ajustes tiene que estar montada en la ventana: sin ella no hay dónde ajustar nada");

        Code(WindowCode).Should().Contain("Settings.Vm = SettingsPanel.CreateViewModel(services);",
            "el host monta el view model de ajustes con el reparto de dependencias del escritorio: el que ya " +
            "viva en el contenedor del núcleo manda y, si no, los mismos singulares que resuelve la ventana " +
            "del escritorio más los adaptadores de diálogo de ESTE host");

        Code(WindowCode).Should().Contain("private void OnOpenSettingsClicked(object sender, RoutedEventArgs e) => Settings.Open();",
            "el punto de entrada del marco despliega la superficie por su método (no duplica el montaje)");

        string panel = Code(PanelCode);
        panel.Should().Contain("WorkflowSettingsViewModel",
            "la vista consume el view model portable de ajustes");
        panel.Should().NotContain("UpdatePreferences",
            "la persistencia es del view model: si el host la escribiera por su cuenta habría dos verdades " +
            "sobre las preferencias (y la del host no pasaría por SetCulture/SetThemeById)");
        panel.Should().NotContain("new UserPreferencesService",
            "el servicio de preferencias se toma del núcleo (o de su singular), no se instancia en la vista");
    }

    /// <summary>
    /// El censo de la superficie: cada control que la vista pinta, la propiedad que escribe y <b>por dónde</b>
    /// llega al <see cref="FileFlow.App.ViewModels.WorkflowSettingsViewModel"/> portable. Es la tabla que obliga
    /// a que un ajuste nuevo se declare.
    ///
    /// <para><b>Por qué hay dos caminos declarados y no uno</b>: la mayoría de los controles enlazan
    /// <c>TwoWay</c> en el XAML, pero los tres campos numéricos van por <c>NumberBox</c>, cuyo <c>Value</c> es
    /// <c>double</c> y el view model guarda <c>int</c>: el enlace del host no convierte entre tipos y el
    /// número se quedaría escrito en la caja sin llegar a la preferencia (el defecto que el censo caza). Ahí el
    /// camino es el write-back declarado del code-behind —<c>ValueChanged</c> → propiedad del view model— y el
    /// censo lo nombra: una fila sin ninguno de los dos caminos es un control que el usuario mueve y el producto
    /// no se entera; un <c>ValueChanged</c> sin su asignación al view model, lo mismo con otra cara.</para>
    /// </summary>
    private static IReadOnlyList<(string Control, string Binding, string Preference, string WriteBack)> ControlCensus() =>
    [
        ("carpeta de salida global", "GlobalOutputDir", "DefaultGlobalOutputDir", string.Empty),
        ("directorio de trabajo temporal", "TempWorkingDir", "TemporaryDirectory", string.Empty),
        ("estrategia de conflicto", "SelectedConflictStrategy", "DefaultConflictStrategy", string.Empty),
        ("autoguardado", "EnableAutoSave", "EnableAutoSave", string.Empty),
        ("intervalo de autoguardado", "AutoSaveIntervalMinutes", "AutoSaveIntervalMinutes", "OnAutoSaveIntervalChanged"),
        ("limpieza de temporales intermedios", "AutoCleanIntermediateTempFiles", "AutoCleanIntermediateTempFiles", string.Empty),
        ("purga de temporales huérfanos", "CleanStaleTempOnStartup", "CleanStaleTempOnStartup", string.Empty),
        ("idioma", "SelectedLanguage", "Language", string.Empty),
        ("tema", "SelectedTheme", "ActiveTheme", string.Empty),
        ("cajón compacto", "IsCompactToolbox", "IsCompactToolbox", string.Empty),
        ("autodesplazamiento de la consola", "AutoScrollConsole", "AutoScrollConsole", string.Empty),
        ("máximo de líneas de consola", "MaxLogEntries", "MaxLogEntries", "OnMaxLogEntriesChanged"),
        ("hilos de CPU", "MaxParallelThreads", "MaxParallelThreads", "OnMaxCpuThreadsChanged"),
        ("dry-run por defecto", "DefaultDryRunState", "DefaultDryRunState", string.Empty),
        ("nivel mínimo de registro", "SelectedLogLevel", "DefaultLogLevel", string.Empty),
        ("puntos de control", "EnableCheckpointing", "EnableCheckpointing", string.Empty),
        ("descarga de modelos de IA", "AutoUnloadAiModelsOnCompletion", "AutoUnloadAiModelsOnCompletion", string.Empty),
        ("ruta de FFmpeg", "FfmpegPath", "FfmpegPath", string.Empty),
        ("ruta de FFprobe", "FfprobePath", "FfprobePath", string.Empty),
        ("ruta de 7-Zip", "SevenZipPath", "SevenZipPath", string.Empty),
        ("ruta de Python", "PythonPath", "PythonPath", string.Empty),
    ];

    [Fact]
    public void EveryControl_ShouldReachThePortablePreference_ByBindingOrDeclaredWriteBack()
    {
        string xaml = Read(PanelXaml);
        string panel = Code(PanelCode);
        var missing = new List<string>();

        foreach (var (control, binding, preference, writeBack) in ControlCensus())
        {
            if (writeBack.Length == 0)
            {
                if (!xaml.Contains($"{{Binding {binding}", StringComparison.Ordinal))
                {
                    missing.Add($"{control}: la vista no enlaza '{binding}'");
                }
            }
            else
            {
                // El camino declarado: el control avisa a su handler, y el handler asigna la MISMA propiedad
                // del view model que el resto de la superficie escribe.
                if (!xaml.Contains($"ValueChanged=\"{writeBack}\"", StringComparison.Ordinal))
                {
                    missing.Add($"{control}: el control no avisa al write-back '{writeBack}'");
                }

                if (!panel.Contains(writeBack, StringComparison.Ordinal))
                {
                    missing.Add($"{control}: el write-back '{writeBack}' no está en la vista");
                }

                if (!panel.Contains($"_vm.{binding} = value;", StringComparison.Ordinal))
                {
                    missing.Add($"{control}: '{writeBack}' no escribe '{binding}' en el view model");
                }
            }

            // La preferencia que el view model escribe tras ese camino: se censa contra el VM portable para
            // que un renombrado del núcleo no deje el control enganchado a nada.
            Code("FileFlow.App.Core/ViewModels/WorkflowSettingsViewModel.cs")
                .Should().Contain(preference,
                    $"el control '{control}' escribe '{binding}' y el view model tiene que persistir '{preference}'");
        }

        xaml.Should().Contain("Mode=TwoWay",
            "los controles de ajustes escriben en el view model: un enlace de sólo lectura pintaría el valor " +
            "y descartaría la intención del usuario");
        missing.Should().BeEmpty(
            "el censo de la superficie declara TODOS los controles y su camino hasta la preferencia —enlace " +
            "TwoWay o write-back nombrado—: uno sin declarar es un ajuste que el usuario cambia sin que llegue " +
            "al producto");
    }

    [Fact]
    public void ThePersistence_ShouldBeTheViewModelsOwnCommands()
    {
        string panel = Code(PanelCode);

        panel.Should().Contain("_vm?.SaveSettingsCommand.Execute(null)",
            "guardar es el comando canónico del view model: es el que pasa por UpdatePreferences + SetCulture + SetThemeById");
        panel.Should().Contain("_vm?.CancelSettingsCommand.Execute(null)",
            "cancelar es el comando del view model (RequestClose(false)), no un simple ocultar del host");
        panel.Should().Contain("_vm?.ClearCheckpointsCommand.Execute(null)",
            "borrar los puntos de control es la orden del view model");
        panel.Should().Contain("_vm?.CleanTemporaryFilesNowCommand.Execute(null)",
            "limpiar el espacio temporal es la orden del view model");
        panel.Should().Contain("await _vm.AutoDetectToolsCommand.ExecuteAsync(null)",
            "la autodetección de herramientas es la orden asíncrona del view model");

        // El explorador: el host aporta el picker asíncrono (el contrato síncrono del núcleo devuelve null en
        // el hilo de UI — medido y declarado en UnoFileDialogService), pero escribe en la MISMA propiedad.
        panel.Should().Contain("await _fileDialog.ShowFolderBrowserDialogAsync(title)",
            "las carpetas se eligen con el picker asíncrono del host (el síncrono del contrato aborta en el hilo de UI)");
        panel.Should().Contain("await _fileDialog.ShowOpenFileDialogAsync(title",
            "los ejecutables se eligen con el picker asíncrono del host");
        panel.Should().Contain("AssignTarget(_vm, target, folder)",
            "el resultado del picker entra por la MISMA propiedad que el comando del view model habría escrito");
    }

    /// <summary>
    /// Las SEIS secciones de la superficie (hitos 255 y 261): almacenamiento, apariencia, rendimiento,
    /// herramientas externas y las dos que cierran la paridad con la ventana del escritorio —modelos de IA y
    /// actualizaciones—. Cada una con su cuerpo y su botón, y las seis en la tabla que comparten el conmutador
    /// y el sondeo: quitar una sección de la tabla deja su cuerpo inalcanzable y su botón sin destino.
    /// </summary>
    [Fact]
    public void TheSixSections_ShouldBeDeclared_WithTheirPaneAndTheirButton()
    {
        string xaml = Read(PanelXaml);

        foreach (var section in new[] { "Storage", "Appearance", "Performance", "Tools", "AiModels", "Updates" })
        {
            xaml.Should().Contain($"x:Name=\"{section}Pane\"",
                $"la sección '{section}' tiene que tener su cuerpo (materializado: la conmutación es de visibilidad)");
            xaml.Should().Contain($"Tag=\"{section}\"",
                $"la sección '{section}' necesita su botón con la etiqueta que el conmutador lee");
        }

        Code(PanelCode).Should().Contain(
            "[StoragePane, AppearancePane, PerformancePane, ToolsPane, AiModelsPane, UpdatesPane]",
            "el sondeo y el conmutador comparten la tabla de secciones");
        Code(PanelCode).Should().Contain(
            "[StorageTabButton, AppearanceTabButton, PerformanceTabButton, ToolsTabButton, AiModelsTabButton, UpdatesTabButton]",
            "y los botones van en el mismo orden que los cuerpos: la tabla es la que los empareja");
        Code(PanelCode).Should().Contain("internal void ShowSection(string section)",
            "el sondeo conmuta la sección con el MISMO camino que el botón");

        // Las dos secciones portadas en el hito 261 son VISTAS del view model portable: la lista de modelos es
        // el catálogo del gestor del núcleo y la sección de actualizaciones enseña sus propiedades, no copias.
        xaml.Should().Contain("ItemsSource=\"{Binding AiModelManager.Models}\"",
            "la lista de modelos es el catálogo del gestor del núcleo, no una lista propia del host");
        xaml.Should().Contain("Text=\"{Binding CurrentVersionDisplay}\"",
            "la versión que se enseña en la sección de actualizaciones sale del view model portable");
        xaml.Should().Contain("SelectedValue=\"{Binding SelectedUpdateChannel, Mode=TwoWay}\"",
            "y el canal de actualizaciones se elige con la propiedad del view model (el control no guarda estado propio)");

        // Las órdenes del gestor las ejecuta el view model: la vista no descarga ni borra modelos por su cuenta.
        string panel = Code(PanelCode);
        foreach (string command in new[]
                 {
                     "RefreshStatusCommand", "DownloadMissingModelsCommand", "OpenModelsFolderCommand",
                     "DeleteModelCommand", "DownloadModelCommand", "CheckForUpdatesNowCommand",
                 })
        {
            panel.Should().Contain(command, $"la orden {command} es del view model portable, no de la vista");
        }

        // El gestor lo resuelve el contenedor del núcleo, con el mismo reparto que la ventana del escritorio (y
        // el constructor de respaldo del hito 255 cuando el contenedor no lo tiene).
        panel.Should().Contain("services.GetService<AiModelManagerViewModel>()",
            "el gestor de modelos se resuelve del contenedor del núcleo antes de construirlo");
    }

    /// <summary>
    /// La acción de <b>URLs por modelo</b> de la pestaña de modelos de IA (hito 262): la fila la dibuja, el
    /// host la sirve y el editor escribe donde escribe el escritorio. Las tres mitades quedan atadas aquí.
    ///
    /// <para><b>Por qué esta prueba existe</b>: el tramo anterior dejó esta frontera <i>declarada</i> —la
    /// pestaña de modelos de IA existía, pero no ofrecía la edición de URLs por fila, así que la clave
    /// <see cref="FileFlow.Sdk.Services.DialogKeys.AiModelUrlsConfig"/> no podía servirse—. Ahora la acción
    /// existe, así que la declaración deja de ser cierta y la clave tiene que estar SERVIDA. Y el modo de
    /// fallo que más importa no es que falte la ventana: es que el botón exista y haga <i>otra cosa</i> —la
    /// rama de la acción es la que decide entre abrir el editor y caer en la descarga, y una fila que descarga
    /// cuando le pides sus URLs es peor que un botón que no hace nada.</para>
    ///
    /// <para><b>Y la mitad que impide dos verdades</b>: el cuerpo NO escribe la configuración. La escribe el
    /// view model portable (<c>Save()</c> → <c>SetCustomUrls</c>), que es el mismo camino del escritorio; si la
    /// vista llamara al almacén por su cuenta, habría dos sitios escribiendo lo mismo.</para>
    /// </summary>
    [Fact]
    public void TheModelUrlAction_ShouldOpenTheServedEditor_AndWriteWhereTheDesktopWrites()
    {
        string xaml = Read(PanelXaml);

        int listStart = xaml.IndexOf("AutomationProperties.AutomationId=\"SettingsAiModelsList\"", StringComparison.Ordinal);
        listStart.Should().BeGreaterThan(-1, "la pestaña de modelos de IA tiene que tener su lista");
        int listEnd = xaml.IndexOf("</ListView>", listStart, StringComparison.Ordinal);
        listEnd.Should().BeGreaterThan(listStart, "y su lista tiene que cerrarse: sin eso no se puede saber qué dibuja la fila");

        string row = xaml[listStart..listEnd];
        var actions = Regex.Matches(row, "Tag=\"([a-z]+)\"")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();

        var servedHere = new[] { "delete", "download", "urls" };
        actions.Should().BeEquivalentTo(servedHere,
            "la fila dibuja exactamente las acciones que este host cumple: añadir una cuarta obliga a decidir su " +
            "destino, y quitar la de URLs deja la edición de URLs sin punto de entrada");
        xaml.Should().Contain("AutomationProperties.AutomationId=\"SettingsAiModelUrlsButton\"",
            "la acción de URLs lleva su ancla: el canal externo (y la sonda) la encuentran por ahí");

        // 1) La rama de la acción abre el EDITOR: el comando canónico del gestor, no la descarga del `default`.
        string panel = Code(PanelCode);
        panel.Should().Contain("case \"urls\":",
            "la acción de URLs tiene su propia rama; sin ella cae en la de descarga y el botón haría otra cosa");
        panel.Should().Contain("manager.ConfigureUrlsCommand.ExecuteAsync(model)",
            "y esa rama ejecuta la orden CANÓNICA del gestor de modelos: es la que pide el diálogo por el catálogo");

        // 2) La clave está SERVIDA (ya no declarada) y su arm del catálogo es el que hace que la orden llegue.
        string service = Code(WindowService);
        service.Should().Contain("(DialogKeys.AiModelUrlsConfig, nameof(AiModelUrlsConfigBody)),",
            "con la acción dibujada, la clave tiene que estar servida por el host con su vista");
        service.Should().NotContain("(DialogKeys.AiModelUrlsConfig, \"",
            "y ya no puede seguir declarada como pendiente: una clave servida y declarada a la vez no dice nada");
        service.Should().Contain("case DialogKeys.AiModelUrlsConfig when payload is string modelId:",
            "el arm del catálogo es el que convierte la orden del gestor en una ventana: sin él la acción caería " +
            "en «no servido por este host»");
        service.Should().Contain("ShowAiModelUrlsAsync(modelId, root)",
            "y ese arm abre el editor del host");
        service.Should().Contain("new AiModelUrlsConfigViewModel(modelId, new UnoDialogService()",
            "el editor se sirve del view model portable de las URLs, con el adaptador de diálogos del host " +
            "(por donde el view model pide su aviso, igual que en el escritorio)");

        // 3) El cuerpo es una vista del view model portable y NO escribe la configuración por su cuenta.
        string body = Code(BodyCode);
        body.Should().Contain("private readonly AiModelUrlsConfigViewModel _vm;",
            "el cuerpo recibe el view model portable construido: no se lo fabrica él");
        body.Should().Contain("_vm.Save()",
            "el guardado es el del view model portable: el mismo método que usa la ventana del escritorio");
        body.Should().NotContain("SetCustomUrls",
            "la vista NO escribe la configuración: eso es del view model (dos sitios escribiendo lo mismo serían " +
            "dos verdades sobre las URLs de un modelo)");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. El arranque: lo GUARDADO es lo APLICADO
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La otra mitad de la superficie: dentro de ella el tema y el idioma se aplican al guardar, pero al
    /// <b>abrir</b> la aplicación quien tiene que aplicar lo guardado es el host — una preferencia es un dato,
    /// y el núcleo no aplica el tema ni la cultura por su cuenta. Medido antes del arreglo: con
    /// <c>light_studio</c> guardado y el host recién abierto, el tema aplicado era el de por defecto
    /// (<c>dark_fluent</c>) y la sonda lo canta en su renglón de arranque.
    /// </summary>
    [Fact]
    public void TheStartup_ShouldApplyTheSavedThemeAndLanguage()
    {
        string app = Code(AppCode);

        app.Should().Contain("ApplySavedPreferences(s_services);",
            "el host aplica lo guardado en el arranque; sin esa llamada el ajuste se guarda y no se aplica");

        int window = app.IndexOf("s_mainWindow = new MainWindow();", StringComparison.Ordinal);
        int applied = app.IndexOf("ApplySavedPreferences(s_services);", StringComparison.Ordinal);
        int activate = app.IndexOf("s_mainWindow.Activate();", StringComparison.Ordinal);
        window.Should().BeGreaterThan(0);
        applied.Should().BeGreaterThan(window,
            "la ventana se crea ANTES de aplicar lo guardado: el repintado del tema pasa por ella y sin " +
            "ventana la publicación se pierde sin ruido (medido en la sesión de playtest: marco oscuro con " +
            "'light_studio' guardado, con el gestor de temas ya en el tema correcto)");
        activate.Should().BeGreaterThan(applied,
            "y lo guardado se aplica ANTES de activar la ventana: el usuario no ve el tema de por defecto " +
            "ni un fotograma");

        app.Should().Contain("string language = LanguageCatalog.Resolve(savedLanguage)?.Code ?? LanguageCatalog.All[0].Code;",
            "el idioma guardado se normaliza a uno de los que ofrece la aplicación (un valor ausente o heredado " +
            "dejaría el desplegable de idioma sin nada que mostrar)");
        app.Should().Contain("LocalizationManager.Instance.SetCulture(language);",
            "la cultura de arranque es el idioma guardado, no la del sistema");
        app.Should().Contain("string themeId = ThemeManager.ResolveThemeId(savedTheme) ?? ThemeManager.DefaultThemeId;",
            "el tema guardado se traduce antes de aplicarlo: las preferencias viejas escriben 'Dark' y el " +
            "catálogo usa 'dark_fluent'");
        app.Should().Contain("SetThemeById(themeId);",
            "y el identificador traducido es el que se aplica");

        // El escritorio es la referencia de comportamiento: el mismo arranque, los mismos dos pasos.
        string desktop = Code("FileFlow.App/App.axaml.cs");
        desktop.Should().Contain("LocalizationManager.Instance.SetCulture(language);",
            "el escritorio ya normalizaba y aplicaba el idioma guardado (el host Uno copia su arranque, no lo inventa)");
        desktop.Should().Contain("ThemeManager.ResolveThemeId(savedTheme) ?? ThemeManager.DefaultThemeId",
            "y ya aplicaba el tema guardado traducido");

        // La sonda mide el arranque ANTES de tocar nada y contra el valor GUARDADO: si lo midiera después de su
        // propia mudanza de tema e idioma, mediría la medición y no lo que el usuario tiene al abrir la app.
        string panel = Code(PanelCode);
        int arranque = panel.IndexOf("startup = string.Equals(canonicalStoredTheme, appliedThemeId", StringComparison.Ordinal);
        int mudanza = panel.IndexOf("ThemeCombo.SelectedItem = requestedTheme;", StringComparison.Ordinal);
        arranque.Should().BeGreaterThan(0, "el sondeo mide el arranque: es la mitad de la superficie que no se ve desde dentro");
        mudanza.Should().BeGreaterThan(arranque,
            "y lo mide antes de cambiar el tema y el idioma (después mediría su propia mudanza)");

        Code(SelfCheckCode).Should().Contain("el tema y el idioma GUARDADOS del usuario son los que están puestos al arrancar",
            "el renglón del arranque está en el veredicto del sondeo, no sólo en el detalle");

        Read("mutations/arranque-que-no-aplica-el-tema-guardado.json")
            .Should().Contain("\"FullyQualifiedName~TheStartup_ShouldApplyTheSavedThemeAndLanguage\"",
                "esta guardia es el testigo de la mutación que quita la aplicación del tema guardado");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2.b El diccionario del host: dos idiomas, sin claves huérfanas
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Las claves declaradas en un resx del host.</summary>
    private static HashSet<string> DictionaryKeys(string relativePath) =>
        Regex.Matches(Read(relativePath), "<data name=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Todas las claves <c>Uno_*</c> citadas por el código y el XAML del host.</summary>
    private static HashSet<string> CitedKeys()
    {
        string root = Path.Combine(TestRepositoryLocator.RepositoryRoot(), HostRoot);
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                                 || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                     .Where(f => !f.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase)
                                 && !f.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase))
                     .Where(f => !f.Contains(@"\Resources\", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), "\"(Uno_[A-Za-z0-9_]+)\""))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    [Fact]
    public void BothHostDictionaries_ShouldDeclareTheSameKeys()
    {
        var english = DictionaryKeys(StringsEnglish);
        var spanish = DictionaryKeys(StringsSpanish);

        english.Should().NotBeEmpty("el host resuelve sus textos por diccionario, no por literales incrustados");
        spanish.Should().BeEquivalentTo(english,
            "un idioma al que le falte una entrada cae al valor neutro del otro: el usuario elegiría un idioma " +
            "y leería el otro en la mitad de la interfaz");
    }

    [Fact]
    public void EveryCitedUnoKey_ShouldExistInBothDictionaries()
    {
        var english = DictionaryKeys(StringsEnglish);
        var spanish = DictionaryKeys(StringsSpanish);
        var orphans = CitedKeys()
            .Where(key => !english.Contains(key))
            .OrderBy(key => key)
            .ToList();

        orphans.Should().BeEmpty(
            "una clave citada por el host y ausente del diccionario se resuelve por el fallback incrustado en " +
            "el código: el proceso cambia de cultura y el texto se queda en el idioma del fallback (el defecto " +
            "que la superficie de ajustes mide)");

        spanish.Should().Contain(english.ToArray());
    }

    [Fact]
    public void TheProbeExpectation_ShouldMatchTheDictionaryText()
    {
        // El sondeo afirma que el texto del marco CAMBIA con el idioma comparando contra estos dos literales:
        // si el diccionario cambia de texto y el sondeo no, la medición cantaría un fallo que no es del
        // producto (o peor: pasaría sin que la cadena viniera del diccionario).
        Read(StringsEnglish).Should().Contain("<data name=\"Uno_Settings_Save\" xml:space=\"preserve\"><value>Save settings</value></data>",
            "el sondeo espera 'Save settings' en inglés: el literal y el diccionario tienen que decir lo mismo");
        Read(StringsSpanish).Should().Contain("<data name=\"Uno_Settings_Save\" xml:space=\"preserve\"><value>Guardar ajustes</value></data>",
            "y 'Guardar ajustes' en español");

        Code(PanelCode).Should().Contain("string.Equals(languageCode, \"en-US\", StringComparison.OrdinalIgnoreCase) ? \"Save settings\" : \"Guardar ajustes\"",
            "la expectativa del sondeo vive en un solo sitio (ExpectedSaveText) y es la que ata la prueba al diccionario");
    }

    [Fact]
    public void TheHostDictionary_ShouldBeRegisteredAtStartup()
    {
        Code(AppCode).Should().Contain("new System.Resources.ResourceManager(\"FileFlow.App.Uno.Resources.Strings\", typeof(App).Assembly)",
            "sin registrar el diccionario del host, GetString resuelve siempre el fallback y el idioma no cambia");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. La medición: modo propio, dos tiempos, y restauración de lo del usuario
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheProbe_ShouldHaveItsOwnMode_OutsideTheCanvasProbes()
    {
        Code(AppCode).Should().Contain("\"--selfcheck-settings\"",
            "la superficie de ajustes se mide en su propio modo: su medición cambia tema e idioma (estado global) " +
            "y los sondeos del lienzo no toleran esa mudanza a mitad");
        Code(AppCode).Should().Contain("SelfCheckSettings.Run(",
            "el modo propio llama al corredor del sondeo de ajustes");

        string selfCheck = Code(SelfCheckCode);
        selfCheck.Should().Contain("public static int Run(Window window, DispatcherQueue dispatcher)",
            "el corredor del sondeo de ajustes existe en SU archivo y es el único dueño de sus dos tiempos");
        selfCheck.Should().Contain("panel.OpenForMeasurement()",
            "primer tiempo: desplegar y dejar a la vista la sección que se va a medir");
        selfCheck.Should().Contain("result = panel.ProbeSettingsSurface();",
            "segundo tiempo: medir, restaurar y recoger");
        selfCheck.Should().Contain("Thread.Sleep(800);",
            "entre los dos tiempos el layout tiene que asentar: el enlace de un control recién hecho visible no " +
            "está vivo en el mismo callback (medido)");

        // El sondeo del lienzo NO corre la sonda de ajustes (medido: conviviendo se caen la sonda de selección,
        // la de paneles y la de foco, que no tienen nada que ver con los ajustes). Antes de la partición del
        // hito 276 los dos corredores convivían en un mismo fichero y había que mirar el CUERPO de Inspect para
        // verlo; ahora cada modo vive en su archivo, así que la separación se lee de un vistazo.
        SourceText.CodeWithoutComments("FileFlow.App.Uno/SelfCheckCanvas.cs").Should().NotContain("SelfCheckSettings",
            "el sondeo del lienzo NO llama al de ajustes: su medición mueve tema e idioma, que es estado " +
            "global, y las sondas del lienzo no toleran esa mudanza a mitad");

        Code(PanelCode).Should().Contain("internal bool OpenForMeasurement()",
            "el primer tiempo vive en la vista (es ella quien sabe qué sección se mide)");
    }

    [Fact]
    public void TheRestore_ShouldReturnWhatTheUserHad_NotAConstant()
    {
        string panel = Code(PanelCode);

        panel.Should().Contain("LanguageCombo.SelectedValue = storedLanguage;",
            "la vuelta del idioma es al idioma GUARDADO del usuario: restaurar una constante le cambiaría la " +
            "preferencia por haber medido la superficie");
        panel.Should().Contain("vm.SelectedThemeId = storedTheme;",
            "la red de seguridad devuelve la preferencia de tema GUARDADA (el identificador aplicado puede no ser " +
            "el que el usuario tenía escrito)");
        panel.Should().Contain("vm.SelectedLanguage = storedLanguage;",
            "y el idioma guardado, igual");
        panel.Should().Contain("vm.MaxParallelThreads = storedThreads;",
            "y los hilos guardados");
        panel.Should().Contain("themes.SetThemeById(appliedThemeId);",
            "lo APLICADO y lo GUARDADO son dos cosas distintas: el tema que se ve se devuelve por su identificador " +
            "aplicado, y la preferencia por su valor guardado");
        panel.Should().Contain("loc.SetCulture(storedLanguage);",
            "y la cultura se devuelve al idioma guardado, no al de la prueba");

        // La vuelta del usuario viaja por el MISMO camino que la superficie escribe: si el host tuviera una
        // segunda vía de escritura de preferencias, el sondeo podría dejar el fichero y la interfaz contando
        // cosas distintas (y la del host no pasaría ni por SetCulture ni por SetThemeById).
        panel.Should().NotContain("UpdatePreferences",
            "la red de seguridad restaura por los setters del view model y su comando de guardado, no escribiendo " +
            "el fichero a mano");

        Read("mutations/ajuste-que-no-devuelve-el-idioma.json")
            .Should().Contain("\"FullyQualifiedName~TheRestore_ShouldReturnWhatTheUserHad_NotAConstant\"",
                "esta guardia es el testigo de la mutación que devuelve una constante en vez de lo del usuario");
    }

    [Fact]
    public void TheDictionaryCensus_ShouldHaveAMutationThatBites()
    {
        Read("mutations/ajuste-sin-su-texto.json")
            .Should().Contain("\"FullyQualifiedName~EveryCitedUnoKey_ShouldExistInBothDictionaries\"",
                "esta guardia lee los resx del host: borrar una entrada del diccionario la hace caer de verdad");
    }
}
