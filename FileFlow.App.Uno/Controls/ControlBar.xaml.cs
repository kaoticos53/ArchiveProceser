using System;
using System.Collections.Generic;
using System.ComponentModel;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// La barra de control del host Uno (el «menú principal» del encargo): la misma barra de la versión anterior
/// (<c>FileFlow.App/Views/ControlBarView.axaml</c>) sobre el MISMO <see cref="ControlBarViewModel"/>
/// portable, que el contenedor del núcleo ya resolvía para el botón Ejecutar del hito 243.
///
/// <para><b>Qué es paridad y qué no.</b> Las entradas dibujadas son las que en este host tienen destino
/// REAL, y cada una ejecuta el comando canónico del view model (nunca una copia de la lógica en la vista):
/// Modo Prueba, Vigilante, Ejecutar, Depurar, Siguiente Paso, Continuar, Pausar, Detener, Deshacer,
/// Rehacer, Revertir y el Inspector. Lo que el host no tiene —la ventana del explorador virtual, el
/// servicio de actualizaciones, las ventanas de tema/métricas/VFS/dataset/manual/acerca y las órdenes de
/// flujo que dependen de un diálogo síncrono de fichero— NO se dibuja: se declara (plan de la rebanada 5
/// y el informe de la sesión). Un botón que no hace nada sería una mentira con forma de botón.</para>
///
/// <para><b>El estado es del view model</b>, no una copia local: la isla del ciclo enseña Ejecutar/Depurar
/// cuando no hay ejecución y Pausar/Detener mientras corre, y Deshacer/Rehacer se deshabilitan cuando el
/// editor dice <c>CanUndo</c>/<c>CanRedo</c> en falso. Los textos salen del diccionario del host y se
/// reescriben en caliente al cambiar de idioma.</para>
/// </summary>
public sealed partial class ControlBar : UserControl
{
    /// <summary>
    /// Las entradas que la barra y el cajón del ESCRITORIO tienen y este host NO dibuja, con su razón.
    ///
    /// <para>Es el otro lado del censo: lo que no está no es un olvido, es una declaración. La guardia
    /// (<c>UnoControlBarParityGuardTests</c>) recorre las órdenes de la versión anterior y exige que cada una esté
    /// dibujada aquí o en esta tabla; añadir una entrada a la versión anterior sin portarla ni declararla la hace
    /// caer. «Lo que no llega queda declarado, nunca fingido»: un botón cuyo destino no existe sería una
    /// mentira con forma de botón.</para>
    /// </summary>
    internal static readonly (string Entry, string Reason)[] DeclaredPendingEntries =
    [
        // VACÍA desde el hito 261: la última orden que quedaba declarada —el Diseñador de Datasets— ya se
        // sirve, por el contrato de superficie del SDK. La tabla se queda (con su guardia) para que la
        // siguiente orden de la versión anterior que no se pueda servir tenga dónde declararse en vez de fingirse.
    ];

    /// <summary>
    /// Las entradas que la barra y el cajón del host dibujan y ejecutan por el COMANDO CANÓNICO del núcleo
    /// (hito 259), aunque su ventana la sirva el servicio del host: son las cuatro que pidieron el Estudio
    /// de temas, las Métricas, el VFS y el aviso de actualización.
    ///
    /// <para>Se declaran como tabla —y no se dejan implícitas en el XAML— para que el censo del host siga
    /// siendo legible: cada entrada con destino tiene aquí su fila, y la guardia exige que las órdenes de la
    /// versión anterior estén dibujadas, en <see cref="DeclaredPendingEntries"/> o en <see cref="HostOwnedOrders"/>.</para>
    /// </summary>
    internal static readonly (string Entry, string Where)[] ServedWindowEntries =
    [
        ("OpenThemeCustomizerCommand", "cajón, sección APARIENCIA E IDIOMA: abre el Estudio de temas del host"),
        ("OpenMetricsDashboardCommand", "cajón, sección PANELES Y HERRAMIENTAS: abre el panel de métricas del host"),
        ("OpenVirtualFileSystemExplorerCommand", "cajón y barra (VFS con su recuento): abre el explorador virtual del host"),
        ("OpenUpdateDialogCommand", "barra, distintivo con la versión nueva: abre el aviso de actualización del host"),
    ];

