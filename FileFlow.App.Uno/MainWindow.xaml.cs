using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using FileFlow.App.Services;
using FileFlow.App.Uno.Controls;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno;

/// <summary>
/// Ventana principal del host Uno. Fase 3.1: el lienzo del editor monta el <see cref="EditorViewModel"/>
/// del núcleo portable y carga el flujo de ejemplo del banco si está disponible en disco — nodos y cables
/// a sus posiciones, pintados con la geometría compartida. La barra inferior conserva la prueba de vida
/// del núcleo de la rebanada 1.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Quién tiene el foco según el gestor, en palabras (tipo, nombre y ancestros).</summary>
    private static string ReadFocused(UIElement root) => CanvasFocusTrace.Describe(
        Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root.XamlRoot), ancestors: 6);

    /// <summary>
    /// El enrutador del teclado del editor (hito 252): las teclas no consumidas por nadie van al lienzo.
    /// El orden importa: primero el respeto por lo ya manejado, después el resolver único del lienzo.
    /// </summary>
    private void OnRootKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Handled || Canvas is null)
        {
            return;
        }

        bool consumed = Canvas.TryHandleShortcutKey(e.Key, e.OriginalSource);

        // Lo que el lienzo no reclama llega al MENÚ PRINCIPAL (hito 258): las seis teclas que el escritorio
        // liga a las órdenes del ciclo y del flujo en los KeyBinding de su ventana. Aquí la tabla del
        // ControlBar es la que enruta, y el estado de los modificadores se lee del teclado de verdad.
        if (!consumed && Bar.RouteShortcut(e.Key, IsDown(Windows.System.VirtualKey.Control),
                IsDown(Windows.System.VirtualKey.Shift)))
        {
            consumed = true;
        }

        if (consumed)
        {
            e.Handled = true;
        }

        // El rastro (hito 252): con FILEFLOW_CANVAS_TRACE=1 esta línea es la prueba de que el atajo se
        // resolvió SIN que el lienzo fuera dueño del foco, y de quién lo tenía cuando llegó.
        CanvasFocusTrace.Write($"enrutado tecla={e.Key} consumido={consumed} "
                             + $"enfocado={CanvasFocusTrace.Describe(
                                 Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Content.XamlRoot), 3)}");
    }

    /// <summary>¿Está esa tecla pulsada ahora mismo? (los modificadores de los atajos del menú).</summary>
    private static bool IsDown(Windows.System.VirtualKey key) =>
        Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    /// <summary>El cuadro de mando del núcleo (sus órdenes las cumplen los manejadores de esta ventana).</summary>
    private ControlBarViewModel? _controlBar;

    /// <summary>
    /// El ancho que la FICHA tenía antes de plegarse, para devolvérselo al volver (hito 270).
    ///
    /// <para>Es la mitad que un redimensionado a medias no tiene: el panel se pliega a ancho CERO —la columna
    /// se queda sin sitio, como en el escritorio—, así que sin recordar lo que medía el usuario, abrirlo otra
    /// vez lo devolvería a un ancho por defecto y el sitio que acababa de darle a la ficha se perdería en cada
    /// visita al botón del inspector.</para>
    /// </summary>
    private double _inspectorWidthBeforeCollapse = 300;

    /// <summary>
    /// Ata las dos asas del marco a sus columnas, con el reparto que el escritorio declara (cajón 180–480,
    /// ficha 220–750) y el mínimo del LIENZO como segunda cota. Se llama en el arranque, antes de que la
    /// primera orden de layout necesite el ancho.
    /// </summary>
    private void AttachPanelSplitters()
    {
        ToolboxSplitter.Attach(ToolboxColumn, min: 180, max: 480, widensToTheRight: true, canvas: CanvasColumn);
        InspectorSplitter.Attach(InspectorColumn, min: 220, max: 750, widensToTheRight: false, canvas: CanvasColumn);
        ApplySplitterNames();
    }

    /// <summary>Los rótulos de las asas con el idioma vigente (los lee el árbol de accesibilidad).</summary>
    private void ApplySplitterNames()
    {
        AutomationProperties.SetName(ToolboxSplitter, LocalizationManager.Instance.GetString(
            "Uno_SplitterToolbox", "Redimensionar el cajón de nodos"));
        AutomationProperties.SetName(InspectorSplitter, LocalizationManager.Instance.GetString(
            "Uno_SplitterInspector", "Redimensionar la ficha del nodo"));
    }

    // ── La superficie que mide la sonda del MARCO (hito 272) ──

    /// <summary>
    /// Las tres zonas de la rejilla raíz —barra, editor y franja de estado— y las dos asas con sus columnas.
    /// Existen para que el sondeo pueda medir la GEOMETRÍA del marco: la regresión del hito 270 —el
    /// <c>Workspace</c> sin su <c>Grid.Row</c>, pintado encima de la barra— pasó con 85 <c>[OK]</c> porque
    /// ninguna sonda miraba dónde cae cada zona, y la barra sólo se pulsaba por método, no por puntero.
    /// </summary>
    internal FrameworkElement FrameBar => Bar;
    internal FrameworkElement FrameWorkspace => Workspace;
    internal FrameworkElement FrameStatus => StatusBarHost;
    internal PanelSplitter FrameToolboxSplitter => ToolboxSplitter;
    internal ColumnDefinition FrameToolboxColumn => ToolboxColumn;
    internal ColumnDefinition FrameCanvasColumn => CanvasColumn;
    internal ColumnDefinition FrameInspectorColumn => InspectorColumn;

    /// <summary>
    /// Aplica la visibilidad del panel al marco entero: la ficha plegada se lleva su columna (a ancho cero) y su
    /// asa, y al abrirse vuelve al ancho que el usuario le había dado —no al de fábrica—.
    /// </summary>
    internal void ApplyInspectorVisibility(bool isOpen)
    {
        if (!isOpen && InspectorColumn.ActualWidth > 0)
        {
            _inspectorWidthBeforeCollapse = InspectorColumn.ActualWidth;
        }
        else if (!isOpen && InspectorColumn.Width.IsAbsolute && InspectorColumn.Width.Value > 0)
        {
            _inspectorWidthBeforeCollapse = InspectorColumn.Width.Value;
        }

        Inspector.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        InspectorSplitter.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;

        if (isOpen)
        {
            InspectorColumn.MinWidth = 220;
            InspectorColumn.MaxWidth = 750;
            double targetWidth = Math.Clamp(_inspectorWidthBeforeCollapse, 220, 750);
            InspectorColumn.Width = new GridLength(targetWidth, GridUnitType.Pixel);
        }
        else
        {
            InspectorColumn.MinWidth = 0;
            InspectorColumn.Width = new GridLength(0, GridUnitType.Pixel);
        }
    }

    /// <summary>
    /// El catálogo de diálogos del host: por él se sirven las superficies que las órdenes del núcleo piden por
    /// clave (<c>DialogKeys</c>) —la ventana de «Acerca de» y el DISEÑADOR DE DATASETS, que declara el nodo de
    /// datos sintéticos—.
    /// </summary>
    private FileFlow.Sdk.Services.IWindowService? _windowService;

    /// <summary>
    /// El aviso de actualización que la comprobación de arranque encontró (hito 259): lo llama la aplicación
    /// cuando su comprobación devuelve una novedad no ignorada, y <b>es el mismo camino del escritorio</b>
    /// (<c>ControlBar.SetPendingUpdate</c>): el distintivo de la barra aparece con la versión nueva y su
    /// comando abre el aviso. Sin esto, el servicio de ventanas serviría un aviso que nadie pide nunca.
    /// </summary>
    internal void ApplyPendingUpdate(FileFlow.Sdk.Services.AppUpdateInfo info) =>
        _controlBar?.SetPendingUpdate(info);

    public MainWindow()
    {
        InitializeComponent();

        // El foco de TODA la ventana al rastro del hito 252 (con FILEFLOW_CANVAS_TRACE=1): el lienzo deja
        // escrito quién tiene el foco al clicar, pero un robo POSTERIOR —el caso medido con puntero real:
        // un ScrollViewer se lleva el foco ~0,5 s después del clic— sólo se ve escuchando en la raíz. El
        // renglón dice el tipo, el nombre y los ancestros, que es lo que identifica al panel del ladrón.
        if (CanvasFocusTrace.IsEnabled && Content is UIElement root)
        {
            // El evento CLR de la raíz (GotFocus burbujea): WinUI 3 no expone el campo `GotFocusEvent`
            // para AddHandler, y aquí no hace falta que llegue lo ya marcado como manejado.
            //
            // Se registran DOS lecturas (hito 253): el origen del evento —quién RECIBE el foco, con su
            // nombre, que es lo que identifica al ladrón— y una re-lectura un tick después, porque un
            // elemento recién creado puede no estar todavía en el árbol visual cuando el evento ocurre.
            root.GotFocus += (_, args) =>
            {
                CanvasFocusTrace.Write($"foco global -> src={CanvasFocusTrace.Describe(args.OriginalSource, 6)}"
                                     + $" | gestor={ReadFocused(root)}");
                root.DispatcherQueue.TryEnqueue(() =>
                    CanvasFocusTrace.Write($"  foco +tick | src={CanvasFocusTrace.Describe(args.OriginalSource, 6)}"
                                         + $" | gestor={ReadFocused(root)}"));
            };
        }

        // ─── El teclado del editor NO depende del foco (hito 252) ───
        //
        // El lienzo resuelve los atajos en TryHandleShortcutKey, y aquí se le enrutan las teclas que nadie
        // consumió —el burbujeo que el escritorio ya usaba en su vista de editor—. Hace falta porque el foco
        // de este host no es propiedad estable del lienzo: el rastro con puntero real midió que el clic se lo
        // entrega y ~0,5 s después un panel que reacciona a la selección se lo lleva (un ScrollViewer), y con
        // el foco se iban Ctrl+Z, Ctrl+Y, Supr y F2. Se instala en la RAÍZ y sólo si el evento no viene ya
        // consumido: un control que maneja su tecla (un botón con la barra espaciadora, un ListView) sigue
        // mandando en la suya. El resolver del lienzo se salta por su cuenta lo que es de un cuadro de texto.
        if (Content is UIElement keyboardRoot)
        {
            keyboardRoot.KeyDown += OnRootKeyDown;
        }

        try
        {
            var services = App.Services;

            var loader = services.GetRequiredService<PluginLoader>();
            int nodes = loader.DiscoveredNodesCount;

            // Las dos ASAS del marco (hito 270): se atan antes de montar nada que mida el lienzo —el ancho de
            // la columna del cajón es el desplazamiento con el que el lienzo calcula su área de clic—, y con
            // el reparto del escritorio (cajón 180–480, ficha 220–750) más el mínimo del lienzo.
            AttachPanelSplitters();

            var mainVm = services.GetRequiredService<MainViewModel>();            Canvas.Editor = mainVm.Editor;
            TryLoadSampleFlow(mainVm.Editor);

            // Rebanada 4: los dos paneles del editor, consumiendo los VM del núcleo (los mismos que
            // el escritorio): el cajón añade nodos al MISMO editor que pinta el lienzo, el inspector
            // sigue la selección que el lienzo escribe.
            Toolbox.Vm = mainVm.Toolbox;
            Toolbox.Editor = mainVm.Editor;
            Inspector.Vm = mainVm.NodeInspector;

            // Hito 255 — la superficie de AJUSTES del host: la vista es del host, el view model es el
            // MISMO WorkflowSettingsViewModel portable que alimenta la ventana de ajustes del escritorio
            // (almacenamiento y rutas, apariencia e idioma, rendimiento y herramientas externas).
            Settings.Vm = SettingsPanel.CreateViewModel(services);

            // Hito 257 — el MENÚ PRINCIPAL del host: la barra de control del escritorio y su cajón, sobre
            // el MISMO ControlBarViewModel portable que el contenedor del núcleo ya resolvía (el del botón
            // Ejecutar del hito 243). La barra y el cajón comparten la instancia: el botón «Menú» conmuta
            // el estado (IsMenuOpen) y el cajón lo sigue, sin copia de la vista.
            Bar.Vm = mainVm.ControlBar;
            Drawer.Vm = mainVm.ControlBar;
            Drawer.VersionText = mainVm.AppVersionDisplay;
            _controlBar = mainVm.ControlBar;
            _windowService = services.GetRequiredService<FileFlow.Sdk.Services.IWindowService>();

            // Las dos entradas a los ajustes (la de la barra y la del cajón) abren la MISMA superficie.
            Bar.SettingsRequested += OnOpenSettingsClicked;
            Drawer.SettingsRequested += OnOpenSettingsClicked;

            // Hito 258 — las órdenes de FLUJO: las pide el cajón por su entrada y la barra por su atajo
            // (Ctrl+N / Ctrl+O / Ctrl+S), y las cumple ESTA ventana con los diálogos asíncronos del host
            // más los métodos del view model portable que ya no dependen de un diálogo.
            Bar.NewWorkflowRequested += OnNewWorkflowRequested;
            Drawer.NewWorkflowRequested += OnNewWorkflowRequested;
            Bar.LoadWorkflowRequested += OnLoadWorkflowRequested;
            Drawer.LoadWorkflowRequested += OnLoadWorkflowRequested;
            Bar.SaveWorkflowRequested += OnSaveWorkflowRequested;
            Drawer.SaveWorkflowRequested += OnSaveWorkflowRequested;

            // Hito 261 — el DISEÑADOR DE DATASETS: su superficie la declara el NODO de datos sintéticos al SDK
            // (qué diálogo quiere y qué contiene), y la sirve el catálogo de diálogos de ESTE host con su
            // propia vista sobre el view model portable del plugin. La lógica del diseñador no se copia.
            Drawer.DataSetDesignerRequested += OnOpenDataSetDesigner;

            // El INSPECTOR: el host arranca con el panel abierto —es una columna del marco, como hasta
            // ahora— y su entrada de la barra y del cajón lo conmuta desde ahí. El view model del núcleo
            // nace cerrado porque en el escritorio el panel es colapsable; el estado inicial es decisión
            // del marco del host, y la conmutación sí es la del núcleo (ToggleInspectorCommand).
            mainVm.NodeInspector.IsOpen = true;
            ApplyInspectorVisibility(true);
            mainVm.NodeInspector.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NodeInspectorViewModel.IsOpen))
                {
                    ApplyInspectorVisibility(mainVm.NodeInspector.IsOpen);
                }
            };

            // El renglón de la barra de estado sigue el ciclo desde la propia barra (sus PropertyChanged
            // de IsRunning/IsDebugging/IsDryRun/IsWatching), que es el mismo view model que mueve los botones.
            Bar.ExecutionStateChanged += (_, _) => RefreshExecutionStatus(mainVm.ControlBar);

            var loc = LocalizationManager.Instance;
            var core = loc.GetFormattedString(
                "Uno_HostCoreReady",
                "Núcleo portable listo: {0} nodos, MainViewModel resuelto.",
                nodes);

            Console.WriteLine("[UnoHost] nodos descubiertos: " + nodes);

            int catalogue = mainVm.Toolbox.CategoryGroups.SelectMany(g => g.Items).Count();
            engineStatus.Text = core
                + Environment.NewLine
                + loc.GetFormattedString(
                "Uno_HostViewModel",
                "Lienzo montado: {0} nodos en el grafo.",
                mainVm.Editor.Nodes.Count)
                + Environment.NewLine
                + loc.GetFormattedString(
                "Uno_HostPanels",
                "Paneles montados: cajón con {0} tipos de nodo, inspector conectado a la selección.",
                catalogue);

            Title = "FileFlow Studio — Uno Platform";

            RefreshFrameLocalization();
            BuildStatusBar(mainVm);

            // Localización en caliente (fase 3.5): los textos del marco se rescriben al cambiar el idioma.
            // El lienzo ya reconstruye los suyos al reasignar Editor (el selector de idioma vive en los
            // ajustes del escritorio; cuando el núcleo cambie la cultura, LanguageChanged notifica).
            LocalizationManager.Instance.LanguageChanged += (_, _) => RefreshLocalizedTexts(
                nodes, mainVm.Editor.Nodes.Count,
                mainVm.Toolbox.CategoryGroups.SelectMany(g => g.Items).Count());
        }
        catch (Exception ex)
        {
            engineStatus.Text = $"Fallo al arrancar el núcleo portable: {ex.Message}";
        }
    }

    /// <summary>
    /// El botón Ejecutar (hito 243): el comando canónico del ControlBar del núcleo — el MISMO que
    /// el botón del escritorio. El guion UIA externo lo invoca por su AutomationId para el ciclo
    /// completo (ejecutar → snapshot nuevo → diff recalculado). La barra de estado expone el
    /// estado de ejecución y los contadores de snapshots/diff del nodo fuente: el legible del
    /// ciclo para un observador sin acceso al árbol de VMs.
    /// </summary>
    private void BuildStatusBar(MainViewModel mainVm)
    {
        var loc = LocalizationManager.Instance;
        var controlBar = mainVm.ControlBar;

        var runButton = new Button
        {
            Padding = new Thickness(12, 4, 12, 4),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = loc.GetString("Uno_RunExecute", "Ejecutar")
        };
        AutomationProperties.SetAutomationId(runButton, "ExecuteButton");
        int clicksReceived = 0;
        runButton.Click += async (_, _) =>
        {
            // La marca del clic (latch del canal): distingue «el botón no recibió el gesto» de
            // «el comando corrió y fue rechazado por el diagnóstico» — ambas acaban en idle.
            clicksReceived++;
            engineStatus.Text = StatusLineWriter.Padded($"run: click #{clicksReceived} recibido");
            try
            {
                if (controlBar.ExecuteWorkflowCommand.CanExecute(null))
                {
                    await controlBar.ExecuteWorkflowCommand.ExecuteAsync(null);
                }
                else
                {
                    engineStatus.Text = StatusLineWriter.Padded("run: RECHAZADO por CanExecute (IsRunning="
                        + controlBar.IsRunning + ")");
                }
            }
            catch (Exception ex)
            {
                // El veredicto del ciclo no puede morir en silencio: la excepción del comando
                // queda en la línea del canal para el observador externo.
                engineStatus.Text = StatusLineWriter.Padded("run: EXCEPCION " + ex.GetType().Name
                    + ": " + ex.Message);
            }
        };

        // El renglón sigue el ciclo por el evento de la BARRA (que escucha los mismos PropertyChanged del
        // view model), y no por una segunda suscripción a este: una sola fuente para la misma línea.
        StatusBarHost.Children.Add(runButton);
        RefreshExecutionStatus(controlBar);
    }

    /// <summary>La línea legible del ciclo: estado del ControlBar y contadores del nodo fuente.</summary>
    private void RefreshExecutionStatus(ControlBarViewModel controlBar)
    {
        var source = controlBar.Editor.Nodes.FirstOrDefault(n => n.Title.Contains("Source", StringComparison.OrdinalIgnoreCase))
                     ?? controlBar.Editor.Nodes.FirstOrDefault();
        var inspector = controlBar.NodeInspector;
        string counters = source is null
            ? "sin grafo"
            : $"node={source.Title} snapshots={source.InputSnapshots.Count + source.OutputSnapshots.Count} diff={inspector.MetadataDiffs.Count}";
        string line = $"run: {(controlBar.IsRunning ? (controlBar.IsDryRun ? "dry-run" : "running") : "idle")} | "
            + counters;

        // El renglón padded es el CANAL del ciclo (hito 243): la UI lo pinta y un observador
        // externo (el guion UIA del ciclo completo) lo lee atómicamente por el writer.
        engineStatus.Text = StatusLineWriter.Padded(line);
    }

    /// <summary>El renglón del ciclo para lectores externos (la sonda de la superficie UIA).</summary>
    public static string ExecutionStatusLine => StatusLineWriter.Current;

    /// <summary>Los textos localizados del marco del host, re-escritura del idioma vigente.</summary>
    private void RefreshLocalizedTexts(int nodes, int canvasNodes, int catalogue)
    {
        var loc = LocalizationManager.Instance;
        RefreshFrameLocalization();
        engineStatus.Text = loc.GetFormattedString(
            "Uno_HostCoreReady",
            "Núcleo portable listo: {0} nodos, MainViewModel resuelto.",
            nodes)
            + Environment.NewLine
            + loc.GetFormattedString(
            "Uno_HostViewModel",
            "Lienzo montado: {0} nodos en el grafo.",
            canvasNodes)
            + Environment.NewLine
            + loc.GetFormattedString(
            "Uno_HostPanels",
            "Paneles montados: cajón con {0} tipos de nodo, inspector conectado a la selección.",
            catalogue);
    }

    /// <summary>
    /// El punto de entrada a los ajustes: despliega la superficie del host. Es un botón de la cabecera
    /// (no un ítem de un cajón) porque el cajón del escritorio —con sus órdenes de flujo y de ayuda— depende
    /// de ventanas que este host todavía no tiene: lo que no llega queda declarado en el plan de la rebanada 5.
    /// </summary>
    private void OnOpenSettingsClicked(object sender, RoutedEventArgs e) => Settings.Open();

    /// <summary>
    /// El DISEÑADOR DE DATASETS (hito 261): la superficie la declara el NODO de datos sintéticos por el contrato
    /// <c>INodeDialogSurfaceProvider</c> del SDK, así que esta mano no reimplementa nada —pregunta al view model
    /// portable qué diálogo quiere y qué contiene, y se lo entrega al catálogo de diálogos del host, que pinta
    /// su propia vista sobre ese MISMO view model—.
    ///
    /// <para>Cuando el nodo no está en el catálogo (el plugin del sistema de archivos no cargó), no se queda en
    /// silencio: la orden se declina con su motivo, que es la misma regla que el resto del host.</para>
    /// </summary>
    private void OnOpenDataSetDesigner(object? sender, RoutedEventArgs e)
    {
        var bar = _controlBar;
        if (bar is null)
        {
            return;
        }

        var surface = bar.GetDataSetDesignerSurface();
        if (surface is null)
        {
            Platform.UnoWindowService.Decline("DataSetDesigner",
                "ningún nodo del catálogo declara esa superficie (el plugin de sistema de archivos no está cargado)");
            CanvasFocusTrace.Write("menu datos=declinado sin-superficie");
            return;
        }

        // Los diálogos de ESTE host viajan en el contexto: el contenido de la superficie lo construye el nodo
        // —un plugin, que no puede resolverlos— y sin ellos su borrado de datasets caería al doble nulo, que a
        // una confirmación contesta «sí» sin preguntar. Es la misma entrega que hace el núcleo en las puertas
        // de la fila y de la tarjeta.
        object? payload = surface.CreateDialogPayload(new NodeCustomActionContext(
            Dialogs: App.Services.GetRequiredService<IDialogService>()));
        CanvasFocusTrace.Write("menu datos=abierto clave=" + surface.DialogKey);
        _windowService.ShowWindow(surface.DialogKey, payload);
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Las tres órdenes de FLUJO (hito 258): diálogo asíncrono del host + ViewModel portable
    // ───────────────────────────────────────────────────────────────────────────────
    //
    // El comando del núcleo pide un diálogo SÍNCRONO (confirmación o fichero) que desde el hilo de UI
    // devuelve falso/nulo en este host, así que el botón quedaría mudo. Estas tres manos eligen con las
    // APIs asíncronas del host y llaman después al MISMO view model portable que el comando usa: la
    // lógica del producto no se copia, sólo se cambia quién abre el diálogo.

    private async void OnNewWorkflowRequested(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_controlBar is null)
            {
                return;
            }

            // La pregunta la hace el COMANDO del núcleo por el contrato ASÍNCRONO (hito 265). Hasta entonces
            // la ventana confirmaba por su cuenta —el comando preguntaba por el contrato síncrono, que desde
            // el hilo de UI devuelve falso—: era el host haciendo la pregunta del producto, y se quitó.
            int before = _controlBar.Editor.Nodes.Count;
            await _controlBar.NewWorkflowCommand.ExecuteAsync(null);
            CanvasFocusTrace.Write("menu flujo=nuevo nodos=" + before + " -> " + _controlBar.Editor.Nodes.Count);
            RefreshExecutionStatus(_controlBar);
        }
        catch (Exception ex)
        {
            engineStatus.Text = StatusLineWriter.Padded("flujo nuevo: EXCEPCION " + ex.GetType().Name
                + ": " + ex.Message);
        }
    }

    private async void OnLoadWorkflowRequested(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_controlBar is null)
            {
                return;
            }

            var loc = LocalizationManager.Instance;
            var picker = App.Services.GetRequiredService<IFileDialogService>();
            string? filePath = await picker.ShowOpenFileDialogAsync(
                loc.GetString("LoadWorkflowBtn", "Cargar Flujo"),
                "Flujo FileFlow (*.json)|*.json|Todos los archivos (*.*)|*.*",
                ".json");

            if (string.IsNullOrEmpty(filePath))
            {
                CanvasFocusTrace.Write("menu flujo=cargar sin-ruta");
                return;
            }

            await _controlBar.LoadWorkflowFromFileAsync(filePath);
            CanvasFocusTrace.Write("menu flujo=cargado nodos=" + _controlBar.Editor.Nodes.Count);
            RefreshExecutionStatus(_controlBar);
        }
        catch (Exception ex)
        {
            engineStatus.Text = StatusLineWriter.Padded("flujo cargado: EXCEPCION " + ex.GetType().Name
                + ": " + ex.Message);
        }
    }

    private async void OnSaveWorkflowRequested(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_controlBar is null)
            {
                return;
            }

            var loc = LocalizationManager.Instance;
            var picker = App.Services.GetRequiredService<IFileDialogService>();
            string? filePath = await picker.ShowSaveFileDialogAsync(
                loc.GetString("SaveWorkflowBtn", "Guardar Flujo"),
                "Flujo FileFlow (*.json)|*.json|Todos los archivos (*.*)|*.*",
                ".json",
                "flujo.json");

            if (string.IsNullOrEmpty(filePath))
            {
                CanvasFocusTrace.Write("menu flujo=guardar sin-ruta");
                return;
            }

            await _controlBar.SaveWorkflowToFileAsync(filePath);
            CanvasFocusTrace.Write("menu flujo=guardado ruta=" + Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            engineStatus.Text = StatusLineWriter.Padded("flujo guardado: EXCEPCION " + ex.GetType().Name
                + ": " + ex.Message);
        }
    }

    /// <summary>
    /// Los textos del marco con el idioma vigente: la barra de control y su cajón (cada uno aplica sus
    /// claves del diccionario del host) y el renglón del núcleo. El cambio de idioma en caliente pasa por
    /// aquí, que es <see cref="RefreshLocalizedTexts"/>.
    /// </summary>
    private void RefreshFrameLocalization()
    {
        Bar.RefreshLocalization();
        Drawer.RefreshLocalization();
        ApplySplitterNames();
    }

    /// <summary>El error del último intento de carga del ejemplo (vacío si no hubo): visible para el sondeo.</summary>
    public string? SampleLoadError { get; private set; }

    /// <summary>
    /// Carga el primer flujo de ejemplo que encuentre en las carpetas canónicas del producto, para que el
    /// lienzo muestre un grafo real en el arranque. Sin ejemplos en disco, el lienzo arranca vacío; un
    /// error de carga no impide el arranque pero SE REPORTA (consola y <see cref="SampleLoadError"/>).
    /// </summary>
    private void TryLoadSampleFlow(EditorViewModel editor)
    {
        // Del bin del host al repositorio: caminar hacia arriba hasta un directorio que contenga
        // docs\examples (el marcador del banco). En instalación publicada, la copia local Examples/ manda.
        string? repoRoot = AppContext.BaseDirectory;
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot, "docs", "examples")))
        {
            repoRoot = Path.GetDirectoryName(repoRoot.TrimEnd(Path.DirectorySeparatorChar));
        }

        // Sin rastro paso a paso (BaseDirectory, candidatos, primer json): el RESULTADO informa y el
        // error, si lo hay, se reporta. La barra de estado de la ventana ya dice cuántos nodos quedaron.
        foreach (var candidate in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "Examples"),
                     repoRoot is null ? "" : Path.Combine(repoRoot, "docs", "examples")
                 })
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            var first = Directory.EnumerateFiles(candidate, "*.json", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (first is not null)
            {
                try
                {
                    var json = File.ReadAllText(first);
                    var graph = FileFlow.Core.Engine.WorkflowGraph.FromJson(json);
                    editor.LoadFromGraphModel(graph);
                    Console.WriteLine("[UnoHost] ejemplo cargado: " + graph.Nodes.Count + " nodos ("
                        + Path.GetFileName(first) + ")");
                }
                catch (Exception ex)
                {
                    // Un ejemplo que no se puede leer no impide el arranque, pero el error no se traga:
                    // queda visible en consola y para el sondeo en runtime (--selfcheck).
                    SampleLoadError = ex.GetType().Name + ": " + ex.Message
                        + (ex.InnerException is null ? "" : " | " + ex.InnerException.Message);
                    Console.Error.WriteLine("[TryLoadSampleFlow] " + SampleLoadError);
                }

                return;
            }
        }
    }
}
