using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La guardia del <b>MENÚ PRINCIPAL</b> del host Uno (hito 257): la barra de control del escritorio y su
/// cajón, portados sobre el MISMO <c>ControlBarViewModel</c> portable.
///
/// <para><b>Qué protege</b>: (1) que la barra sea una VISTA del view model —cada entrada ejecuta una orden
/// canónica, no una copia de la lógica en el host—; (2) la <b>paridad de entradas</b> contra el escritorio:
/// toda orden que la barra o el cajón del escritorio dibujan (o disparan por atajo) está dibujada aquí,
/// declarada pendiente o reconocida como cumplida por el host, así que portar una entrada a medias cae aquí;
/// (3) que el <b>estado por contexto</b> salga del view model (ejecutar fuera mientras corre, deshacer según
/// el editor) y no de una copia local; (4) que los textos sean <b>los del escritorio</b>, copiados clave por
/// clave en los dos idiomas —una traducción propia del host sería otra interfaz—; y (5) que su medición viva
/// en su propio modo (<c>--selfcheck-controlbar</c>), fuera de los sondeos del lienzo, que no toleran que
/// les muevan el documento a mitad.</para>
/// </summary>
public class UnoControlBarParityGuardTests
{
    private const string BarXaml = "FileFlow.App.Uno/Controls/ControlBar.xaml";
    private const string BarCode = "FileFlow.App.Uno/Controls/ControlBar.xaml.cs";
    private const string DrawerXaml = "FileFlow.App.Uno/Controls/MainMenuDrawer.xaml";
    private const string DrawerCode = "FileFlow.App.Uno/Controls/MainMenuDrawer.xaml.cs";
    private const string WindowXaml = "FileFlow.App.Uno/MainWindow.xaml";
    private const string WindowCode = "FileFlow.App.Uno/MainWindow.xaml.cs";
    private const string SelfCheckCode = "FileFlow.App.Uno/RuntimeSelfCheck.cs";
    private const string AppCode = "FileFlow.App.Uno/App.xaml.cs";
    private const string StringsEnglish = "FileFlow.App.Uno/Resources/Strings.resx";
    private const string StringsSpanish = "FileFlow.App.Uno/Resources/Strings.es.resx";

    /// <summary>La barra del escritorio y su cajón: la referencia de paridad.</summary>
    private const string DesktopBarXaml = "FileFlow.App/Views/ControlBarView.axaml";
    private const string DesktopWindowXaml = "FileFlow.App/MainWindow.axaml";
    private const string DesktopStringsEnglish = "FileFlow.App/Resources/Strings.resx";
    private const string DesktopStringsSpanish = "FileFlow.App/Resources/Strings.es.resx";

    /// <summary>Dónde vive la orden de una entrada: en el code-behind (un comando) o en el XAML (un enlace).</summary>
    private enum Home
    {
        Code,
        Xaml,
    }