    /// <summary>
    /// Las órdenes del ESCRITORIO que este host cumple por su PROPIO canal —su evento y sus diálogos
    /// ASÍNCRONOS— en vez de por el enlace directo del núcleo, con su razón. Es el otro lado del censo: la
    /// orden no se pierde ni se finge, cambia de manos.
    ///
    /// <para>Son cuatro: <c>OpenWorkflowSettingsCommand</c> —el ítem «Ajustes» del cajón de la versión anterior—
    /// abre aquí la superficie de ajustes del propio host (hito 255) por el evento
    /// <see cref="SettingsRequested"/> en vez de por el comando del núcleo, porque ese comando delega en
    /// <c>IWindowService</c>, que en este host es el Nulo declarado; las dos de FICHERO (Cargar y Guardar)
    /// pasan por los eventos homónimos porque su selector de fichero sigue siendo síncrono en el contrato y
    /// desde el hilo de UI devuelve nulo; y «Nuevo Flujo» pasa por su evento para que la ventana refresque
    /// además el renglón del ciclo que lee el canal externo, aunque quien pregunta y crea sea ya el comando
    /// CANÓNICO del núcleo. La orden de la versión anterior se satisface; el mecanismo es el del host.</para>
    /// </summary>
    internal static readonly (string Entry, string Reason)[] HostOwnedOrders =
    [
        ("OpenWorkflowSettingsCommand", "la cumple el evento SettingsRequested de esta barra: el comando del núcleo abre una VENTANA por IWindowService, que en este host es el Nulo declarado"),
        ("OpenSyntheticDataSetDesignerCommand", "la cumple el evento DataSetDesignerRequested del cajón (hito 261): el comando del núcleo abre la ventana que monta el PROPIO plugin con el toolkit de la versión anterior, y un host WinUI no puede montar una ventana ajena. La ventana pide al nodo la superficie que declara al SDK (INodeDialogSurfaceProvider: clave DialogKeys.DataSetDesigner y view model portable) y el catálogo de diálogos del host la sirve con SU vista sobre ese mismo view model, sin reimplementar el diseñador"),
        // «Nuevo Flujo» ya NO pasa por el host para preguntar (hito 265): la confirmación la hace el comando
        // CANÓNICO del núcleo por el contrato asíncrono, y la ventana sólo lo ejecuta. Sigue declarado aquí
        // porque el disparo —el evento de la barra y del cajón— es del host: por ese camino la ventana
        // refresca también el renglón del ciclo que lee el canal externo.
        ("NewWorkflowCommand", "la cumple el canal propio del host: los eventos NewWorkflowRequested de la barra (Ctrl+N) y del cajón llegan a la ventana, que ejecuta la orden CANÓNICA del núcleo (la que confirma por el contrato ASÍNCRONO) y refresca el renglón del ciclo"),
        ("LoadWorkflowCommand", "la cumple el canal propio del host: el comando del núcleo pide su fichero por el contrato SÍNCRONO (que desde el hilo de UI devuelve nulo), así que la ventana usa el picker asíncrono y llama a ControlBarViewModel.LoadWorkflowFromFileAsync"),
        ("SaveWorkflowCommand", "la cumple el canal propio del host: misma frontera que Cargar; la ventana usa el picker asíncrono y llama a ControlBarViewModel.SaveWorkflowToFileAsync"),
    ];

