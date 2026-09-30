using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El inspector de nodos del host Uno, montado sobre el <see cref="NodeInspectorViewModel"/> del
/// núcleo portable (el mismo que el escritorio): la ficha del nodo seleccionado con su descripción,
/// los parámetros con los MISMOS editores que el escritorio decide por los flags del
/// <see cref="NodeParameterViewModel"/> (toggle, slider, número, desplegable, ruta con explorar,
/// multilínea, texto) y la sección de telemetría del nodo —montada desde el hito 275 en su propio control
/// (<see cref="NodeInspectorTelemetrySection"/>), que sólo LEE las medidas que el motor escribe en él.
///
/// <para><b>Por qué por código y no XAML</b>: el escritorio decide los editores con un Selector de
/// estilos de Avalonia sobre los flags del VM; WinUI no tiene un equivalente directo (los DataTemplate
/// de WinUI no seleccionan por propiedad del ítem), así que la tabla de editores vive aquí como el
/// mismo orden de flags que el escritorio — la guardia de la rebanada compara esa tabla contra el VM.
/// El botón «Probar» del escritorio (prueba aislada con fichero) está ACTIVADO desde el hito 240:
/// ejecuta TestNodeWithCustomFileAsync del núcleo, que consume la variante asíncrona del
/// IFileDialogService — el picker se abre desde el click de UI sin bloquear el hilo de UI.</para>
///
/// <para><b>Este fichero construye la ficha; medirla vive al lado</b>: la superficie de observación —los
/// accesos por ancla, los censos de lo materializado y las sondas de estado que el sondeo en runtime y las
/// guardias de fuente necesitan— está en <c>NodeInspectorPanel.Probes.cs</c>, junto a esta. Nadie que lea el
/// panel para cambiarlo debería tener que apartar el instrumento que lo mide.</para>
/// </summary>
public sealed partial class NodeInspectorPanel : UserControl
{
    // ── Construcción una sola vez; el contenido se rellena por nodo inspeccionado ──
    private readonly TextBlock _titleText;
    private readonly TextBlock _emptyText;
    private readonly TextBlock _descriptionText;
    private readonly TextBlock _paramsHeader;
    private readonly TextBlock _actionsHeader;
    private readonly StackPanel _paramsHost = new() { Spacing = 4 };

    /// <summary>
    /// La pila de las ACCIONES del nodo (el bloque de acciones rápidas del escritorio): un botón por acción
    /// declarada, que ejecuta la misma orden del núcleo que el botón de la tarjeta del lienzo.
    /// </summary>
    private readonly StackPanel _actionsHost = new() { Spacing = 4 };

    /// <summary>
    /// Los controles de las filas de parámetros por su AutomationId. Es la tabla que hace observable
    /// la fila desde fuera: el driver externo (y la sonda en proceso) encuentra la caja de un parámetro
    /// y las acciones de su fila por un ancla estable, sin descifrar el árbol.
    /// </summary>
    private readonly Dictionary<string, Control> _paramControls = new(StringComparer.Ordinal);

    /// <summary>
    /// Los botones de las acciones del nodo por su AutomationId (<c>InspectorAction_&lt;ActionId&gt;</c>),
    /// en su propia tabla: el censo de las filas de parámetro se vacía al reconstruirlas y llevarle estas
    /// anclas las borraría del registro mientras los botones siguen en el árbol.
    /// </summary>
    private readonly Dictionary<string, Control> _actionControls = new(StringComparer.Ordinal);

    /// <summary>
    /// Las suscripciones vivo-parametro → caja de las filas materializadas. Se sueltan en cada reconstrucción
    /// (la fila vieja se va con el nodo): sin soltarlas, el parámetro seguiría escribiendo en cajas muertas y
    /// el árbol visual de la selección anterior no se podría recoger.
    /// </summary>
    private readonly List<(NodeParameterViewModel Param, PropertyChangedEventHandler Handler)> _rowValueSubscriptions = new();
    private readonly StackPanel _snapshotsHost = new() { Spacing = 6 };
    private readonly StackPanel _inputsHost = new() { Spacing = 6 };
    private readonly StackPanel _outputsHost = new() { Spacing = 6 };
    private readonly StackPanel _diffHost = new() { Spacing = 2 };

    /// <summary>
    /// La sección de TELEMETRÍA de la ficha (hito 275): sus filas salen del <c>CurrentStats</c> del nodo
    /// inspeccionado —el agregado que escribe el motor— y de su estado, y las pinta ella. Vive en su propio
    /// archivo porque es una superficie con entidad propia (la pestaña de Telemetría del escritorio), no un
    /// bloque más de este panel: el panel la monta y le dice qué nodo, nada más.
    /// </summary>
    private readonly NodeInspectorTelemetrySection _telemetrySection;

    /// <summary>
    /// El conmutador de secciones de la ficha (hito 273): la tira de rótulos —que PARTE la línea— y el
    /// host de los cinco cuerpos, todos materializados (la conmutación es de visibilidad, no de creación).
    ///
    /// <para><b>Por qué no un <c>Pivot</c></b>: el Pivot reparte sus rótulos en el ancho de la ficha y no
    /// los envuelve. Con la ficha en sus 300 lógicos de fábrica —y sus rótulos en español o inglés— medía
    /// «Salidas» y «Diff» con <b>caja vacía</b> (rectángulo (0,0,0,0), fuera del alcance del ratón) y
    /// «Entradas» recortada a 36 px, así que dos de las cinco secciones no se podían pulsar y no había
    /// scroll, ni rueda, ni chevron de desbordamiento que las alcanzara. El escritorio usa un
    /// <c>TabControl</c>, que ENVUELVE sus cabeceras; aquí se usa el mismo conmutador segmentado que la
    /// superficie de Ajustes del propio host, sobre el <see cref="WrapPanel"/> del hito 272.</para>
    /// </summary>
    private readonly WrapPanel _tabStrip = new() { Spacing = 4 };
    private readonly Grid _paneHost = new();
    private readonly RadioButton[] _tabButtons = new RadioButton[InspectorTabs.Length];
    private readonly UIElement[] _tabPanes = new UIElement[InspectorTabs.Length];
    private int _selectedTab;