    private static string Code(string relativePath) => SourceText.CodeWithoutComments(relativePath);

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath));

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. El censo: cada entrada, dónde vive su orden y su estado
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Las entradas dibujadas en el host, con el AutomationId que las ancla al canal externo, la vista que
    /// las dibuja, DÓNDE vive su orden y cuál es esa orden. La columna <see cref="Home"/> es lo que separa dos
    /// cosas que se parecen: un botón cuyo manejador ejecuta un comando del view model (<c>Home.Code</c>) y un
    /// control cuyo enlace bidireccional ES la orden —la casilla del Modo Prueba, los desplegables de tema y
    /// idioma: su orden es la propiedad, y buscarla en el code-behind sería buscar donde no está.
    ///
    /// <para>La columna <c>State</c> es el estado por CONTEXTO: la propiedad del view model que el propio
    /// control consulta para visibilidad o habilitación.</para>
    ///
    /// <para>Las dos entradas de AJUSTES son paridad del cajón del escritorio
    /// (<c>OpenWorkflowSettingsCommand</c>), que en este host tiene una superficie propia —la del hito 255— y
    /// no una ventana: las dos la abren por el mismo evento, y el AutomationId de la barra es el que la sesión
    /// de los ajustes ya usaba.</para>
    /// </summary>
    private static IReadOnlyList<(string Id, string View, Home Home, string Order, string State)> EntryCensus() =>
    [
        ("ControlBarMenuButton", BarXaml, Home.Code, "ToggleMenuCommand", string.Empty),
        ("ControlBarDryRunToggle", BarXaml, Home.Xaml, "IsDryRun", string.Empty),
        ("ControlBarWatchButton", BarXaml, Home.Code, "ToggleWatchModeCommand", string.Empty),
        ("ControlBarRunButton", BarXaml, Home.Code, "ExecuteWorkflowCommand", "Visibility=IsRunning"),
        ("ControlBarDebugButton", BarXaml, Home.Code, "DebugWorkflowCommand", "Visibility=IsRunning"),
        ("ControlBarStepButton", BarXaml, Home.Code, "StepNextCommand", "Visibility=IsDebugging"),
        ("ControlBarContinueButton", BarXaml, Home.Code, "ContinueWorkflowCommand", "Visibility=IsDebugging"),
        ("ControlBarPauseButton", BarXaml, Home.Code, "TogglePauseCommand", "Visibility=IsRunning"),
        ("ControlBarStopButton", BarXaml, Home.Code, "StopWorkflowCommand", "Visibility=IsRunning"),
        ("ControlBarUndoButton", BarXaml, Home.Code, "UndoCommand", "IsEnabled=CanUndo"),
        ("ControlBarRedoButton", BarXaml, Home.Code, "RedoCommand", "IsEnabled=CanRedo"),
        ("ControlBarRollbackButton", BarXaml, Home.Code, "RollbackLastExecutionCommand", string.Empty),
        ("ControlBarInspectorButton", BarXaml, Home.Code, "ToggleInspectorCommand", string.Empty),
        ("SettingsButton", BarXaml, Home.Code, "SettingsRequested", string.Empty),
        ("ControlBarThemeCombo", DrawerXaml, Home.Xaml, "SelectedTheme", string.Empty),
        ("ControlBarLanguageCombo", DrawerXaml, Home.Xaml, "SelectedLanguage", string.Empty),
        ("ControlBarDrawerSettingsButton", DrawerXaml, Home.Code, "SettingsRequested", string.Empty),
        ("ControlBarDrawerInspectorButton", DrawerXaml, Home.Code, "ToggleInspectorCommand", string.Empty),
        ("ControlBarDrawerCloseButton", DrawerXaml, Home.Code, "ToggleMenuCommand", string.Empty),

        // Hito 258 — las seis entradas nuevas del cajón. Las tres de FLUJO declaran su orden con el
        // EVENTO del host (la cumple la ventana con sus diálogos asíncronos: el comando del núcleo pide
        // el contrato síncrono, que desde el hilo de UI devuelve nulo/falso), y las tres de AYUDA
        // ejecutan la orden CANÓNICA del núcleo, como sus botones del escritorio.
        ("ControlBarDrawerNewButton", DrawerXaml, Home.Code, "NewWorkflowRequested", string.Empty),
        ("ControlBarDrawerLoadButton", DrawerXaml, Home.Code, "LoadWorkflowRequested", string.Empty),
        ("ControlBarDrawerSaveButton", DrawerXaml, Home.Code, "SaveWorkflowRequested", string.Empty),
        ("ControlBarDrawerManualButton", DrawerXaml, Home.Code, "OpenUserManualCommand", string.Empty),
        ("ControlBarDrawerExamplesButton", DrawerXaml, Home.Code, "OpenExamplesFolderCommand", string.Empty),
        ("ControlBarDrawerAboutButton", DrawerXaml, Home.Code, "OpenAboutDialogCommand", string.Empty),

        // Hito 259 — las CUATRO entradas con VENTANA: su orden es el comando CANÓNICO del núcleo, que pide
        // su ventana al catálogo de diálogos (el host las sirve). Las dos de la barra son las que tienen
        // estado por contexto: el chip del VFS sólo aparece cuando la última ejecución dejó archivos
        // virtuales y el distintivo de actualización cuando el arranque encontró una novedad.
        ("ControlBarVfsButton", BarXaml, Home.Code, "OpenVirtualFileSystemExplorerCommand", "Visibility=HasVirtualFiles"),
        ("ControlBarUpdateBadge", BarXaml, Home.Code, "OpenUpdateDialogCommand", "Visibility=HasPendingUpdate"),
        ("ControlBarDrawerThemeStudioButton", DrawerXaml, Home.Code, "OpenThemeCustomizerCommand", string.Empty),
        ("ControlBarDrawerMetricsButton", DrawerXaml, Home.Code, "OpenMetricsDashboardCommand", string.Empty),
        ("ControlBarDrawerVfsButton", DrawerXaml, Home.Code, "OpenVirtualFileSystemExplorerCommand", string.Empty),

        // Hito 261 — el DISEÑADOR DE DATASETS: su orden es el EVENTO del cajón, porque el comando canónico del
        // núcleo construye la ventana que monta el plugin con el toolkit del escritorio. La ventana pide al
        // nodo la superficie que declara al SDK y el catálogo del host la sirve sobre el mismo view model.
        ("ControlBarDrawerDataSetButton", DrawerXaml, Home.Code, "DataSetDesignerRequested", string.Empty),
    ];

    [Fact]
    public void EveryEntry_ShouldExistInItsViewWithItsAnchorAndItsState()
    {
        var views = new Dictionary<string, string>
        {
            [BarXaml] = Read(BarXaml),
            [DrawerXaml] = Read(DrawerXaml),
        };
        var missing = new List<string>();

        foreach (var (id, view, _, _, state) in EntryCensus())
        {
            if (!views[view].Contains($"AutomationProperties.AutomationId=\"{id}\"", StringComparison.Ordinal))
            {
                missing.Add($"{id}: su vista ({view}) no lo declara con su AutomationId");
            }

            if (state.Length == 0)
            {
                continue;
            }

            // El estado por contexto: el XAML tiene que nombrar la propiedad del view model en el enlace del
            // MISMO control (no basta con que exista en algún sitio del fichero).
            if (!ControlBlock(views[view], id).Contains(state.Split('=')[1], StringComparison.Ordinal))
            {
                missing.Add($"{id}: su estado ({state}) no sale del view model en su propio control");
            }
        }

        missing.Should().BeEmpty(
            "cada entrada del menú principal tiene que existir en su vista con su ancla de automatización y su "
            + "estado colgado del view model portable; sin eso el canal externo no la encuentra o el contexto "
            + "no se sigue");
    }

    [Fact]
    public void EveryEntry_ShouldRunACanonicalOrder_NotACopyOfIt()
    {
        string barCode = Code(BarCode);
        string drawerCode = Code(DrawerCode);

        foreach (var (id, view, home, order, _) in EntryCensus())
        {
            // La orden tiene que estar donde vive: el comando en el code-behind de la vista, el enlace en su
            // XAML. Buscarla en el artefacto equivocado es como no buscarla.
            string artifact = home == Home.Code
                ? (view == BarXaml ? barCode : drawerCode)
                : Read(view);
            artifact.Should().Contain(order,
                $"la entrada {id} ({view}) tiene que declarar su orden {order} en {(home == Home.Code ? "su code-behind" : "su XAML")}");
        }

        // Y la vista no puede reimplementarla: nada de construir el view model ni de fabricar el ciclo.
        foreach (string code in new[] { barCode, drawerCode })
        {
            code.Should().NotContain("new ControlBarViewModel(",
                "el view model de la barra viene del contenedor del núcleo: construirlo en la vista crearía "
                + "un segundo ciclo con su propio estado");
            code.Should().NotContain("WorkflowExecutionCoordinator",
                "la ejecución la orquesta el núcleo; el host no puede saltarse el ciclo canónico");
        }

        // Las casillas y los desplegables del censo van por enlace bidireccional (su orden es la propiedad).
        Read(BarXaml).Should().Contain("IsChecked=\"{Binding IsDryRun, Mode=TwoWay}\"",
            "el Modo Prueba es una casilla atada al view model: sin el TwoWay, marcar la casilla no cambia el ciclo");
        Read(DrawerXaml).Should().Contain("SelectedValue=\"{Binding SelectedTheme, Mode=TwoWay}\"");
        Read(DrawerXaml).Should().Contain("SelectedValue=\"{Binding SelectedLanguage, Mode=TwoWay}\"");
    }

    [Fact]
    public void TheHost_ShouldMountThePortableControlBarViewModel()
    {
        Code(WindowCode).Should().Contain("Bar.Vm = mainVm.ControlBar;",
            "la barra consume el ControlBar del núcleo portable, el mismo que el botón Ejecutar del hito 243");
        Code(WindowCode).Should().Contain("Drawer.Vm = mainVm.ControlBar;",
            "la barra y su cajón comparten la MISMA instancia: el botón «Menú» conmuta el estado y el cajón lo sigue");
        Read(WindowXaml).Should().Contain("<controls:ControlBar",
            "la barra tiene que estar montada en la ventana: sin ella no hay menú principal");
        Read(WindowXaml).Should().Contain("<controls:MainMenuDrawer",
            "el cajón tiene que estar montado sobre toda la ventana, como el del escritorio");

        // Las dos entradas a los ajustes (barra y cajón) abren la misma superficie del host.
        Code(WindowCode).Should().Contain("Bar.SettingsRequested += OnOpenSettingsClicked;");
        Code(WindowCode).Should().Contain("Drawer.SettingsRequested += OnOpenSettingsClicked;");
        Code(WindowCode).Should().Contain("private void OnOpenSettingsClicked(object sender, RoutedEventArgs e) => Settings.Open();",
            "la orden de ajustes sigue siendo la del hito 255: la superficie del host, sin duplicar su montaje");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. La paridad de entradas contra el escritorio (lo que no llega, declarado)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Las órdenes que el escritorio dibuja en su barra o en su cajón, o dispara por atajo (la referencia de
    /// paridad).
    ///
    /// <para>Se leen los DOS modos de enlace del escritorio: el de la barra (<c>{Binding XCommand}</c>, cuyo
    /// contexto es el <c>ControlBar</c>) y el de su ventana (<c>{Binding ControlBar.XCommand}</c>, donde el cajón
    /// cuelga del nodo principal). Mirar solo el primero dejaba fuera justamente las órdenes del cajón —la mitad
    /// del menú— y con ellas el <c>OpenWorkflowSettingsCommand</c> que este host cumple por su cuenta: el patrón
    /// con el que se mide y el que se escribe tienen que ser el mismo.</para>
    ///
    /// <para>Las órdenes del <c>Editor</c> (los atajos del lienzo: deshacer, copiar, zoom…) NO entran: son otra
    /// superficie, ya cubierta por sus guardias.</para>
    /// </summary>
    private static IReadOnlyList<string> DesktopCommands()
    {
        var commands = new List<string>();
        foreach (string path in new[] { DesktopBarXaml, DesktopWindowXaml })
        {
            foreach (Match match in Regex.Matches(Read(path), @"Command=""\{Binding (?:ControlBar\.)?([A-Za-z]+Command)\}\"""))
            {
                string command = match.Groups[1].Value;
                if (!commands.Contains(command, StringComparer.Ordinal))
                {
                    commands.Add(command);
                }
            }
        }

        commands.Should().Contain("ToggleMenuCommand",
            "la referencia de paridad tiene que leerse de verdad: una lectura que no encuentre ni el botón de "
            + "menú del escritorio está midiendo el vacío");
        commands.Should().Contain("OpenWorkflowSettingsCommand",
            "las órdenes del cajón viven en el XAML de la ventana del escritorio, tras el prefijo ControlBar.");
        return commands;
    }

    [Fact]
    public void EveryDesktopOrder_ShouldBeDrawnHere_OrDeclaredByTheHost()
    {
        var drawn = EntryCensus().Select(e => e.Order).ToHashSet(StringComparer.Ordinal);
        var pending = EmptyableTable("DeclaredPendingEntries");
        var hostOwned = Table("HostOwnedOrders");

        var orphans = DesktopCommands()
            .Where(command => !drawn.Contains(command)
                              && !pending.Contains(command)
                              && !hostOwned.Contains(command))
            .ToList();

        orphans.Should().BeEmpty(
            "toda orden del menú principal del escritorio tiene que estar dibujada en el host, declarada "
            + "pendiente o reconocida como cumplida por el host: «lo que no llega queda declarado, nunca fingido»");

        // Las dos tablas son disjuntas: una orden o la cumple el host por su cuenta o está pendiente, nunca
        // las dos cosas.
        pending.Should().NotIntersectWith(hostOwned,
            "una orden o la cumple el host o está pendiente: declararla de las dos maneras sería no decirla");

        // Y lo declarado NO puede estar dibujado: sería una mentira en la tabla. Se mira el TEXTO de las vistas
        // (no el censo) porque una entrada a medio portar puede estar dibujada sin estar censada, que es justo
        // el caso que hay que atrapar.
        string hostViews = Read(BarXaml) + Read(DrawerXaml);
        foreach (string declared in pending.Concat(hostOwned))
        {
            hostViews.Should().NotContain(declared,
                $"la orden declarada {declared} no puede estar además dibujada en la vista: si ya está, se "
                + "quita de la tabla");
        }

        // Las tablas son las del host, no copias en la guardia: se leen del propio control, que es donde las
        // encuentra quien lo mantiene.
        Code(BarCode).Should().Contain("internal static readonly (string Entry, string Reason)[] DeclaredPendingEntries");
        Code(BarCode).Should().Contain("internal static readonly (string Entry, string Reason)[] HostOwnedOrders");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2.b Los ATAJOS: lo que el host ya enruta, y lo que no
    // ─────────────────────────────────────────────────────────────────────────────

    private const string CanonicalShortcutTable = "FileFlow.App.Core/Services/EditorKeyboardShortcuts.cs";

    /// <summary>
    /// Los atajos que el escritorio liga en su ventana a una orden del <c>ControlBar</c> (gesto y orden).
    /// Se leen de sus <c>KeyBinding</c>: es la mitad del menú que no se pulsa, se teclea.
    /// </summary>
    private static IReadOnlyList<(string Gesture, string Order)> DesktopShortcuts()
    {
        var shortcuts = new List<(string, string)>();
        foreach (Match match in Regex.Matches(
                     Read(DesktopWindowXaml),
                     @"<KeyBinding\s+Gesture=""([^""]+)""\s+Command=""\{Binding ControlBar\.([A-Za-z]+Command)\}"""))
        {
            var pair = (match.Groups[1].Value, match.Groups[2].Value);
            if (!shortcuts.Contains(pair))
            {
                shortcuts.Add(pair);
            }
        }

        shortcuts.Should().NotBeEmpty(
            "la referencia de atajos tiene que leerse de verdad: si la lectura no encuentra ni un KeyBinding "
            + "del escritorio, el caso estaría midiendo el vacío");
        return shortcuts;
    }

    /// <summary>
    /// Los atajos que el host SÍ enruta, leídos de su tabla (gesto -> tecla -> orden). La tabla es la que
    /// enruta —el manejador de la ventana la recorre—, así que quitarla es dejar la tecla muda.
    /// </summary>
    private static IReadOnlyList<(string Gesture, string Key, string Order)> RoutedShortcutsTable()
    {
        string code = Code(BarCode);
        int at = code.IndexOf("RoutedShortcuts =", StringComparison.Ordinal);
        at.Should().BeGreaterThan(-1, "el host tiene que declarar los atajos que enruta");
        int end = code.IndexOf("];", at, StringComparison.Ordinal);
        return Regex.Matches(code[at..end],
                @"\(""([^""]+)"", ""([^""]+)"", (?:true|false), (?:true|false), ""([A-Za-z]+Command)""")
            .Select(m => (Gesture: m.Groups[1].Value, Key: m.Groups[2].Value, Order: m.Groups[3].Value))
            .ToList();
    }

    /// <summary>Los atajos que el host declara SIN ruta (hoy ninguno: los seis están enrutados).</summary>
    private static IReadOnlyList<(string Gesture, string Key, string Order)> DeclaredUnroutedTable()
    {
        string code = Code(BarCode);
        int at = code.IndexOf("DeclaredUnroutedShortcuts =", StringComparison.Ordinal);
        at.Should().BeGreaterThan(-1, "el control tiene que declarar su tabla de atajos sin ruta");
        int end = code.IndexOf("];", at, StringComparison.Ordinal);
        return Regex.Matches(code[at..end], @"\(""([^""]+)"", ""([^""]+)"", ""([A-Za-z]+Command)""")
            .Select(m => (Gesture: m.Groups[1].Value, Key: m.Groups[2].Value, Order: m.Groups[3].Value))
            .ToList();
    }

    [Fact]
    public void EveryDesktopShortcut_ShouldBeRoutedHere_OrDeclaredUnrouted()
    {
        var routed = RoutedShortcutsTable();
        var declared = DeclaredUnroutedTable();
        routed.Should().NotBeEmpty("la tabla de atajos enrutados del host tiene que leerse del código");

        var orphan = DesktopShortcuts()
            .Where(pair => !routed.Any(d => d.Gesture == pair.Gesture && d.Order == pair.Order)
                           && !declared.Any(d => d.Gesture == pair.Gesture && d.Order == pair.Order))
            .ToList();
        orphan.Should().BeEmpty(
            "todo atajo del menú principal del escritorio tiene que estar enrutado en el host o declarado en "
            + "DeclaredUnroutedShortcuts con su razón: un atajo que no llega y no se declara es un usuario "
            + "apretando una tecla que no hace nada");

        // Una tecla no puede estar en las dos tablas: enrutada y declarada sin ruta a la vez sería no
        // decirla. (Y al enrutar una tecla nueva hay que quitar su fila de la tabla de declaradas.)
        var gestures = routed.Select(r => r.Gesture).ToHashSet(StringComparer.Ordinal);
        declared.Where(d => gestures.Contains(d.Gesture)).Should().BeEmpty(
            "ningún atajo puede estar enrutado y declarado sin ruta a la vez");

        // Y la tabla del lienzo sigue siendo la del LIENZO: los atajos del menú no la tocan (si una tecla
        // enrutada por el menú estuviera en la tabla canónica del lienzo, dos dueños se la disputarían).
        string canvas = Code(CanonicalShortcutTable);
        var canvasKeys = Regex.Matches(canvas, @"new\(PhysicalKey\.([A-Za-z0-9]+),")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        canvasKeys.Should().NotBeEmpty("la tabla canónica del lienzo tiene que leerse del código");

        var lies = declared.Where(d => canvasKeys.Contains(d.Key))
            .Select(d => $"{d.Gesture} ({d.Order}): su tecla {d.Key} SÍ está en la tabla del lienzo")
            .ToList();
        lies.Should().BeEmpty(
            "un atajo declarado como no enrutado no puede estar en la tabla canónica que el host ya enruta");
    }

    /// <summary>
    /// Las tres órdenes de FLUJO del menú (Nuevo / Cargar / Guardar) NO se quedan en un botón mudo, y el
    /// reparto entre ellas es distinto a propósito.
    ///
    /// <para><b>Nuevo Flujo</b> la cumple el COMANDO del núcleo (hito 265): su confirmación va por el
    /// contrato ASÍNCRONO, que este host sí contesta, así que la ventana ya no confirma por su cuenta —era el
    /// host haciendo la pregunta del producto—. <b>Cargar y Guardar</b> siguen pidiéndose a la ventana: su
    /// mitad de diálogo es el selector de FICHERO, que sigue siendo SÍNCRONO en el contrato y desde el hilo de
    /// UI devuelve nulo, así que ejecutar su comando dejaría el botón mudo (el modo de fallo exacto de un
    /// portado a medias: la entrada dibujada, la orden invocada y el efecto ausente, sin crash y sin mensaje).</para>
    ///
    /// <para>La guardia lo cierra por los dos lados —lo que la ventana tiene que usar y lo que no puede
    /// usar— y exige que quien sabe hacerlo sin diálogo sea el view model portable, no una copia en la vista.</para>
    /// </summary>
    [Fact]
    public void TheFlowOrders_ShouldBeFulfilledByTheHostsOwnAsyncChannel_NotByTheSilentSyncOne()
    {
        string drawer = Code(DrawerCode);
        string window = Code(WindowCode);

        // El cajón declara QUÉ se ha pedido (no ejecuta el comando mudo).
        drawer.Should().Contain("NewWorkflowRequested?.Invoke(this, e);");
        drawer.Should().Contain("LoadWorkflowRequested?.Invoke(this, e);");
        drawer.Should().Contain("SaveWorkflowRequested?.Invoke(this, e);");

        // Nuevo Flujo: la ventana ejecuta la orden CANÓNICA, que pregunta por el contrato asíncrono. Ni una
        // línea de confirmación en la vista —preguntar es del producto— ni una llamada a CreateNewWorkflow
        // desde aquí (quien ya tiene la respuesta no necesita el diálogo, pero el host no la tiene).
        //
        // Y la BARRA enruta su atajo POR EL MISMO CAMINO: la orden es una sola en el host y sus dos pasos de
        // host —el rastro y el renglón del ciclo que lee el canal externo— se hacen una vez y en el mismo sitio
        // (si la barra ejecutara el comando por su cuenta, Ctrl+N dejaría el renglón con el grafo viejo).
        Code(BarCode).Should().Contain("NewWorkflowRequested?.Invoke(this, new RoutedEventArgs());",
            "el atajo de la barra tiene que pedir la orden a la ventana, no ejecutarla por su cuenta");
        window.Should().Contain("await _controlBar.NewWorkflowCommand.ExecuteAsync(null);");
        window.Should().NotContain("_controlBar.CreateNewWorkflow();");
        window.Should().NotContain("ShowConfirmationAsync(",
            "la confirmación la pide el comando del núcleo por su contrato asíncrono: repetirla aquí sería "
            + "preguntar dos veces (o preguntar por el producto en la vista)");

        // Cargar y Guardar: las APIs asíncronas del host y los métodos portables del view model.
        window.Should().Contain("ShowOpenFileDialogAsync(");
        window.Should().Contain("ShowSaveFileDialogAsync(");
        window.Should().Contain("await _controlBar.LoadWorkflowFromFileAsync(filePath);");
        window.Should().Contain("await _controlBar.SaveWorkflowToFileAsync(filePath);");
        window.Should().NotContain("LoadWorkflowCommand.Execute");
        window.Should().NotContain("SaveWorkflowCommand.Execute");

        // Quien sabe hacerlo sin diálogo es el view model portable, no la vista.
        string core = Code("FileFlow.App.Core/ViewModels/ControlBarViewModel.cs");
        core.Should().Contain("public void CreateNewWorkflow()");
        core.Should().Contain("public async Task NewWorkflowAsync()");
        core.Should().Contain("public async Task SaveWorkflowToFileAsync(string filePath)");
        core.Should().Contain("public async Task LoadWorkflowFromFileAsync(string filePath)");
        window.Should().NotContain("ClearGraph(",
            "el flujo nuevo lo crea el view model portable; la vista no reimplementa el producto");
        window.Should().NotContain("ExportToGraphModel(",
            "el guardado lo hace el view model portable; la vista sólo pone la ruta");

        // La barra cumple su mitad por la tabla: es ELLA la que enruta, no un switch paralelo.
        Code(BarCode).Should().Contain("in RoutedShortcuts)");
        Code(BarCode).Should().Contain("internal bool RouteShortcut(Windows.System.VirtualKey key, bool control, bool shift)");
    }

    /// <summary>Las entradas de la tabla del control que se llame así, leídas del propio código fuente.</summary>
    private static HashSet<string> Table(string tableName) =>
        ReadTable(tableName, allowEmpty: false);

    /// <summary>
    /// La tabla, admitiendo que esté VACÍA. Hace falta para la de declaradas: desde el hito 261 no queda
    /// ninguna orden del escritorio sin servir, y una tabla vacía es la verdad —no una tabla que no se lee—.
    /// Que la tabla EXISTA (con su guardia y su razón) lo comprueba el caso del censo.
    /// </summary>
    private static HashSet<string> EmptyableTable(string tableName) =>
        ReadTable(tableName, allowEmpty: true);

    private static HashSet<string> ReadTable(string tableName, bool allowEmpty)
    {
        string code = Code(BarCode);
        int at = code.IndexOf(tableName + " =", StringComparison.Ordinal);
        at.Should().BeGreaterThan(-1, $"el control tiene que declarar la tabla {tableName}");

        int end = code.IndexOf("];", at, StringComparison.Ordinal);
        end.Should().BeGreaterThan(at, $"la tabla {tableName} tiene que cerrarse con '];'");

        var declared = Regex.Matches(code[at..end], @"""([A-Za-z]+Command)"", ""[^""]+""")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        if (!allowEmpty)
        {
            declared.Should().NotBeEmpty($"la tabla {tableName} tiene que leerse desde el código, con su razón");
        }

        return declared;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2.c Las VENTANAS del menú (hito 259): las sirve el catálogo de diálogos del host
    // ─────────────────────────────────────────────────────────────────────────────

    private const string WindowServiceCode = "FileFlow.App.Uno/Platform/UnoWindowService.cs";
    private const string DialogKeysCode = "FileFlow.Sdk/Services/IWindowService.cs";

    /// <summary>
    /// Las cuatro entradas del hito 259 abren su ventana por el CATÁLOGO DE DIÁLOGOS del host: la orden
    /// canónica del núcleo pide una clave y el servicio la sirve. Sin esta mitad, la entrada existiría y su
    /// orden pediría una ventana que nadie tiene —el modo de fallo que el censo declara ya no puede quedar
    /// escondido—.
    /// </summary>
    [Fact]
    public void TheWindowEntries_ShouldBeServedByTheHostsDialogCatalogue()
    {
        string service = Code(WindowServiceCode);
        string bar = Code(BarCode);
        string drawer = Code(DrawerCode);

        // La tabla de lo servido: las cuatro entradas con su destino, declaradas en el control (no en la guardia).
        var served = Table("ServedWindowEntries");
        served.Should().BeEquivalentTo(
            ["OpenThemeCustomizerCommand", "OpenMetricsDashboardCommand", "OpenVirtualFileSystemExplorerCommand", "OpenUpdateDialogCommand"],
            "las cuatro entradas con ventana del hito 259 tienen que estar en la tabla de lo servido, que es lo que"
            + " la mantiene legible cuando se añada la siguiente (el Diseñador de Datasets del hito 261 va por su"
            + " propio contrato y vive en HostOwnedOrders)");

        // Y cada una tiene que estar DIBUJADA en el host: la tabla no sustituye a la entrada. Se busca la
        // orden FUERA de las tablas de declaración —una fila de `ServedWindowEntries` nombra la orden y
        // eso no es dibujarla—, en el code-behind o en el XAML de las dos vistas.
        string drawn = WithoutDeclarationTables(bar) + drawer + Read(BarXaml) + Read(DrawerXaml);
        foreach (string command in served)
        {
            drawn.Should().Contain(command,
                $"la entrada servida {command} tiene que estar dibujada en la barra o en el cajón: la tabla de "
                + "lo servido lo deja escrito, pero no es la entrada");
            service.Should().Contain($"(DialogKeys.",
                "y el servicio tiene que seguir sirviendo claves del catálogo con su vista");
        }

        // El catálogo: las claves SERVIBLES (con su vista) y las que quedan declaradas con su razón. Las
        // declaradas no pueden aparecer como implementadas —sería una mentira en la tabla—.
        foreach (string key in new[] { "ThemeCustomizer", "WorkflowMetricsDashboard", "VirtualFileSystemExplorer", "UpdateDialog", "DataSetDesigner", "AiModelUrlsConfig" })
        {
            service.Should().Contain($"(DialogKeys.{key},", $"la clave {key} tiene que estar entre las servidas");
        }

        // El editor de URLs por modelo entró aquí en el hito 262: cuando su acción pasó a estar dibujada en la
        // fila del catálogo, la clave dejó de poder declararse pendiente. Sólo queda una declarada, y es una
        // decisión: la superficie de ajustes tiene su puerta en la barra y el cajón, y abrirla por este canal
        // sería una segunda copia de lo mismo.
        foreach (string key in new[] { "WorkflowSettings" })
        {
            service.Should().Contain($"(DialogKeys.{key}, \"", $"la clave {key} tiene que seguir declarada con su razón");
        }

        service.Should().NotContain("(DialogKeys.AiModelUrlsConfig, \"",
            "y la de URLs por modelo ya NO puede estar declarada: está servida, y declararla además sería no decir nada");

        // El censo completo: ninguna clave del catálogo puede quedarse fuera de las dos tablas.
        string keys = Code(DialogKeysCode);
        var all = Regex.Matches(keys, @"public const string [A-Za-z]+ = ""([A-Za-z]+)"";")
            .Select(m => m.Groups[1].Value)
            .ToList();
        all.Should().HaveCountGreaterThanOrEqualTo(9,
            "el catálogo de claves tiene que leerse de verdad del contrato del SDK");

        string serviceRaw = Code(WindowServiceCode);
        var missing = all.Where(key => !serviceRaw.Contains($"DialogKeys.{key}", StringComparison.Ordinal)).ToList();
        missing.Should().BeEmpty(
            "toda clave del catálogo tiene que aparecer en la tabla de servidas o en la de declaradas: una clave"
            + " nueva sin destino es un diálogo que se pide y se cae sin decir nada");
    }

    /// <summary>
    /// El ESTUDIO DE TEMAS es la ventana que más se apoya en el escritorio, así que declara sus partes
    /// pendientes en su propia tabla: las DOS órdenes que necesitan el selector de fichero SÍNCRONO (que desde
    /// el hilo de UI devuelve nulo) y la vista previa en vivo. La guardia exige que estén declaradas y que NO
    /// estén dibujadas —un botón cuyo destino no existe sería la mentira con forma de botón que el proyecto no
    /// acepta—, y por el otro lado exige DIBUJADA la que dejó de serlo: «Eliminar tema» (hito 265), cuya orden
    /// ya pregunta por el contrato asíncrono.
    /// </summary>
    [Fact]
    public void TheThemeStudio_ShouldDeclareWhatItCannotServe_AndNotDrawIt()
    {
        string body = Code("FileFlow.App.Uno/Controls/ThemeCustomizerBody.xaml.cs");
        string view = Read("FileFlow.App.Uno/Controls/ThemeCustomizerBody.xaml");

        body.Should().Contain("internal static readonly (string Part, string Reason)[] DeclaredPendingParts");
        foreach (string part in new[] { "ExportThemeAsyncCommand", "ImportThemeAsyncCommand", "LivePreviewResources" })
        {
            body.Should().Contain(part, $"la parte {part} del estudio del escritorio tiene que estar declarada con su razón");
            view.Should().NotContain(part, $"y no puede estar además dibujada en la vista: si ya está, se quita de la tabla");
        }

        // «Eliminar tema» salió de esa tabla en el hito 265: su orden ya pregunta por el contrato ASÍNCRONO de
        // diálogos (el que este host contesta), así que el botón se DIBUJA y se ejecuta su comando canónico. Una
        // parte dibujada NO puede seguir declarada pendiente, y una declarada no puede estar dibujada: la
        // guardia lo exige por los dos lados.
        body.Should().NotContain("\"DeleteThemeCommand\"",
            "si el botón está dibujado, la fila se quita de la tabla de pendientes");
        view.Should().Contain("AutomationProperties.AutomationId=\"ThemeStudioDeleteButton\"",
            "el borrado del estudio tiene que tener su botón: su orden ya se puede cumplir en este host");
        body.Should().Contain("private void OnDeleteClicked(object sender, RoutedEventArgs e) => _vm.DeleteThemeCommand.Execute(null);",
            "y su manejador ejecuta la orden del view model: la vista no confirma ni borra por su cuenta");

        // Las órdenes que SÍ se dibujan son las canónicas del view model portable, no copias en la vista.
        foreach (string order in new[] { "NewCustomThemeCommand", "DuplicateThemeCommand", "DeleteThemeCommand",
                                          "ApplyToApplicationCommand", "SaveAndApplyCommand" })
        {
            body.Should().Contain(order + ".Execute(null)", $"la orden {order} del estudio la ejecuta su view model, no la vista");
        }

        body.Should().NotContain("RemoveCustomTheme(",
            "borrar un tema lo hace el servicio de temas del núcleo: la vista no reimplementa el producto");
        body.Should().NotContain("ShowConfirmation(",
            "y la confirmación es del producto (el contrato asíncrono del view model), no de la vista");
    }

    /// <summary>
    /// El AVISO DE ACTUALIZACIÓN tiene que estar alimentado por alguien: el host comprueba las actualizaciones
    /// al arrancar (la misma mitad del escritorio) y ese resultado es el que enciende el distintivo. Sin la
    /// comprobación, el distintivo no se enciende nunca y el aviso que el host ya sirve no lo pide nadie.
    /// </summary>
    [Fact]
    public void TheUpdateCheck_ShouldFeedTheBadge_AndStayOutOfTheProbes()
    {
        string app = Code(AppCode);

        // La LLAMADA, no sólo la definición: un método de comprobación que nadie invoca deja el distintivo
        // apagado para siempre y no rompe nada — el fallo exacto que esta guardia tiene que ver.
        app.Should().Contain("StartUpdateCheck(s_services);",
            "el arranque del host tiene que ARRANCAR la comprobación de actualizaciones: definirla y no "
            + "llamarla deja el aviso invisible para siempre");
        app.Should().Contain("CheckForUpdatesAsync(",
            "y la comprobación tiene que ser la del servicio del núcleo, la misma que la del escritorio");
        app.Should().Contain("ApplyPendingUpdate(",
            "y entregar la novedad a la ventana, que es quien tiene el view model de la barra");
        app.Should().Contain("StartsWith(\"--selfcheck\"",
            "la comprobación se SALTA ENTERA en los modos de sondeo —no basta con ignorar su resultado—: su "
            + "veredicto tiene que ser hermético y una novedad real abriría un aviso en mitad de la medición");

        Code(WindowCode).Should().Contain("internal void ApplyPendingUpdate(",
            "la ventana es la que tiene el ControlBar del núcleo y la que puede encender el distintivo");
        Code(WindowCode).Should().Contain("_controlBar?.SetPendingUpdate(info)",
            "y lo enciende por el MISMO camino del escritorio (ControlBar.SetPendingUpdate)");

        string bar = Code(BarCode);
        bar.Should().Contain("OpenUpdateDialogCommand", "el distintivo ejecuta la orden canónica del aviso");
        bar.Should().Contain("PendingUpdateVersionTag",
            "y su rótulo sale de la versión que el arranque anunció, no de una copia en la vista");
    }

    /// <summary>
    /// El código de la barra SIN sus tablas de declaración (censo de declaradas, de cumplidas por el host y
    /// de entradas servidas): lo que quede es lo que la vista HACE.
    ///
    /// <para>Hace falta para medir de verdad un dibujado: una tabla que nombra la orden dice que la entrada
    /// existe, no que se ejecute —y confundir las dos cosas deja pasar una entrada dibujada cuyo manejador se
    /// quedó vacío, que es justo el fallo que este tramo quiere que se vea.</para>
    /// </summary>
    private static string WithoutDeclarationTables(string barCode)
    {
        string result = barCode;
        foreach (string table in new[] { "DeclaredPendingEntries", "HostOwnedOrders", "ServedWindowEntries" })
        {
            int at = result.IndexOf(table + " =", StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            int end = result.IndexOf("];", at, StringComparison.Ordinal);
            if (end > at)
            {
                result = result.Remove(at, end - at);
            }
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. Los textos son los del escritorio, en los dos idiomas
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>De la clave del host a la clave del diccionario del escritorio: el texto se COPIA, no se re-traduce.</summary>
    private static IReadOnlyList<(string Host, string Desktop)> SharedTexts() =>
    [
        ("Uno_ControlBar_Menu", "MenuBtn"),
        ("Uno_ControlBar_MenuToolTip", "ControlBar_MenuToolTip"),
        ("Uno_ControlBar_DryRun", "DryRun"),
        ("Uno_ControlBar_DryRunToolTip", "ControlBar_DryRunToolTip"),
        ("Uno_ControlBar_Watcher", "ControlBar_Watcher"),
        ("Uno_ControlBar_WatchToolTip", "ControlBar_WatchModeToolTip"),
        ("Uno_ControlBar_Run", "RunFlow"),
        ("Uno_ControlBar_Debug", "DebugFlow"),
        ("Uno_ControlBar_StepNext", "StepNext"),
        ("Uno_ControlBar_StepNextToolTip", "ControlBar_StepNextToolTip"),
        ("Uno_ControlBar_Continue", "ContinueFlow"),
        ("Uno_ControlBar_ContinueToolTip", "ControlBar_ContinueToolTip"),
        ("Uno_ControlBar_Pause", "Pause"),
        ("Uno_ControlBar_PauseToolTip", "ControlBar_PauseToolTip"),
        ("Uno_ControlBar_Stop", "Stop"),
        ("Uno_ControlBar_StopToolTip", "ControlBar_StopToolTip"),
        ("Uno_ControlBar_Undo", "UndoBtn"),
        ("Uno_ControlBar_UndoToolTip", "UndoToolTip"),
        ("Uno_ControlBar_Redo", "RedoBtn"),
        ("Uno_ControlBar_RedoToolTip", "RedoToolTip"),
        ("Uno_ControlBar_Rollback", "RollbackExecutionBtn"),
        ("Uno_ControlBar_RollbackToolTip", "RollbackExecutionToolTip"),
        ("Uno_ControlBar_Inspector", "InspectorBtn"),
        ("Uno_ControlBar_InspectorToolTip", "ControlBar_InspectorToolTip"),
        ("Uno_Drawer_Subtitle", "Drawer_AppSubtitle"),
        ("Uno_Drawer_AppearanceLanguage", "Drawer_AppearanceLanguage"),
        ("Uno_Drawer_ThemeLabel", "Drawer_ThemeLabel"),
        ("Uno_Drawer_LanguageLabel", "Drawer_LanguageLabel"),
        ("Uno_Drawer_PanelsTools", "Drawer_PanelsTools"),
        ("Uno_Drawer_Settings", "Drawer_Settings"),

        // Hito 258 — las secciones y entradas nuevas del cajón, y la ventana «Acerca de».
        ("Uno_Drawer_FlowManagement", "Drawer_FlowManagement"),
        ("Uno_Drawer_NewWorkflow", "Drawer_NewWorkflow"),
        ("Uno_Drawer_LoadWorkflow", "Drawer_LoadWorkflow"),
        ("Uno_Drawer_SaveWorkflow", "Drawer_SaveWorkflow"),
        ("Uno_Drawer_HelpResources", "Drawer_HelpResources"),
        ("Uno_Drawer_UserManual", "Drawer_UserManual"),
        ("Uno_Drawer_UserManualToolTip", "Drawer_UserManualToolTip"),
        ("Uno_Drawer_ExampleFlows", "Drawer_ExampleFlows"),
        ("Uno_Drawer_ExampleFlowsToolTip", "Drawer_ExampleFlowsToolTip"),
        ("Uno_Drawer_About", "Drawer_About"),
        ("Uno_Drawer_AboutToolTip", "Drawer_AboutToolTip"),
        ("Uno_About_Title", "About_Title"),
        ("Uno_About_Subtitle", "About_Subtitle"),
        ("Uno_About_Description", "About_Description"),
        ("Uno_About_Accept", "Common_Accept"),
    ];

    [Fact]
    public void TheSharedTexts_ShouldBeTheDesktopOnes_InBothLanguages()
    {
        foreach (var (hostPath, desktopPath) in new[]
                 {
                     (StringsEnglish, DesktopStringsEnglish),
                     (StringsSpanish, DesktopStringsSpanish),
                 })
        {
            var host = Dictionary(hostPath);
            var desktop = Dictionary(desktopPath);
            var wrong = new List<string>();

            foreach (var (hostKey, desktopKey) in SharedTexts())
            {
                if (!host.TryGetValue(hostKey, out string? hostValue))
                {
                    wrong.Add($"{hostKey}: falta en {hostPath}");
                    continue;
                }

                if (!desktop.TryGetValue(desktopKey, out string? desktopValue))
                {
                    wrong.Add($"{desktopKey}: falta en {desktopPath} (la referencia)");
                    continue;
                }

                if (!string.Equals(hostValue, desktopValue, StringComparison.Ordinal))
                {
                    wrong.Add($"{hostKey}='{hostValue}' contra {desktopKey}='{desktopValue}'");
                }
            }

            wrong.Should().BeEmpty(
                "los textos de la barra y del cajón son los del escritorio, copiados: una traducción propia "
                + $"sería otra interfaz ({Path.GetFileName(hostPath)})");
        }
    }

    [Fact]
    public void EveryTextUsedByTheViews_ShouldExistInBothHostDictionaries()
    {
        var english = Dictionary(StringsEnglish);
        var spanish = Dictionary(StringsSpanish);
        string views = Code(BarCode) + Code(DrawerCode);

        var cited = Regex.Matches(views, @"""(Uno_[A-Za-z_]+)""")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        cited.Should().NotBeEmpty("las vistas de la barra citan sus claves del diccionario del host");

        var missing = cited.Where(key => !english.ContainsKey(key) || !spanish.ContainsKey(key)).ToList();
        missing.Should().BeEmpty(
            "sin entrada en los dos diccionarios, GetString resuelve el fallback incrustado y el cambio de "
            + "idioma deja la mitad del marco en el idioma equivocado");

        english.Keys.OrderBy(k => k, StringComparer.Ordinal)
            .Should().Equal(spanish.Keys.OrderBy(k => k, StringComparer.Ordinal),
                "los dos diccionarios del host declaran las mismas claves");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3.b El DISEÑADOR DE DATASETS: la superficie la declara el NODO, y el host la sirve
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El DISEÑADOR DE DATASETS del hito 261 es la única superficie cuya ORDEN no la cumple ni el comando
    /// canónico ni el canal asíncrono de la ventana: la cumple el contrato de superficie del SDK, que declara
    /// el propio nodo del plugin. Esta guardia ata las cuatro piezas de esa cadena — el contrato en el SDK, el
    /// nodo que lo implementa con su clave y su view model PORTABLE, el servicio del host que sirve esa clave
    /// con vista propia, y esa vista como vista del view model del plugin (no una segunda versión del
    /// diseñador).
    ///
    /// <para>Sin el contrato, el host tendría que conocer el tipo del plugin por su nombre; sin la clave en el
    /// catálogo, la superficie no tendría identidad compartida; y sin esta guardia, la vista podría empezar a
    /// reimplementar el diseñador sin que nadie lo note.</para>
    /// </summary>
    [Fact]
    public void TheDataSetDesigner_ShouldBeDeclaredByTheNode_AndServedByTheHost()
    {
        const string Contract = "FileFlow.Sdk/Descriptors/INodeDialogSurfaceProvider.cs";
        const string Node = "FileFlow.Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs";
        const string ViewModel = "FileFlow.Plugin.FileSystem/UI/ViewModels/SyntheticDataSetDesignerViewModel.cs";
        const string BodyCode = "FileFlow.App.Uno/Controls/DataSetDesignerBody.xaml.cs";
        const string BodyXaml = "FileFlow.App.Uno/Controls/DataSetDesignerBody.xaml";

        // 1. El contrato (SDK): qué diálogo quiere el nodo y qué contiene, sin tipos de UI.
        string contract = Code(Contract);
        contract.Should().Contain("interface INodeDialogSurfaceProvider");
        contract.Should().Contain("string DialogKey");
        contract.Should().Contain("object? CreateDialogPayload(");

        // 2. El nodo: lo implementa, declara la clave del catálogo y su view model portable.
        string node = Code(Node);
        node.Should().Contain("INodeDialogSurfaceProvider",
            "el nodo de datos sintéticos es el dueño de la superficie: la declara él, no el host");
        node.Should().Contain("DialogKeys.DataSetDesigner",
            "y la identidad del diálogo es la clave del catálogo del SDK, la misma para todos los hosts");
        node.Should().Contain("new UI.ViewModels.SyntheticDataSetDesignerViewModel(",
            "y su carga útil es el view model portable, no una ventana");
        node.Should().Contain("(context as NodeCustomActionContext)?.Dialogs",
            "y viaja con los diálogos de quien abre: sin ellos el contenido cae al doble nulo, que a una "
            + "confirmación contesta «sí» sin preguntar");

        // El view model del plugin no puede depender de un toolkit: si lo hiciera, la «lógica portable» sería
        // una promesa y el host no podría pintarlo.
        string viewModel = Read(ViewModel);
        viewModel.Should().NotContain("using Avalonia", "el view model del diseñador tiene que seguir siendo portable");

        // 3. El servicio del host: sirve la clave con vista propia y declara que espera ESE view model.
        string service = Code(WindowServiceCode);
        service.Should().Contain("(DialogKeys.DataSetDesigner, nameof(DataSetDesignerBody))",
            "la clave tiene que estar entre las servidas, con la vista que la sirve");
        service.Should().Contain("payload is SyntheticDataSetDesignerViewModel designer",
            "y el servicio tiene que comprobar la carga útil que espera, no tragarse cualquier cosa");

        // 4. La vista del host es una vista del view model del PLUGIN: sus órdenes son sus comandos y la vista
        //    no reimplementa ni el almacén ni las reglas del diseñador.
        string body = Code(BodyCode);
        body.Should().Contain("SyntheticDataSetDesignerViewModel",
            "la vista del host pinta el view model del plugin, no una copia del diseñador");
        foreach (string command in new[]
                 {
                     "NewDataSetCommand", "SaveCommand", "DuplicateDataSetCommand", "DeleteDataSetCommand",
                     "ImportCommand", "ExportCommand", "AddFileCommand", "AddFolderCommand",
                     "AddArchiveToTreeCommand", "AddArchiveEntryCommand", "RemoveItemCommand",
                     "ExpandAllTreeCommand", "CollapseAllTreeCommand", "ApplyDslToItemsCommand", "ApplyJsonToItemsCommand",
                 })
        {
            Read(BodyXaml).Should().Contain("Command=\"{Binding " + command + "}\"",
                $"la orden {command} del diseñador la ejecuta su view model, no la vista");
        }

        body.Should().NotContain("new SyntheticDataSetDesignerViewModel(",
            "el view model lo construye quien lo declara (el nodo), no la vista: si lo construyera la vista, "
            + "habría dos diseñadores");
        body.Should().NotContain("SyntheticDataSetStorageService",
            "el almacén de datasets es del plugin: la vista no habla con él");
        body.Should().NotContain("SyntheticTreeDslParser",
            "y el parser del DSL también: la vista no reimplementa el producto");

        // Y la superficie tiene que estar censada en el cajón, que es por donde el usuario la alcanza.
        Code(DrawerCode).Should().Contain("ControlBarDrawerDataSetButton");
        Code(BarCode).Should().Contain("OpenSyntheticDataSetDesignerCommand",
            "la orden del escritorio tiene que quedar reconocida en el censo del host (cumplida por su canal)");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 4. La medición: modo propio, fuera de los sondeos del lienzo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheProbe_ShouldHaveItsOwnMode_OutsideTheCanvasProbes()
    {
        Code(AppCode).Should().Contain("\"--selfcheck-controlbar\"",
            "el sondeo del menú tiene su propio modo: su ciclo mueve el documento y las sondas del lienzo no "
            + "toleran esa mudanza a mitad");
        Code(AppCode).Should().Contain("RuntimeSelfCheck.RunControlBarProbe(");

        string selfCheck = Code(SelfCheckCode);
        selfCheck.Should().Contain("public static int RunControlBarProbe(Window window, DispatcherQueue dispatcher)");
        selfCheck.Should().Contain("selfcheck-controlbar-report.txt",
            "el veredicto tiene que quedar en su fichero, como el de las otras sondas");

        // La sonda del menú NO puede llamarse desde dentro del recorrido del lienzo: una sola mención (su
        // propia definición) es la prueba de que vive fuera.
        Regex.Matches(selfCheck, @"RunControlBarProbe").Count.Should().Be(1,
            "el sondeo del menú se arranca desde la línea de comandos, no desde el recorrido del lienzo");

        // Y ejerce las entradas por el canal del usuario, no por el view model directamente.
        string bar = Code(BarCode);
        bar.Should().Contain("FrameworkElementAutomationPeer",
            "la sonda pulsa el MISMO control que un lector de pantalla (el peer de automatización)");
        bar.Should().Contain("internal Control? EntryById(string id)");
        bar.Should().Contain("internal string CensusLine()");
    }

    // ─────────────────────────────────────────────────────────────────────────────

    private static Dictionary<string, string> Dictionary(string relativePath)
    {
        XDocument document = XDocument.Parse(Read(relativePath));
        return document.Root!.Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    /// <summary>El trozo del XAML del control con ese AutomationId (desde su etiqueta hasta el cierre de «&gt;»).</summary>
    private static string ControlBlock(string xaml, string id)
    {
        int at = xaml.IndexOf($"AutomationProperties.AutomationId=\"{id}\"", StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }

        int start = xaml.LastIndexOf('<', at);
        int end = xaml.IndexOf('>', at);
        return start >= 0 && end > start ? xaml[start..(end + 1)] : string.Empty;
    }
}