    /// <summary>
    /// Los ATAJOS de la versión anterior que este host NO enruta, con la tecla física que los dispara allí.
    ///
    /// <para>La versión anterior liga F5 / F10 / Shift+F5 a las órdenes del ciclo y Ctrl+N / Ctrl+O / Ctrl+S a
    /// las de flujo, todo en los <c>KeyBinding</c> de su ventana. Este host enruta <b>sólo</b> los atajos del
    /// LIENZO (<c>EditorKeyboardShortcuts</c>: Ctrl+Z/Y/C/V/X/D, Supr, Retroceso, F2, Espacio, Escape): no
    /// tiene la tabla de la ventana de la versión anterior, así que esas seis combinaciones no hacen nada aquí. Se
    /// declaran —con su tecla, su orden y su razón— para que el hueco sea visible y para que enrutar una de
    /// ellas más adelante obligue a quitar su fila: la guardia exige que ninguna tecla de esta tabla esté en
    /// la tabla canónica del lienzo, así que un enrutado nuevo la pone en rojo en vez de pasar inadvertido.</para>
    /// </summary>
    /// <summary>
    /// Los ATAJOS de la versión anterior que este host SÍ enruta (hito 258), con la tecla física, los modificadores
    /// que la acompañan y la orden que dispara.
    ///
    /// <para><b>La tabla es la que enruta</b>: el manejador de teclado de la ventana la recorre y despacha
    /// por la columna <c>Order</c>; no hay una segunda lista en un <c>switch</c> que se pueda desincronizar.
    /// Quitar una fila deja su tecla muda y la guardia lo dice (la versión anterior la liga y aquí ya no estaría
    /// ni enrutada ni declarada).</para>
    ///
    /// <para><b>Las dos vías</b>: las tres del ciclo ejecutan el comando canónico del view model portable
    /// —el mismo que su botón de la barra—, y las tres de flujo pasan por el canal propio del host, porque
    /// su comando del núcleo pide un diálogo SÍNCRONO que desde el hilo de UI no puede cumplirse.</para>
    /// </summary>
    internal static readonly (string Gesture, string Key, bool Control, bool Shift, string Order, string Route)[] RoutedShortcuts =
    [
        ("F5", "F5", false, false, "ContinueWorkflowCommand", "el comando del ciclo del núcleo, el mismo del botón Continuar"),
        ("F10", "F10", false, false, "StepNextCommand", "el comando del ciclo del núcleo, el mismo del botón Siguiente Paso"),
        ("Shift+F5", "F5", false, true, "StopWorkflowCommand", "el comando del ciclo del núcleo, el mismo del botón Detener"),
        ("Ctrl+N", "N", true, false, "NewWorkflowCommand", "el comando del núcleo, que confirma por su contrato asíncrono y crea el flujo"),
        ("Ctrl+O", "O", true, false, "LoadWorkflowCommand", "el picker asíncrono del host elige la ruta y el ViewModel portable la carga"),
        ("Ctrl+S", "S", true, false, "SaveWorkflowCommand", "el picker asíncrono del host elige la ruta y el ViewModel portable guarda"),
    ];

    /// <summary>
    /// Los ATAJOS de la versión anterior que este host NO enruta, con la tecla física que los dispara allí.
    ///
    /// <para>La versión anterior liga F5 / F10 / Shift+F5 a las órdenes del ciclo y Ctrl+N / Ctrl+O / Ctrl+S a
    /// las de flujo, todo en los <c>KeyBinding</c> de su ventana. <b>Los seis están enrutados desde el hito
    /// 258</b> (ver <see cref="RoutedShortcuts"/>), así que esta tabla está vacía —y la guardia lo exige: si
    /// una fila volviera aquí con su tecla ya enrutada, la declaración mentiría—. La tabla se conserva
    /// porque es donde cae lo que vuelva a quedarse sin ruta, con su gesto y su razón.</para>
    /// </summary>
    internal static readonly (string Gesture, string Key, string Order, string Reason)[] DeclaredUnroutedShortcuts =
    [
        // (ninguna fila: el hito 258 enrutó los seis atajos del menú de la versión anterior)
    ];

    private ControlBarViewModel? _vm;

    /// <summary>La orden de abrir la superficie de AJUSTES; la ventana la conecta con su panel.</summary>
    public event RoutedEventHandler? SettingsRequested;

    /// <summary>Las tres órdenes de FLUJO que la barra enruta por atajo y cumple la ventana (hito 258).</summary>
    public event RoutedEventHandler? NewWorkflowRequested;

    /// <inheritdoc cref="NewWorkflowRequested"/>
    public event RoutedEventHandler? LoadWorkflowRequested;

    /// <inheritdoc cref="NewWorkflowRequested"/>
    public event RoutedEventHandler? SaveWorkflowRequested;

    public ControlBar()
    {
        InitializeComponent();
        RefreshLocalization();
    }

    /// <summary>El view model de la barra: el <c>ControlBar</c> del núcleo portable.</summary>
    public ControlBarViewModel? Vm
    {
        get => _vm;
        set
        {
            if (ReferenceEquals(_vm, value))
            {
                return;
            }

            if (_vm is not null)
            {
                _vm.PropertyChanged -= OnVmPropertyChanged;
            }

            _vm = value;
            DataContext = _vm;

            if (_vm is not null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
            }

            RefreshLocalization();
        }
    }

