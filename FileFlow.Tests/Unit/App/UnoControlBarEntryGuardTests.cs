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
/// La guardia del <b>CENSO DE ENTRADAS</b> del menú principal del host Uno (hito 257): la barra de control y su
/// cajón, portados sobre el MISMO <c>ControlBarViewModel</c> portable.
///
/// <para><b>Qué protege</b>: que cada entrada exista en su vista con su ancla de automatización y su estado
/// colgado del view model (no de una copia local); que ejecute la orden CANÓNICA y no una copia de ella; que
/// toda orden del escritorio esté dibujada aquí, declarada pendiente o reconocida como cumplida por el host;
/// que los atajos del menú del escritorio estén enrutados o declarados sin ruta, sin disputarle una tecla a la
/// tabla del lienzo; y que su medición viva en su propio modo (<c>--selfcheck-controlbar</c>), fuera de los
/// sondeos del lienzo, que no toleran que les muevan el documento a mitad.</para>
///
/// <para><b>Qué NO vive aquí</b>: las entradas que abren una VENTANA están en
/// <c>UnoControlBarSurfaceGuardTests</c> y la paridad de TEXTOS en <c>UnoControlBarTextsGuardTests</c>. Las
/// tablas del control que este censo lee las sirve <c>UnoControlBarTables</c>.</para>
/// </summary>
public class UnoControlBarEntryGuardTests
{
    private const string BarXaml = "FileFlow.App.Uno/Controls/ControlBar.xaml";
    private const string BarCode = "FileFlow.App.Uno/Controls/ControlBar.xaml.cs";
    private const string DrawerXaml = "FileFlow.App.Uno/Controls/MainMenuDrawer.xaml";
    private const string DrawerCode = "FileFlow.App.Uno/Controls/MainMenuDrawer.xaml.cs";
    private const string WindowXaml = "FileFlow.App.Uno/MainWindow.xaml";
    private const string WindowCode = "FileFlow.App.Uno/MainWindow.xaml.cs";
    private const string SelfCheckCode = "FileFlow.App.Uno/SelfCheckControlBar.cs";
    private const string AppCode = "FileFlow.App.Uno/App.xaml.cs";

    /// <summary>La barra del escritorio y su cajón: la referencia de paridad.</summary>
    private const string DesktopBarXaml = "FileFlow.App/Views/ControlBarView.axaml";
    private const string DesktopWindowXaml = "FileFlow.App/MainWindow.axaml";

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
        var pending = UnoControlBarTables.Of("DeclaredPendingEntries", allowEmpty: true);
        var hostOwned = UnoControlBarTables.Of("HostOwnedOrders");

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
    // 3. Los ATAJOS: lo que el host ya enruta, y lo que no
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

    // ─────────────────────────────────────────────────────────────────────────────
    // 4. La medición: modo propio, fuera de los sondeos del lienzo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheProbe_ShouldHaveItsOwnMode_OutsideTheCanvasProbes()
    {
        Code(AppCode).Should().Contain("\"--selfcheck-controlbar\"",
            "el sondeo del menú tiene su propio modo: su ciclo mueve el documento y las sondas del lienzo no "
            + "toleran esa mudanza a mitad");
        Code(AppCode).Should().Contain("SelfCheckControlBar.Run(");

        string selfCheck = Code(SelfCheckCode);
        selfCheck.Should().Contain("public static int Run(Window window, DispatcherQueue dispatcher)");
        selfCheck.Should().Contain("selfcheck-controlbar-report.txt",
            "el veredicto tiene que quedar en su fichero, como el de las otras sondas");

        // La sonda del menú NO puede llamarse desde dentro del recorrido del lienzo: cada modo vive en su
        // archivo (hito 276) y el del lienzo no lo nombra — la misma afirmación que antes medía el censo de
        // menciones, ahora sin depender de contar ocurrencias.
        SourceText.CodeWithoutComments("FileFlow.App.Uno/SelfCheckCanvas.cs").Should().NotContain("SelfCheckControlBar",
            "el sondeo del menú se arranca desde la línea de comandos, no desde el recorrido del lienzo");

        // Y ejerce las entradas por el canal del usuario, no por el view model directamente.
        string bar = Code(BarCode);
        bar.Should().Contain("FrameworkElementAutomationPeer",
            "la sonda pulsa el MISMO control que un lector de pantalla (el peer de automatización)");
        bar.Should().Contain("internal Control? EntryById(string id)");
        bar.Should().Contain("internal string CensusLine()");
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