    /// <summary>
    /// Las seis secciones de la ficha, en orden: su clave de idioma, su rótulo de fábrica y su ancla de
    /// automatización (las MISMAS que llevaba el Pivot, porque la observación UIA externa y las guardias
    /// las buscan por ahí). Telemetría va al final, como en el escritorio: es la sección que CIERRA la ficha.
    /// </summary>
    private static readonly (string Key, string Fallback, string Aid)[] InspectorTabs =
    {
        ("Uno_InspectorTabParams", "Parámetros", "InspectorTabParams"),
        ("Uno_InspectorTabSnapshots", "Snapshots", "InspectorTabSnapshots"),
        ("Uno_InspectorTabInputs", "Entradas", "InspectorTabInputs"),
        ("Uno_InspectorTabOutputs", "Salidas", "InspectorTabOutputs"),
        ("Uno_InspectorTabDiff", "Diff", "InspectorTabDiff"),
        ("Uno_InspectorTelemetry", "Telemetría", "InspectorTabTelemetry")
    };

    private Grid? _paramsPane;
    private ScrollViewer? _snapshotsPane;
    private ScrollViewer? _inputsPane;
    private ScrollViewer? _outputsPane;
    private ScrollViewer? _diffPane;
    private ScrollViewer? _telemetryPane;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _inputsSub;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _outputsSub;
    private readonly Button _testButton;
    private readonly FrameworkElement _body;
    private readonly Grid _root = new();

    private NodeInspectorViewModel? _vm;
    private NodeViewModel? _inspected;
    private NotifyCollectionChangedEventHandler? _paramsSub;

    public NodeInspectorPanel()
    {
        var loc = LocalizationManager.Instance;

        // Cabecera: título + cerrar (el comando del VM de la rebanada del escritorio).
        var closeButton = new Button
        {
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 11,
            CornerRadius = new CornerRadius(6),
            Background = Brush("CanvasCardBrush"),
            BorderBrush = Brush("CanvasBorderBrush"),
            BorderThickness = new Thickness(1),
            Foreground = Brush("CanvasSecondaryBrush"),
            Content = loc.GetString("Uno_InspectorClose", "Cerrar")
        };
        closeButton.Click += (_, _) => _vm?.ClosePanelCommand.Execute(null);

        // El «Probar» del escritorio (hito 240): el comando canónico del núcleo abre el picker con
        // la variante asíncrona del IFileDialogService — desde el click de UI sin interbloqueo —
        // y ejecuta el nodo con el fichero elegido (estados Running/Completed/PausedOnError,
        // snapshot de entrada, diff de metadatos y diálogos de resultado viven en el núcleo).
        _testButton = new Button
        {
            Padding = new Thickness(12, 4, 12, 4),
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            CornerRadius = new CornerRadius(6),
            Background = Brush("CanvasAccentPrimaryBrush"),
            Foreground = Brush("CanvasOnAccentBrush"),
            BorderThickness = new Thickness(0),
            Content = loc.GetString("Uno_InspectorTest", "Probar")
        };
        AutomationProperties.SetAutomationId(_testButton, "InspectorTestButton");
        _testButton.Click += (_, _) => _vm?.TestNodeWithCustomFileCommand.Execute(null);

        var header = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _titleText = new TextBlock
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush("CanvasTextBrush")
        };
        Grid.SetColumn(_titleText, 0);
        Grid.SetColumn(_testButton, 1);
        Grid.SetColumn(closeButton, 2);
        header.Children.Add(_titleText);
        header.Children.Add(_testButton);
        header.Children.Add(closeButton);