    /// <summary>
    /// El idioma vigente en los textos de la barra. Se resuelven con las claves del diccionario del host
    /// (con el texto de la versión anterior como fallback): el cambio de idioma reescribe estos controles.
    /// </summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        MenuLabel.Text = loc.GetString("Uno_ControlBar_Menu", "Menú");
        BrandLabel.Text = loc.GetString("Uno_ControlBar_Brand", "FileFlow Studio");

        DryRunToggle.Content = loc.GetString("Uno_ControlBar_DryRun", "🧪 Modo Prueba (Simulación)");
        WatchLabel.Text = loc.GetString("Uno_ControlBar_Watcher", "Vigilante");
        RunLabel.Text = loc.GetString("Uno_ControlBar_Run", "▶ Ejecutar Flujo");
        DebugLabel.Text = loc.GetString("Uno_ControlBar_Debug", "🐞 Depurar");
        StepLabel.Text = loc.GetString("Uno_ControlBar_StepNext", "⏭ Siguiente Paso");
        ContinueLabel.Text = loc.GetString("Uno_ControlBar_Continue", "⏯ Continuar");
        PauseLabel.Text = loc.GetString("Uno_ControlBar_Pause", "⏸ Pausar");
        StopLabel.Text = loc.GetString("Uno_ControlBar_Stop", "⏹ Detener");
        UndoLabel.Text = loc.GetString("Uno_ControlBar_Undo", "Deshacer");
        RedoLabel.Text = loc.GetString("Uno_ControlBar_Redo", "Rehacer");
        RollbackLabel.Text = loc.GetString("Uno_ControlBar_Rollback", "Revertir Archivos");
        InspectorLabel.Text = loc.GetString("Uno_ControlBar_Inspector", "🔍 Inspector");
        SettingsLabel.Text = loc.GetString("Uno_Drawer_Settings", "Ajustes");
        RefreshContextualLabels();