        _emptyText = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("CanvasSecondaryBrush")
        };

        _descriptionText = new TextBlock
        {
            FontSize = 11,
            Opacity = 0.85,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = Brush("CanvasSecondaryBrush")
        };

        _paramsHeader = new TextBlock
        {
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 6),
            Foreground = Brush("CanvasTextBrush")
        };

        // El encabezado del bloque de ACCIONES del nodo (el que la ficha del escritorio pinta sobre su lista
        // de acciones): se colapsa entero cuando el nodo no declara ninguna.
        _actionsHeader = new TextBlock
        {
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 6),
            Foreground = Brush("CanvasTextBrush")
        };

        // La sección de TELEMETRÍA del nodo (hito 275): el panel la MONTA como una sección más. La fuente de las
        // medidas ya existe —la escribe el motor en el CurrentStats del nodo— y la sección sólo la lee; hasta
        // aquí el panel construía sus filas y no las montaba en ninguna parte: se rellenaban para nadie.
        _telemetrySection = new NodeInspectorTelemetrySection();

        // Las tres pestañas del escritorio (hito 242): Parámetros, Snapshots (los snapshots del
        // nodo con su vista) y Diff (el diff de metadatos que el VM del núcleo computa al
        // seleccionar un snapshot). Los AIDs dan anclas a la observación UIA externa.
        // La ficha en el orden del escritorio: descripción, ACCIONES del nodo (la puerta a sus superficies —
        // el gestor de presets, la configuración del VLM, el estudio de scripts...) y, debajo, los editores de
        // sus parámetros. Las acciones vivían sólo en la tarjeta del lienzo: sin este bloque, el usuario que
        // no supiera desplegar la tarjeta no tenía forma de llegar a la superficie del nodo.
        var paramsGrid = new Grid { RowSpacing = 0 };
        paramsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        paramsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        paramsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        paramsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        paramsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_descriptionText, 0);
        Grid.SetRow(_actionsHeader, 1);
        Grid.SetRow(_actionsHost, 2);
        Grid.SetRow(_paramsHeader, 3);
        var paramsScroll = new ScrollViewer
        {
            Name = "InspectorParamsScroll",
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _paramsHost
        };
        Grid.SetRow(paramsScroll, 4);
        paramsGrid.Children.Add(_descriptionText);
        paramsGrid.Children.Add(_actionsHeader);
        paramsGrid.Children.Add(_actionsHost);
        paramsGrid.Children.Add(_paramsHeader);
        paramsGrid.Children.Add(paramsScroll);

        // Los seis cuerpos, todos materializados en el host de paneles: la conmutación es de
        // visibilidad, no de creación (el Pivot creaba y des-realizaba sus envoltorios).
        UIElement[] panes =
        [
            paramsGrid,
            NamedPane("InspectorSnapshotsScroll", _snapshotsHost),
            NamedPane("InspectorInputsScroll", _inputsHost),
            NamedPane("InspectorOutputsScroll", _outputsHost),
            NamedPane("InspectorDiffScroll", _diffHost),
            NamedPane("InspectorTelemetryScroll", _telemetrySection)
        ];

        _paramsPane = paramsGrid;
        _snapshotsPane = panes[1] as ScrollViewer;
        _inputsPane = panes[2] as ScrollViewer;
        _outputsPane = panes[3] as ScrollViewer;
        _diffPane = panes[4] as ScrollViewer;
        _telemetryPane = panes[5] as ScrollViewer;

        for (int i = 0; i < InspectorTabs.Length; i++)
        {
            var (key, fallback, aid) = InspectorTabs[i];
            int index = i;
            var button = new RadioButton
            {
                GroupName = "InspectorSections",
                FontSize = 11,
                Padding = new Thickness(8, 4, 8, 4),
                CornerRadius = new CornerRadius(6),
                Content = loc.GetString(key, fallback),
                IsChecked = i == 0
            };
            UpdateTabButtonStyle(button, i == 0);
            AutomationProperties.SetAutomationId(button, aid);
            button.Click += (_, _) => ShowTab(index);
            _tabButtons[i] = button;
            _tabStrip.Children.Add(button);

            var pane = panes[i];
            pane.Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
            _tabPanes[i] = pane;
            _paneHost.Children.Add(pane);
        }

        var tabContainer = new Border
        {
            Background = Brush("CanvasBgDarkBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(3),
            Margin = new Thickness(0, 2, 0, 10),
            Child = _tabStrip
        };

        var bodyGrid = new Grid();
        bodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bodyGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(tabContainer, 0);
        Grid.SetRow(_paneHost, 1);
        bodyGrid.Children.Add(tabContainer);
        bodyGrid.Children.Add(_paneHost);
        _body = bodyGrid;

        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(header, 0);
        Grid.SetRow(_emptyText, 1);
        Grid.SetRow(_body, 2);
        _root.Children.Add(header);
        _root.Children.Add(_emptyText);
        _root.Children.Add(_body);

        Content = new Border
        {
            Background = Brush("CanvasCardBrush"),
            BorderBrush = Brush("CanvasBorderBrush"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = _root
        };
        Padding = new Thickness(16, 14, 16, 14);

        ApplyLocalization();

        // La ficha arranca en un estado DECIDIDO, no en el de por defecto: sin VM nada está abierto, así que ni el
        // cuerpo, ni el texto de «sin selección», ni el «Probar» se ofrecen. Sin esta llamada, entre la construcción
        // y la llegada del VM el panel enseñaba a la vez los dos estados (cuerpo y vacío) con el botón de la cabecera
        // ya dibujado, que es el estado que el hito 274 corrige.
        UpdateVisibility();
        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>El VM del núcleo que la vista consume (selección, apertura, cierre).</summary>
    public NodeInspectorViewModel? Vm
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
            if (_vm is not null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
                // El diff vive en el VM (la lógica es del núcleo): la pestaña lo sigue en vivo.
                _vm.MetadataDiffs.CollectionChanged += (_, _) => RebuildDiff();
            }

            RefreshNode();
        }
    }

    /// <summary>Claves de localización propias del inspector (el título admite override del host).</summary>
    public void ApplyLocalization(string? titleKey = null)
    {
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var loc = LocalizationManager.Instance;
        _paramsHeader.Text = loc.GetString("Uno_InspectorParams", "Parámetros");
        _actionsHeader.Text = loc.GetString("Uno_InspectorActions", "Acciones");
        _testButton.Content = loc.GetString("Uno_InspectorTest", "Probar");
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            if (_tabButtons[i] is not null)
            {
                _tabButtons[i].Content = loc.GetString(InspectorTabs[i].Key, InspectorTabs[i].Fallback);
            }
        }

        _telemetrySection.ApplyLocalization();
        RefreshHeaderTexts();
    }

    /// <summary>
    /// Conmuta la sección visible de la ficha: el clic de la tira y la sonda entran por aquí. El cuerpo
    /// del diff se repinta al mostrarlo (lo hacía el <c>SelectionChanged</c> del Pivot).
    /// </summary>
    internal void ShowTab(int index)
    {
        if (index < 0 || index >= _tabPanes.Length || _tabPanes[index] is null)
        {
            return;
        }

        _selectedTab = index;
        for (int i = 0; i < _tabPanes.Length; i++)
        {
            _tabPanes[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            if (_tabButtons[i] is not null)
            {
                _tabButtons[i].IsChecked = i == index;
                UpdateTabButtonStyle(_tabButtons[i], i == index);
            }
        }

        if (ReferenceEquals(_tabPanes[index], _diffPane))
        {
            RebuildDiff();
        }
    }

    private void UpdateTabButtonStyle(RadioButton button, bool isSelected)
    {
        if (isSelected)
        {
            button.Background = Brush("CanvasCardBrush");
            button.Foreground = Brush("CanvasAccentPrimaryBrush");
            button.BorderBrush = Brush("CanvasBorderBrush");
            button.BorderThickness = new Thickness(1);
            button.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }
        else
        {
            button.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            button.Foreground = Brush("CanvasSecondaryBrush");
            button.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            button.BorderThickness = new Thickness(0);
            button.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    private void OnLanguageChanged(object? sender, CultureInfo e) => ApplyLocalization();

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeInspectorViewModel.IsOpen) or nameof(NodeInspectorViewModel.SelectedSnapshot))
        {
            UpdateVisibility();
        }
        else if (e.PropertyName is nameof(NodeInspectorViewModel.InspectedNode))
        {
            RefreshNode();
        }
    }

    /// <summary>
    /// Crea el envoltorio desplazable de una pestaña con NOMBRE y lo registra. El nombre entra en el rastro
    /// del foco (hito 253) y la lista es la que audita la sonda del selfcheck.
    /// </summary>
    private ScrollViewer NamedPane(string name, UIElement content)
    {
        var pane = new ScrollViewer
        {
            Name = name,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };

        return pane;
    }

    /// <summary>Quién tiene el foco, en palabras, para el rastro del 253.</summary>
    private string DescribeInspectorFocus()
    {
        try
        {
            return CanvasFocusTrace.Describe(
                Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot), ancestors: 3);
        }
        catch (Exception ex)
        {
            return "sin consultar (" + ex.GetType().Name + ")";
        }
    }

    private void RefreshNode()
    {
        // El rastro del foco (hito 253): la reconstrucción que sigue a la selección se anota con el nodo y
        // la pestaña activa, para poder casarla con el cambio de foco del gestor. Los envoltorios de las
        // pestañas llevan NOMBRE a propósito: el que se lleva el foco no tiene ancestros en el árbol visual
        // (los de las pestañas no seleccionadas no están realizados), así que su nombre es lo único que lo
        // identifica.
        CanvasFocusTrace.Write($"inspector: refresco de nodo (pestaña={_selectedTab}, "
                             + $"pestanas={InspectorTabs.Length}) antes={DescribeInspectorFocus()}");

        // El nodo anterior deja de notificar: el panel sólo vive del nodo inspeccionado. (La TELEMETRÍA no se
        // suelta aquí: su sección se ata y se suelta en su propio Bind, que es quien escucha sus medidas.)
        if (_inspected is not null && _paramsSub is not null)
        {
            _inspected.Parameters.CollectionChanged -= _paramsSub;
        }

        _inspected = _vm?.InspectedNode;

        if (_inspected is null)
        {
            _paramsHost.Children.Clear();
            _actionsHost.Children.Clear();
            _actionControls.Clear();
            _actionsHeader.Visibility = Visibility.Collapsed;
            _paramsHeader.Visibility = Visibility.Collapsed;
            _telemetrySection.Bind(null);
            _snapshotsHost.Children.Clear();
            _inputsHost.Children.Clear();
            _outputsHost.Children.Clear();
            _diffHost.Children.Clear();
            RefreshHeaderTexts();
            UpdateVisibility();
            return;
        }

        _paramsSub = (_, _) => RebuildParameters();
        _inspected.Parameters.CollectionChanged += _paramsSub;

        // Las colecciones de snapshots del nodo (hito 242): las dos pestañas nuevas viven de ellas.
        if (_inputsSub is not null)
        {
            _inspected.InputSnapshots.CollectionChanged -= _inputsSub;
        }

        if (_outputsSub is not null)
        {
            _inspected.OutputSnapshots.CollectionChanged -= _outputsSub;
        }

        _inputsSub = (_, _) => RebuildAllSnapshotViews();
        _outputsSub = (_, _) => RebuildAllSnapshotViews();
        _inspected.InputSnapshots.CollectionChanged += _inputsSub;
        _inspected.OutputSnapshots.CollectionChanged += _outputsSub;

        RebuildParameters();
        RebuildActions();
        RebuildAllSnapshotViews();
        _telemetrySection.Bind(_inspected);
        RefreshHeaderTexts();
        UpdateVisibility();
    }

    /// <summary>
    /// La pestaña de snapshots (hito 242): los snapshots del NODO (entradas y salidas), con la
    /// cabecera del escritorio (puerto, timestamp, ruta actual), el contenido desplegable (ruta
    /// original, tamaño, metadatos, tags, error) y el botón «Ver» por el comando canónico del VM
    /// (<c>PreviewSpecificSnapshotCommand</c> — la misma vista previa del escritorio).
    /// </summary>
    /// <summary>
    /// Un cambio en las colecciones reconstruye las TRES vistas que comparten el dato (la
    /// combinada del 241 y las separadas del 244): la pestaña separada no puede quedar al día
    /// mientras la combinada se queda congelada, ni al revés.
    /// </summary>
    private void RebuildAllSnapshotViews()
    {
        RebuildSnapshots();
        RebuildInputCards();
        RebuildOutputCards();
    }

    private void RebuildSnapshots()
    {
        // La pestaña combinada (el orden del 241: entradas y luego salidas) y las dos
        // separadas del 244 comparten tarjeta y fuente — tres vistas, UNA colección por dato.
        _snapshotsHost.Children.Clear();
        if (_inspected is null)
        {
            return;
        }

        var loc = LocalizationManager.Instance;
        // Las tarjetas cantan su colección e índice para la observación UIA externa (hito 245):
        // la MISMA familia de anclas en las tres vistas, así el árbol expone la paridad de datos
        // sin descifrar jerarquías de contenedores.
        int inputIndex = 0;
        foreach (var snapshot in _inspected.InputSnapshots)
        {
            _snapshotsHost.Children.Add(BuildSnapshotCard(snapshot, loc, "InspectorSnapshotCard_in_" + inputIndex++));
        }

        int outputIndex = 0;
        foreach (var snapshot in _inspected.OutputSnapshots)
        {
            _snapshotsHost.Children.Add(BuildSnapshotCard(snapshot, loc,
                "InspectorSnapshotCard_out_" + snapshot.PortName + "_" + outputIndex++));
        }
    }

    /// <summary>La pestaña de ENTRADAS (hito 244): sólo InputSnapshots del nodo.</summary>
    private void RebuildInputCards()
    {
        _inputsHost.Children.Clear();
        if (_inspected is null)
        {
            return;
        }

        var loc = LocalizationManager.Instance;
        int inputIndex = 0;
        foreach (var snapshot in _inspected.InputSnapshots)
        {
            _inputsHost.Children.Add(BuildSnapshotCard(snapshot, loc, "InspectorSnapshotCard_in_" + inputIndex++));
        }
    }

    /// <summary>La pestaña de SALIDAS (hito 244): sólo OutputSnapshots del nodo.</summary>
    private void RebuildOutputCards()
    {
        _outputsHost.Children.Clear();
        if (_inspected is null)
        {
            return;
        }

        var loc = LocalizationManager.Instance;
        int outputIndex = 0;
        foreach (var snapshot in _inspected.OutputSnapshots)
        {
            _outputsHost.Children.Add(BuildSnapshotCard(snapshot, loc,
                "InspectorSnapshotCard_out_" + snapshot.PortName + "_" + outputIndex++));
        }
    }

    private FrameworkElement BuildSnapshotCard(NodeDataSnapshot snapshot, LocalizationManager loc, string? anchorKey = null)
    {
        var root = new StackPanel { Spacing = 4, Margin = new Thickness(0, 2, 0, 2) };

        var header = new Grid { ColumnSpacing = 6 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerLines = new StackPanel { Spacing = 1 };
        var line1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        line1.Children.Add(new TextBlock
        {
            Text = (snapshot.IsInput ? "▼ In: " : "▲ Out: ") + snapshot.PortName,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(snapshot.IsInput ? "CanvasAccentGlowBrush" : "CanvasWireBrush")
        });
        line1.Children.Add(new TextBlock
        {
            Text = " • " + snapshot.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture),
            FontSize = 10,
            Opacity = 0.7,
            Foreground = Brush("CanvasSecondaryBrush")
        });
        headerLines.Children.Add(line1);
        headerLines.Children.Add(new TextBlock
        {
            Text = snapshot.ItemSnapshot.CurrentPath,
            FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Brush("CanvasTextBrush")
        });
        Grid.SetColumn(headerLines, 0);
        header.Children.Add(headerLines);

        // El «Ver» del escritorio: el comando canónico del VM (la vista previa vive en el núcleo).
        var viewButton = new Button
        {
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 10,
            Content = loc.GetString("Preview_InspectFileBtn", "Ver")
        };
        AutomationProperties.SetAutomationId(viewButton, "SnapshotViewButton_" + snapshot.SnapshotId);
        viewButton.Click += (_, _) => _vm?.PreviewSpecificSnapshotCommand.Execute(snapshot);
        Grid.SetColumn(viewButton, 1);
        header.Children.Add(viewButton);

        root.Children.Add(header);

        // El contenido desplegable: la misma información que el Expander del escritorio.
        var details = new StackPanel { Spacing = 3, Margin = new Thickness(12, 2, 0, 0) };
        details.Children.Add(new TextBlock
        {
            Text = loc.GetString("Inspector_OriginalPath", "Original Path:") + " " + snapshot.ItemSnapshot.OriginalPath,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("CanvasSecondaryBrush")
        });
        details.Children.Add(new TextBlock
        {
            Text = loc.GetString("Inspector_SizeLabel", "Size:") + " " + snapshot.ItemSnapshot.FileSizeBytes + " "
                + loc.GetString("Inspector_BytesLabel", "bytes"),
            FontSize = 10,
            Foreground = Brush("CanvasSecondaryBrush")
        });

        foreach (var kv in snapshot.ItemSnapshot.Metadata)
        {
            details.Children.Add(new TextBlock
            {
                Text = kv.Key + " = " + kv.Value,
                FontSize = 10,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas"),
                Foreground = Brush("CanvasTextBrush")
            });
        }

        if (snapshot.ItemSnapshot.Tags.Count > 0)
        {
            details.Children.Add(new TextBlock
            {
                Text = loc.GetString("Inspector_Tags", "Tags:") + " " + string.Join(", ", snapshot.ItemSnapshot.Tags),
                FontSize = 10,
                Foreground = Brush("CanvasSecondaryBrush")
            });
        }

        if (snapshot.HasError)
        {
            details.Children.Add(new TextBlock
            {
                Text = "⚠ " + snapshot.ErrorMessage,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 158, 11))
            });
        }

        var expander = new Expander
        {
            Content = details,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        expander.Header = header;

        // La tarjeta canta su colección e índice para la observación UIA externa (hito 245): el
        // AID vive en el Expander (con peer) porque un StackPanel raíz sin peer no materializa en
        // el árbol — la lección del 238.
        AutomationProperties.SetAutomationId(expander,
            anchorKey ?? "InspectorSnapshotCard_" + (snapshot.IsInput ? "in" : "out_" + snapshot.PortName)
                + "_" + snapshot.SnapshotId.ToString("N")[..8]);
        root.Children.Add(expander);

        return root;
    }

    /// <summary>Contador de claves repetidas para el AutomationId de fila de diff (hito 245).</summary>
    private static int _diffKeyCounter;

    /// <summary>
    /// La pestaña de diff (hito 242): las filas de <c>MetadataDiffs</c> que el VM del núcleo
    /// computa (al inspeccionar y al seleccionar un snapshot) — Added/Removed/Modified con los
    /// colores del escritorio.
    /// </summary>
    private void RebuildDiff()
    {
        _diffHost.Children.Clear();
        if (_vm is null)
        {
            return;
        }

        foreach (var diff in _vm.MetadataDiffs)
        {
            _diffHost.Children.Add(BuildDiffRow(diff));
        }
    }

    private FrameworkElement BuildDiffRow(MetadataDiffItem diff)
    {
        var changeColor = diff.ChangeType switch
        {
            "Added" => Windows.UI.Color.FromArgb(255, 16, 185, 129),
            "Removed" => Windows.UI.Color.FromArgb(255, 239, 68, 68),
            _ => Windows.UI.Color.FromArgb(255, 245, 158, 11)
        };

        var key = new TextBlock
        {
            Text = diff.Key,
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(changeColor),
            VerticalAlignment = VerticalAlignment.Center
        };
        var values = new TextBlock
        {
            Text = diff.ChangeType == "Added" ? "→ " + diff.NewValue
                : diff.ChangeType == "Removed" ? diff.OldValue + " →"
                : diff.OldValue + " → " + diff.NewValue,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("CanvasTextBrush")
        };

        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(key, 0);
        Grid.SetColumn(values, 1);
        grid.Children.Add(key);
        grid.Children.Add(values);

        // La clave canta su AutomationId para la observación UIA externa (hito 245): el TextBlock
        // con peer es la fila viva del árbol — un StackPanel raíz sin peer no materializa (la
        // lección del 238). Con claves repetidas, un sufijo mantiene el AID único.
        string aid = "InspectorDiffKey_" + diff.Key;
        if (_diffHost.Children.OfType<FrameworkElement>().Any(existing =>
                AutomationProperties.GetAutomationId(existing) == aid))
        {
            aid += "#" + _diffKeyCounter++;
        }

        AutomationProperties.SetAutomationId(key, aid);
        return new StackPanel { Children = { grid } };
    }

    private void RefreshHeaderTexts()
    {
        var loc = LocalizationManager.Instance;
        _titleText.Text = _inspected?.Title ?? loc.GetString("Uno_InspectorTitle", "Inspector");
        _emptyText.Text = loc.GetString("Uno_InspectorEmpty", "Selecciona un nodo para inspeccionarlo.");
        _descriptionText.Text = _inspected?.Description ?? string.Empty;
        _descriptionText.Visibility = string.IsNullOrEmpty(_descriptionText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// El estado de la ficha en UN solo sitio: si está abierta, si toca el texto de «sin selección» o el cuerpo,
    /// y <b>qué se OFRECE en cada estado</b>. El «Probar» es el caso que este método tenía a medias (hito 274): se
    /// dibujaba siempre —con nodo y sin él— y sin nodo no hay nada que probar, así que quedaba ofrecido, habilitado
    /// y sin efecto. La condición vive aquí, con las demás, y no en el manejador del clic: el control no se ofrece
    /// cuando no tiene trabajo, igual que el bloque de ACCIONES se colapsa sin acciones y el encabezado de
    /// PARÁMETROS sin parámetros.
    /// </summary>
    private void UpdateVisibility()
    {
        bool isOpen = _vm?.IsOpen == true;
        bool hasNode = _inspected is not null;
        Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        _emptyText.Visibility = isOpen && !hasNode ? Visibility.Visible : Visibility.Collapsed;
        _body.Visibility = isOpen && hasNode ? Visibility.Visible : Visibility.Collapsed;
        _testButton.Visibility = isOpen && hasNode ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Ata una CAJA DE TEXTO de una fila al parámetro en LOS DOS SENTIDOS: la caja escribe el valor (lo que
    /// teclea el usuario) y el valor actualiza la caja (lo que escriben los DIÁLOGOS).
    ///
    /// <para><b>Por qué la vuelta no es un adorno.</b> El editor de texto y el catálogo de variables devuelven
    /// el valor por el view model portable (<c>SaveResult</c>, <c>InsertVariableToken</c>), no por el teclado:
    /// con sólo la ida, insertar «{FileName}» desde el catálogo cambiaba el parámetro del nodo y dejaba el
    /// campo con el texto viejo — el usuario veía que no había pasado nada y volvía a insertarlo. Las filas de
    /// casilla y desplegable no lo necesitan porque su enlace ya es bidireccional; estas cajas se atan a mano
    /// porque el valor es un <c>object</c> y el texto quiere pasar por <c>ToString</c>.</para>
    ///
    /// <para>Las suscripciones se sueltan al reconstruir las filas (<see cref="_rowValueSubscriptions"/>): sin
    /// eso, cada selección de nodo dejaría viva una suscripción del parámetro a una caja que ya no está.</para>
    /// </summary>
    private void WireBoxToParameter(TextBox box, NodeParameterViewModel p)
    {
        box.TextChanged += (_, _) =>
        {
            if (box.Text != (p.Value?.ToString() ?? string.Empty))
            {
                p.Value = box.Text;
            }
        };

        void OnParameterChanged(object? _, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(NodeParameterViewModel.Value))
            {
                return;
            }

            string value = p.Value?.ToString() ?? string.Empty;
            if (box.Text != value)
            {
                box.Text = value;
            }
        }

        p.PropertyChanged += OnParameterChanged;
        _rowValueSubscriptions.Add((p, OnParameterChanged));
    }

    // ── La tabla de editores: los mismos flags del VM que el escritorio usa en su Selector ──

    private void RebuildParameters()
    {
        foreach (var (param, handler) in _rowValueSubscriptions)
        {
            param.PropertyChanged -= handler;
        }

        _rowValueSubscriptions.Clear();
        _paramsHost.Children.Clear();
        _paramControls.Clear();
        if (_inspected is null)
        {
            return;
        }

        foreach (var parameter in _inspected.Parameters)
        {
            var row = BuildParameterRow(parameter);
            if (row is not null)
            {
                _paramsHost.Children.Add(row);
            }
        }

        // El sondeo (y cualquier lector) cuenta editores con la cuenta de parámetros del nodo: la
        // fila SIEMPRE se construye (encabezado + editor), sin excepciones ocultas. Y el encabezado se
        // colapsa si no hay editores —la misma regla del bloque de acciones—: un encabezado sobre una
        // lista vacía promete algo que no hay.
        _paramsHeader.Visibility = _paramsHost.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Las ACCIONES del nodo en la ficha: un botón por acción declarada, con su rótulo y su descripción, que
    /// ejecuta <c>ExecuteCommand</c> del <see cref="NodeActionViewModel"/> —la MISMA orden del núcleo que el
    /// botón de la tarjeta del lienzo (<c>NodeViewModel.ExecuteCustomAction</c>)—. El host no reimplementa
    /// ninguna superficie: sirve la que el nodo declara, con su catálogo de diálogos.
    ///
    /// <para><b>Por qué la ficha las lleva</b>. Hasta aquí estas acciones vivían SÓLO en el panel plegable de
    /// la tarjeta del lienzo, y son la única puerta a las superficies del nodo (el gestor de presets, la
    /// configuración del VLM, el estudio de scripts, el diseñador de datasets...). El inspector es donde el
    /// escritorio las pinta y donde el usuario las busca: sin este bloque, la acción existía, el comando
    /// existía y no había dónde pulsarlo desde la ficha.</para>
    /// </summary>
    private void RebuildActions()
    {
        _actionsHost.Children.Clear();
        _actionControls.Clear();

        if (_inspected is null)
        {
            _actionsHeader.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var action in _inspected.CustomActions)
        {
            var button = new Button
            {
                Content = action.Title,
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
                CornerRadius = new CornerRadius(6),
                Background = Brush("CanvasSurfaceBrush"),
                BorderBrush = Brush("CanvasBorderBrush"),
                BorderThickness = new Thickness(1),
                Foreground = Brush("CanvasTextBrush"),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            AnchorAction("InspectorAction_" + action.ActionId, button);

            // La orden se resuelve al pulsar (el botón guarda su acción, no un identificador suelto): el
            // comando es el del view model portable y su ejecución acaba en ExecuteCustomAction del nodo.
            button.Click += (_, _) => action.ExecuteCommand.Execute(null);

            ToolTipService.SetToolTip(button, action.Tooltip ?? action.Title);
            _actionsHost.Children.Add(button);
        }

        // El bloque entero se colapsa sin acciones: un encabezado sobre una lista vacía promete algo que no hay.
        _actionsHeader.Visibility = _actionsHost.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private UIElement? BuildParameterRow(NodeParameterViewModel p)
    {
        var root = new StackPanel { Spacing = 3 };

        var name = new TextBlock
        {
            Text = p.DisplayName,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 2),
            Foreground = Brush("CanvasTextBrush")
        };
        root.Children.Add(name);

        FrameworkElement editor;
        if (p.IsToggle)
        {
            var toggle = new ToggleSwitch
            {
                OnContent = null,
                OffContent = null,
                Margin = new Thickness(0, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Anchor("ParamToggle_" + p.Key, toggle);
            toggle.SetBinding(ToggleSwitch.IsOnProperty, new Binding
            {
                Path = new PropertyPath(nameof(p.ValueAsBool)),
                Source = p,
                Mode = BindingMode.TwoWay
            });
            editor = toggle;
        }
        else if (p.IsSlider)
        {
            var slider = new Slider { Minimum = p.SliderMin, Maximum = p.SliderMax, StepFrequency = Math.Max(p.SliderStep, 0.01) };
            Anchor("ParamSlider_" + p.Key, slider);
            slider.SetBinding(Slider.ValueProperty, new Binding
            {
                Path = new PropertyPath(nameof(p.SliderValue)),
                Source = p,
                Mode = BindingMode.TwoWay
            });
            var display = new TextBlock
            {
                FontSize = 11,
                Foreground = Brush("CanvasSecondaryBrush")
            };
            display.SetBinding(TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath(nameof(p.SliderDisplayValue)),
                Source = p,
                Mode = BindingMode.OneWay
            });
            var stack = new StackPanel { Spacing = 0 };
            stack.Children.Add(slider);
            stack.Children.Add(display);
            editor = stack;
        }
        else if (p.IsDropdown)
        {
            var combo = new ComboBox
            {
                ItemsSource = p.Options,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontSize = 12
            };
            Anchor("ParamDropdown_" + p.Key, combo);
            combo.SetBinding(ComboBox.SelectedItemProperty, new Binding
            {
                Path = new PropertyPath(nameof(p.Value)),
                Source = p,
                Mode = BindingMode.TwoWay
            });
            editor = combo;
        }
        else if (p.HasBrowseButton)
        {
            var box = new TextBox { FontSize = 12, CornerRadius = new CornerRadius(4) };
            Anchor("ParamBox_" + p.Key, box);
            WireBoxToParameter(box, p);
            var browse = new Button
            {
                Content = "…",
                Padding = new Thickness(8, 2, 8, 2),
                FontSize = 12,
                CornerRadius = new CornerRadius(4),
                Background = Brush("CanvasSurfaceBrush"),
                BorderBrush = Brush("CanvasBorderBrush"),
                BorderThickness = new Thickness(1),
                Foreground = Brush("CanvasTextBrush")
            };
            Anchor("ParamBrowse_" + p.Key, browse);
            // La variante ASÍNCRONA del explorador de rutas (hito 273): este host abre sus pickers desde el
            // clic de UI, y allí la síncrona no puede —los pickers de WinRT exigen el hilo de UI y
            // bloquearlo interbloquearía, así que el servicio del host devuelve null—: con la síncrona el
            // botón «…» quedaba dibujado y sin efecto (medido con el ratón).
            browse.Click += (_, _) => p.BrowsePathAsyncCommand.Execute(null);
            var grid = new Grid { ColumnSpacing = 4 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(box, 0);
            Grid.SetColumn(browse, 1);
            grid.Children.Add(box);
            grid.Children.Add(browse);
            editor = grid;
        }
        else if (p.IsMultiLine)
        {
            var box = new TextBox
            {
                AcceptsReturn = true,
                Height = 72,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                CornerRadius = new CornerRadius(4)
            };
            ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
            Anchor("ParamBox_" + p.Key, box);
            WireBoxToParameter(box, p);
            editor = box;
        }
        else
        {
            // Texto estándar y número: una caja (el número deja la validación al nodo, igual que la ficha).
            var box = new TextBox { FontSize = 12, CornerRadius = new CornerRadius(4) };
            Anchor("ParamBox_" + p.Key, box);
            WireBoxToParameter(box, p);
            editor = box;
        }

        // Las ACCIONES de la fila (rebanada 5.3): abrir el EDITOR enriquecido —sólo donde el valor es un
        // texto largo, como la ficha del escritorio— e insertar una VARIABLE por el selector. Son la
        // puerta del usuario a los dos diálogos del host: sin ellas los comandos del núcleo existirían y
        // no habría quien los pulsara, que es exactamente el botón-que-no-hace-nada que este tramo viene
        // a quitar.
        root.Children.Add(WrapWithRowActions(p, editor));

        // El valor evaluado con su copia (la fila que el escritorio pinta bajo el campo).
        if (p.HasExpression)
        {
            var evaluated = new TextBlock
            {
                FontSize = 10,
                Opacity = 0.8,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Brush("CanvasSuccessBrush")
            };
            evaluated.SetBinding(TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath(nameof(p.EvaluatedValue)),
                Source = p,
                Mode = BindingMode.OneWay
            });

            var copyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var copyButton = new Button
            {
                FontSize = 10,
                Padding = new Thickness(6, 0, 6, 0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Content = "⧉"
            };
            copyButton.Click += (_, _) => p.CopyEvaluatedValueCommand.Execute(null);
            copyRow.Children.Add(copyButton);
            copyRow.Children.Add(evaluated);
            root.Children.Add(copyRow);
        }

        return new Border
        {
            Background = Brush("CanvasSurfaceBrush"),
            BorderBrush = Brush("CanvasBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 2, 0, 4),
            Child = root
        };
    }

    /// <summary>
    /// Los DIÁLOGOS DE FILA que este host sirve, con el prefijo de AutomationId de su botón. Es la mitad
    /// positiva de la tabla de paridad: cada orden de fila del escritorio tiene que estar aquí —dibujada— o
    /// en <see cref="DeclaredPendingRowActions"/>. La mitad negativa no es adorno: un portado a medias se ve
    /// igual desde dentro que un portado completo, y el usuario sólo descubre el hueco cuando busca el botón.
    /// </summary>
    internal static readonly (string Command, string Anchor, string What)[] HostRowActions =
    [
        ("BrowsePathAsyncCommand", "ParamBrowse_", "el explorador de rutas del núcleo en su variante ASÍNCRONA (el botón «…» de las filas de ruta): el host abre sus pickers desde el clic de UI, donde la síncrona no puede —exige el hilo de UI y bloquearlo interbloquearía, así que el servicio del host devuelve null y el botón quedaba sin efecto"),
        ("OpenTextEditorCommand", "ParamEditor_", "el editor de texto y prompts expandido (el botón «✎» del valor largo)"),
        ("OpenVariableCatalogCommand", "ParamVariable_", "el catálogo de variables del núcleo: el botón «{x}» abre DIRECTO su primera entrada, el catálogo completo"),
        ("OpenMediaPresetManagerCommand", "ParamPreset_", "el gestor de presets del nodo (el botón «🎬» de la fila del preset): la orden pide la superficie que DECLARA el nodo y la sirve el catálogo de diálogos de este host sobre su view model portable"),
        ("OpenPasswordManagerCommand", "ParamPassword_", "el gestor de contraseñas del nodo (el botón «🔑» de la fila de la lista de claves): la orden pide la superficie que DECLARA el nodo —igual que la acción «🔑 Claves...» de su tarjeta— y la sirve el catálogo de diálogos de este host sobre su view model portable"),
    ];

    /// <summary>
    /// Los diálogos que el escritorio abre desde una fila de parámetro y este host NO sirve, cada uno con su
    /// razón. Lo que no llega queda declarado, nunca fingido.
    /// </summary>
    internal static readonly (string Command, string Reason)[] DeclaredPendingRowActions =
    [
        ("OpenVariablePickerCommand", "el menú emergente de variables del escritorio (el botón «{x}» despliega un menú con el catálogo agrupado): este host no tiene menú emergente y su «{x}» abre directamente el CATÁLOGO COMPLETO, que es la primera entrada de aquél"),
    ];

    /// <summary>
    /// Las ACCIONES de una fila de parámetro: la caja (que ya viene construida) más los botones que
    /// abren los diálogos del host. Se envuelve SÓLO cuando la fila tiene alguna acción, así que el
    /// resto de filas quedan exactamente como estaban.
    ///
    /// <para><b>Qué acción lleva cada fila</b>, con los mismos flags del VM que usa el escritorio: el
    /// EDITOR de texto va en el valor largo (<c>IsMultiLine</c>) y el botón de VARIABLES en las filas
    /// cuyo valor es texto —el multilínea, la ruta con explorar y el texto estándar—, el GESTOR DE PRESETS en la
    /// fila del preset y el GESTOR DE CONTRASEÑAS en la de la lista de claves, que son las mismas filas que el
    /// escritorio marca. El selector de
    /// variables del host abre el CATÁLOGO COMPLETO (el mismo diálogo al que el escritorio llega por el
    /// menú rápido del botón «{x}»): este host todavía no tiene el menú emergente, y ese paso de menos
    /// está declarado.</para>
    /// </summary>
    private FrameworkElement WrapWithRowActions(NodeParameterViewModel p, FrameworkElement editor)
    {
        bool wantsEditor = p.IsMultiLine;
        bool wantsVariables = p.IsMultiLine || p.HasBrowseButton || RowValueIsPlainText(p);
        bool wantsPresets = p.IsMediaPreset;
        bool wantsPassword = p.IsPasswordList;
        if (!wantsEditor && !wantsVariables && !wantsPresets && !wantsPassword)
        {
            return editor;
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Top };
        if (wantsEditor)
        {
            actions.Children.Add(RowActionButton(
                "ParamEditor_" + p.Key,
                "✎",
                "Uno_Dialog_OpenEditorToolTip",
                "Editor de Texto y Prompts Expandido",
                p.OpenTextEditorCommand));
        }

        if (wantsVariables)
        {
            actions.Children.Add(RowActionButton(
                "ParamVariable_" + p.Key,
                "{x}",
                "Uno_Dialog_InsertVariableToolTip",
                "Insertar variable dinámicamente ({x})",
                p.OpenVariableCatalogCommand));
        }

        // El GESTOR DE PRESETS: la misma fila que el escritorio marca con su «Presets» (la del editor de
        // presets de medios). La orden es la de la fila —la misma que el escritorio—, y la cumple la
        // superficie que declara el nodo: el host no reimplementa el gestor, lo sirve.
        if (wantsPresets)
        {
            actions.Children.Add(RowActionButton(
                "ParamPreset_" + p.Key,
                "🎬",
                "Node_Param_OpenPresetManager",
                "Abrir el Gestor de Presets (Crear, Editar, Eliminar Presets)",
                p.OpenMediaPresetManagerCommand));
        }

        // El GESTOR DE CONTRASEÑAS: la misma fila que el escritorio marca con su botón «Claves» (la de la lista
        // de claves del nodo). La orden es la de la fila —la misma que el escritorio— y la cumple la superficie
        // que declara el nodo: el host no reimplementa el gestor, lo sirve. Antes esta fila no tenía botón y la
        // capacidad se declaraba pendiente en la tabla mientras la TARJETA del nodo la ofrecía: dos puertas de
        // acuerdo es lo que esta línea cierra.
        if (wantsPassword)
        {
            actions.Children.Add(RowActionButton(
                "ParamPassword_" + p.Key,
                "🔑",
                "Node_Param_OpenPasswordManager",
                "Gestionar lista de contraseñas (Importar / Exportar / Editar)",
                p.OpenPasswordManagerCommand));
        }

        var grid = new Grid { ColumnSpacing = 4 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(editor, 0);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(editor);
        grid.Children.Add(actions);
        return grid;
    }

    /// <summary>
    /// ¿La fila es de texto libre? Es el mismo resto del selector del escritorio: ni casilla, ni
    /// deslizador, ni desplegable, ni ruta con explorar. Sólo esas filas llevan el botón de variables.
    /// </summary>
    private static bool RowValueIsPlainText(NodeParameterViewModel p) =>
        !p.IsToggle && !p.IsSlider && !p.IsDropdown && !p.HasBrowseButton && !p.IsMultiLine;

    /// <summary>Un botón de la fila: ejecuta el comando del VM y se localiza en caliente.</summary>
    private Button RowActionButton(string automationId, string glyph, string tipKey, string tipFallback,
        System.Windows.Input.ICommand command)
    {
        var button = new Button
        {
            Content = glyph,
            Padding = new Thickness(6, 2, 6, 2),
            FontSize = 11,
            CornerRadius = new CornerRadius(4),
            Background = Brush("CanvasSurfaceBrush"),
            BorderBrush = Brush("CanvasBorderBrush"),
            BorderThickness = new Thickness(1),
            Foreground = Brush("CanvasTextBrush"),
            VerticalAlignment = VerticalAlignment.Top,
        };
        button.Click += (_, _) => command.Execute(null);
        Anchor(automationId, button);

        ToolTipService.SetToolTip(button, LocalizationManager.Instance.GetString(tipKey, tipFallback));
        return button;
    }

    /// <summary>
    /// Ancla un control de una fila: le pone su AutomationId Y lo deja en la tabla, que es la que
    /// permite encontrarlo desde fuera (el driver externo por UIA y la sonda en proceso) sin descifrar
    /// el árbol.
    /// </summary>
    private void Anchor(string automationId, Control control)
    {
        AutomationProperties.SetAutomationId(control, automationId);
        _paramControls[automationId] = control;
    }

}