        SetTip(MenuButton, loc.GetString("Uno_ControlBar_MenuToolTip", "Abrir Menú Principal de Navegación"));
        SetTip(DryRunToggle, loc.GetString("Uno_ControlBar_DryRunToolTip",
            "Modo simulación: Ejecuta el flujo sin modificar ni borrar archivos reales en disco"));
        SetTip(WatchButton, loc.GetString("Uno_ControlBar_WatchToolTip",
            "Activa o detiene el Modo Vigilante en tiempo real para procesar automáticamente los archivos según caigan en las carpetas de origen."));
        SetTip(StepButton, loc.GetString("Uno_ControlBar_StepNextToolTip", "Avanzar al siguiente paso (F10)"));
        SetTip(ContinueButton, loc.GetString("Uno_ControlBar_ContinueToolTip", "Continuar ejecución normal (F5)"));
        SetTip(PauseButton, loc.GetString("Uno_ControlBar_PauseToolTip", "Pausar o Reanudar el flujo"));
        SetTip(StopButton, loc.GetString("Uno_ControlBar_StopToolTip", "Detener ejecución (Shift+F5)"));
        SetTip(UndoButton, loc.GetString("Uno_ControlBar_UndoToolTip", "Deshacer la última acción del lienzo (Ctrl+Z)"));
        SetTip(RedoButton, loc.GetString("Uno_ControlBar_RedoToolTip", "Rehacer la última acción deshecha (Ctrl+Y)"));
        SetTip(RollbackButton, loc.GetString("Uno_ControlBar_RollbackToolTip",
            "Revertir los cambios físicos en disco de la última ejecución"));
        SetTip(InspectorButton, loc.GetString("Uno_ControlBar_InspectorToolTip",
            "Abrir / Ocultar Inspector de Datos del Nodo"));
        SetTip(SettingsButton, loc.GetString("Uno_ControlBar_SettingsToolTip", "Ajustes"));
    }

    private static void SetTip(DependencyObject target, string text) => ToolTipService.SetToolTip(target, text);

    /// <summary>
    /// Los rótulos que llevan un NÚMERO del view model: el recuento de archivos virtuales y la versión nueva
    /// anunciada. Se reescriben cuando el view model los mueve (y al cambiar de idioma), porque WinUI no
    /// compone cadenas en el binding como el <c>StringFormat</c> de la versión anterior.
    /// </summary>
    private void RefreshContextualLabels()
    {
        var loc = LocalizationManager.Instance;

        VfsLabel.Text = loc.GetFormattedString("Uno_ControlBar_VfsCount", "VFS ({0})",
            _vm?.VirtualFilesCount ?? 0);
        UpdateLabel.Text = string.IsNullOrWhiteSpace(_vm?.PendingUpdateVersionTag)
            ? loc.GetString("Update_AvailableBadge", "Actualización")
            : _vm!.PendingUpdateVersionTag;

        SetTip(VfsButton, loc.GetString("VfsExplorer_HeaderTitle", "Explorador de Archivos Virtual"));
        SetTip(UpdateBadge, loc.GetString("Update_WindowTitle", "Actualización Disponible"));
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // El renglón del canal (y la sonda) necesitan ver el ciclo cambiar sin releer el view model:
        // el rastro se apoya en los PropertyChanged del MISMO VM que mueven los botones.
        if (e.PropertyName is nameof(ControlBarViewModel.IsRunning)
            or nameof(ControlBarViewModel.IsDebugging)
            or nameof(ControlBarViewModel.IsDryRun)
            or nameof(ControlBarViewModel.IsWatching))
        {
            ExecutionStateChanged?.Invoke(this, EventArgs.Empty);
        }

        // Las dos entradas que llevan un número del view model (el recuento del VFS y la versión nueva)
        // se reescriben cuando él los mueve: la vista no guarda copia de esos datos.
        if (e.PropertyName is nameof(ControlBarViewModel.VirtualFilesCount)
            or nameof(ControlBarViewModel.PendingUpdateVersionTag))
        {
            RefreshContextualLabels();
        }
    }

    /// <summary>El ciclo (o un modo) cambió de estado: lo consume el renglón de la barra de estado.</summary>
    public event EventHandler? ExecutionStateChanged;

    // ───────────────────────────────────────────────────────────────────────────────
    // Las manos de la barra: los comandos canónicos del view model portable
    // ───────────────────────────────────────────────────────────────────────────────

    private void OnMenuClicked(object sender, RoutedEventArgs e) => _vm?.ToggleMenuCommand.Execute(null);

    private void OnWatchClicked(object sender, RoutedEventArgs e) => Run(_vm?.ToggleWatchModeCommand);

    private void OnRunClicked(object sender, RoutedEventArgs e) => Run(_vm?.ExecuteWorkflowCommand);

    private void OnDebugClicked(object sender, RoutedEventArgs e) => Run(_vm?.DebugWorkflowCommand);

    private void OnStepClicked(object sender, RoutedEventArgs e) => _vm?.StepNextCommand.Execute(null);

    private void OnContinueClicked(object sender, RoutedEventArgs e) => _vm?.ContinueWorkflowCommand.Execute(null);

    private void OnPauseClicked(object sender, RoutedEventArgs e) => _vm?.TogglePauseCommand.Execute(null);

    private void OnStopClicked(object sender, RoutedEventArgs e) => _vm?.StopWorkflowCommand.Execute(null);

    private void OnUndoClicked(object sender, RoutedEventArgs e) => _vm?.UndoCommand.Execute(null);

    private void OnRedoClicked(object sender, RoutedEventArgs e) => _vm?.RedoCommand.Execute(null);

    private void OnRollbackClicked(object sender, RoutedEventArgs e) => Run(_vm?.RollbackLastExecutionCommand);

    private void OnInspectorClicked(object sender, RoutedEventArgs e) => _vm?.ToggleInspectorCommand.Execute(null);

    private void OnSettingsClicked(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, e);

    // ── Las dos entradas con ventana de la isla 1: los comandos canónicos del núcleo ──

    /// <summary>
    /// El explorador virtual de la barra: ejecuta la orden canónica del núcleo, que pide
    /// <c>DialogKeys.VirtualFileSystemExplorer</c> con el almacén de la última ejecución. El botón sólo está
    /// visible cuando la hay (<c>HasVirtualFiles</c>), así que el camino sin datos de la versión anterior —su aviso
    /// informativo— no se alcanza desde aquí.
    /// </summary>
    private void OnVfsClicked(object sender, RoutedEventArgs e) =>
        Execute(_vm?.OpenVirtualFileSystemExplorerCommand);

    /// <summary>
    /// El distintivo de actualización: ejecuta la orden canónica, que pide <c>DialogKeys.UpdateDialog</c> con
    /// el view model del aviso. Sólo se ve cuando el arranque dejó una novedad pendiente.
    /// </summary>
    private void OnUpdateBadgeClicked(object sender, RoutedEventArgs e) =>
        Execute(_vm?.OpenUpdateDialogCommand);

    private static void Execute(System.Windows.Input.ICommand? command)
    {
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Los atajos del menú principal: la tabla RoutedShortcuts es la que enruta
    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enruta una tecla de la ventana a la orden que la versión anterior le liga en sus <c>KeyBinding</c>
    /// (hito 258). Devuelve <c>true</c> si la tecla era de este menú.
    ///
    /// <para>Se llama DESPUÉS del resolver del lienzo y sólo con la tecla sin consumir: el reparto del
    /// teclado de este host es el de la versión anterior —un control que maneja su tecla manda en la suya, el
    /// lienzo resuelve las suyas, y lo que queda llega aquí—.</para>
    ///
    /// <para><b>La tabla manda</b>: el bucle recorre <see cref="RoutedShortcuts"/> y despacha por su orden,
    /// así que una fila de menos es una tecla muda y no una lista paralela en un <c>switch</c>. El rastro
    /// (<c>FILEFLOW_CANVAS_TRACE=1</c>) deja el gesto, la orden y su <c>CanExecute</c> para el observador
    /// externo, que es la única prueba posible de un atajo cuyo efecto no se ve (F5 fuera de una ejecución
    /// no cambia nada, igual que en la versión anterior).</para>
    /// </summary>
    internal bool RouteShortcut(Windows.System.VirtualKey key, bool control, bool shift)
    {
        foreach (var (gesture, tableKey, tableControl, tableShift, order, _) in RoutedShortcuts)
        {
            if (!string.Equals(tableKey, key.ToString(), StringComparison.Ordinal)
                || tableControl != control
                || tableShift != shift)
            {
                continue;
            }

            CanvasFocusTrace.Write($"menu atajo={gesture} orden={order}");
            return Execute(order);
        }

        return false;
    }

    /// <summary>Ejecuta la orden de una fila de <see cref="RoutedShortcuts"/> (o la declara despachada).</summary>
    private bool Execute(string order)
    {
        switch (order)
        {
            case "ContinueWorkflowCommand":
                Run(_vm?.ContinueWorkflowCommand);
                return true;

            case "StepNextCommand":
                Run(_vm?.StepNextCommand);
                return true;

            case "StopWorkflowCommand":
                Run(_vm?.StopWorkflowCommand);
                return true;

            // Nuevo Flujo lo pregunta y lo hace el COMANDO del núcleo —su confirmación va por el contrato
            // ASÍNCRONO desde el hito 265, así que la ventana ya no pregunta por su cuenta—, pero la orden
            // se le pide a la ventana: es ELLA la que refresca el renglón del ciclo que lee el canal externo,
            // y este control no es dueño de esa línea. Una orden, un camino: el mismo por el que entra el
            // botón del cajón.
            case "NewWorkflowCommand":
                NewWorkflowRequested?.Invoke(this, new RoutedEventArgs());
                return true;

            // Cargar y Guardar sí siguen pidiéndose a la ventana: su mitad de DIÁLOGO es el selector de
            // fichero, que sigue siendo SÍNCRONO en el contrato y desde el hilo de UI devuelve nulo.
            case "LoadWorkflowCommand":
                LoadWorkflowRequested?.Invoke(this, new RoutedEventArgs());
                return true;

            case "SaveWorkflowCommand":
                SaveWorkflowRequested?.Invoke(this, new RoutedEventArgs());
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Ejecuta un comando del view model con su <c>CanExecute</c> respetado: exactamente lo que hace un
    /// <c>Button</c> de la versión anterior atado por <c>Command</c>. Los asíncronos del toolkit arrancan su tarea
    /// al ejecutarse; una excepción dentro de ellos queda escrita por el manejador de <c>UnhandledException</c>
    /// del host en vez de morir en silencio.
    /// </summary>
    private static void Run(System.Windows.Input.ICommand? command)
    {
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    /// <summary>
    /// La barra y su estado, para la sonda en runtime: cada entrada con el AutomationId con que el canal
    /// externo la encuentra y si está visible y habilitada AHORA. Es la lectura del estado por contexto.
    /// </summary>
    internal List<(string Id, string Name, bool Visible, bool Enabled)> EntryCensus()
    {
        var entries = new List<(string, string, bool, bool)>
        {
            Entry("ControlBarMenuButton", MenuButton),
            Entry("ControlBarDryRunToggle", DryRunToggle),
            Entry("ControlBarWatchButton", WatchButton),
            Entry("ControlBarRunButton", RunButton),
            Entry("ControlBarDebugButton", DebugButton),
            Entry("ControlBarStepButton", StepButton),
            Entry("ControlBarContinueButton", ContinueButton),
            Entry("ControlBarPauseButton", PauseButton),
            Entry("ControlBarStopButton", StopButton),
            Entry("ControlBarUndoButton", UndoButton),
            Entry("ControlBarRedoButton", RedoButton),
            Entry("ControlBarRollbackButton", RollbackButton),
            Entry("ControlBarInspectorButton", InspectorButton),
            Entry("ControlBarVfsButton", VfsButton),
            Entry("ControlBarUpdateBadge", UpdateBadge),
            Entry("SettingsButton", SettingsButton),
        };
        return entries;
    }

    private static (string, string, bool, bool) Entry(string id, Control control) =>
        (id, control.Name, control.Visibility == Visibility.Visible, control.IsEnabled);

    /// <summary>El rótulo del chip de VFS: lleva el recuento que mueve el view model.</summary>
    internal string VfsChipText => VfsLabel.Text;

    /// <summary>El rótulo del distintivo de actualización: lleva la versión nueva que anunció el arranque.</summary>
    internal string UpdateBadgeText => UpdateLabel.Text;

    /// <summary>La entrada con ese AutomationId (la que pulsa la sonda y la que busca el canal externo).</summary>
    internal Control? EntryById(string id)
    {
        Control[] controls =
        [
            MenuButton, DryRunToggle, WatchButton, RunButton, DebugButton, StepButton, ContinueButton,
            PauseButton, StopButton, UndoButton, RedoButton, RollbackButton, InspectorButton, VfsButton,
            UpdateBadge, SettingsButton,
        ];
        string[] ids =
        [
            "ControlBarMenuButton", "ControlBarDryRunToggle", "ControlBarWatchButton", "ControlBarRunButton",
            "ControlBarDebugButton", "ControlBarStepButton", "ControlBarContinueButton", "ControlBarPauseButton",
            "ControlBarStopButton", "ControlBarUndoButton", "ControlBarRedoButton", "ControlBarRollbackButton",
            "ControlBarInspectorButton", "ControlBarVfsButton", "ControlBarUpdateBadge", "SettingsButton",
        ];

        for (int i = 0; i < ids.Length; i++)
        {
            if (string.Equals(ids[i], id, StringComparison.Ordinal))
            {
                return controls[i];
            }
        }

        return null;
    }

    /// <summary>
    /// PULSA una entrada como la pulsa el usuario: por el peer de automatizacion del propio control
    /// (Invoke de un boton, Toggle de una casilla), que es el MISMO canal por el que llega un lector de
    /// pantalla o el driver externo. Devuelve falso si el control no sostiene el patron.
    /// </summary>
    internal static bool Press(Control control)
    {
        var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer
            .CreatePeerForElement(control);

        if (peer is Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer button)
        {
            button.Invoke();
            return true;
        }

        if (peer is Microsoft.UI.Xaml.Automation.Peers.ToggleButtonAutomationPeer toggle)
        {
            toggle.Toggle();
            return true;
        }

        return false;
    }

    /// <summary>El censo en una linea, para el informe de la sonda (entrada: visible/habilitada).</summary>
    internal string CensusLine()
    {
        var parts = new List<string>();
        foreach (var entry in EntryCensus())
        {
            parts.Add(entry.Id + (entry.Visible ? "=on" : "=off") + (entry.Enabled ? "/enabled" : "/disabled"));
        }

        return string.Join(" ", parts);
    }
}
